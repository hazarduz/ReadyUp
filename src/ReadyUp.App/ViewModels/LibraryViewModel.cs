using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReadyUp.App.Services;
using ReadyUp.Core.Interfaces;
using ReadyUp.Core.Models;
using ReadyUp.Metadata;
using ReadyUp.Scanning;

namespace ReadyUp.App.ViewModels;

/// <summary>
/// The library screen's view model, shared by both MainWindow (windowed)
/// and FullScreenWindow (Big Picture) so switching modes never rebuilds
/// state. Owns scanning, launching, and keeping the visible collection in
/// sync with <see cref="IGameLibraryService"/> events.
/// </summary>
public sealed partial class LibraryViewModel : ObservableObject
{
    private readonly IGameLibraryService _libraryService;
    private readonly CompositeGameScanner _scanner;
    private readonly IManualGameAdder _manualGameAdder;
    private readonly MetadataAggregatorService _metadataAggregator;
    private readonly IFileDialogService _fileDialogService;
    private readonly IScanCache _scanCache;
    private readonly Dispatcher _dispatcher;

    public ObservableCollection<GameViewModel> Games { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    [NotifyCanExecuteChangedFor(nameof(LaunchSelectedGameCommand))]
    [NotifyCanExecuteChangedFor(nameof(RemoveSelectedGameCommand))]
    [NotifyCanExecuteChangedFor(nameof(ToggleFavoriteCommand))]
    private GameViewModel? _selectedGame;

    public bool HasSelection => SelectedGame is not null;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ScanForGamesCommand))]
    private bool _isScanning;

    [ObservableProperty]
    private string _statusText = "Ready";

    public LibraryViewModel(
        IGameLibraryService libraryService,
        CompositeGameScanner scanner,
        IManualGameAdder manualGameAdder,
        MetadataAggregatorService metadataAggregator,
        IFileDialogService fileDialogService,
        IScanCache scanCache)
    {
        _libraryService = libraryService;
        _scanner = scanner;
        _manualGameAdder = manualGameAdder;
        _metadataAggregator = metadataAggregator;
        _fileDialogService = fileDialogService;
        _scanCache = scanCache;
        _dispatcher = Dispatcher.CurrentDispatcher;

        _libraryService.GameAdded += (_, game) => RunOnUiThread(() => AddOrUpdate(game));
        _libraryService.GameUpdated += (_, game) => RunOnUiThread(() => AddOrUpdate(game));
        _libraryService.GameRemoved += (_, id) => RunOnUiThread(() => RemoveById(id));
    }

    private bool _initialized;

    /// <summary>
    /// Idempotent: both MainWindow and FullScreenWindow call this from their
    /// Loaded handler (whichever is shown first, plus again the first time the
    /// other one is ever shown), but the library should only be loaded/scanned once.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_initialized) return;
        _initialized = true;

        await _scanCache.LoadAsync().ConfigureAwait(false);

        var existing = await _libraryService.GetLibraryAsync().ConfigureAwait(false);
        RunOnUiThread(() =>
        {
            foreach (var game in existing.Where(g => !g.IsHidden))
            {
                Games.Add(new GameViewModel(game));
            }
        });

        await ScanForGamesAsync().ConfigureAwait(false);
    }

    [RelayCommand(CanExecute = nameof(CanScan))]
    private async Task ScanForGamesAsync()
    {
        IsScanning = true;
        StatusText = "Scanning for games...";
        var discovered = 0;

        try
        {
            await foreach (var result in _scanner.ScanAsync())
            {
                if (result.Confidence < 0.15) continue;

                var game = await _libraryService.UpsertFromScanResultAsync(result).ConfigureAwait(false);
                discovered++;
                _ = _metadataAggregator.EnrichAsync(game); // fire-and-forget; UI updates via GameUpdated events as fields land.
            }
        }
        finally
        {
            await _scanCache.SaveAsync().ConfigureAwait(false);
            IsScanning = false;
            StatusText = discovered > 0 ? $"Scan complete — {discovered} game(s) found" : "Scan complete";
        }
    }

    private bool CanScan() => !IsScanning;

    [RelayCommand]
    private async Task AddGameManuallyAsync()
    {
        var path = _fileDialogService.PickExecutable();
        if (string.IsNullOrWhiteSpace(path)) return;

        var result = _manualGameAdder.FromExecutable(path);
        var game = await _libraryService.UpsertFromScanResultAsync(result).ConfigureAwait(false);
        _ = _metadataAggregator.EnrichAsync(game);
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void LaunchSelectedGame() => LaunchGame(SelectedGame);

    /// <summary>Also exposed as <c>LaunchGameCommand</c> (via [RelayCommand]) so GameTileControl can bind a click directly to a specific tile's game.</summary>
    [RelayCommand]
    public void LaunchGame(GameViewModel? gameViewModel)
    {
        if (gameViewModel is null) return;
        var game = gameViewModel.Game;

        var startInfo = new ProcessStartInfo(game.ExecutablePath)
        {
            WorkingDirectory = game.InstallDirectory,
            Arguments = game.LaunchArguments ?? string.Empty,
            UseShellExecute = true,
        };

        Process? process;
        try
        {
            process = Process.Start(startInfo);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException)
        {
            StatusText = $"Failed to launch {game.Title}: {ex.Message}";
            return;
        }

        if (process is null) return;

        var startedUtc = DateTime.UtcNow;
        _ = TrackPlaySessionAsync(game.Id, process, startedUtc);
    }

    private async Task TrackPlaySessionAsync(Guid gameId, Process process, DateTime startedUtc)
    {
        try
        {
            await process.WaitForExitAsync().ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            // Process already exited or access was denied to wait on it; still record what we can below.
        }

        var duration = DateTime.UtcNow - startedUtc;
        await _libraryService.RecordPlaySessionAsync(gameId, duration).ConfigureAwait(false);
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task RemoveSelectedGameAsync()
    {
        if (SelectedGame is null) return;
        await _libraryService.RemoveAsync(SelectedGame.Id).ConfigureAwait(false);
    }

    public async Task ApplyMetadataEditAsync(GameViewModel game, string title, string? description, string? publisher, string? developer)
    {
        var editedFields = new List<string> { nameof(Game.Title), nameof(Game.Description), nameof(Game.Publisher), nameof(Game.Developer) };
        await _libraryService.ApplyManualEditAsync(
            game.Id,
            g =>
            {
                g.Title = title;
                g.Description = description;
                g.Publisher = publisher;
                g.Developer = developer;
            },
            editedFields).ConfigureAwait(false);
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task ToggleFavoriteAsync()
    {
        if (SelectedGame is null) return;
        var newValue = !SelectedGame.IsFavorite;
        await _libraryService.ApplyManualEditAsync(
            SelectedGame.Id,
            g => g.IsFavorite = newValue,
            Array.Empty<string>()).ConfigureAwait(false);
    }

    private void AddOrUpdate(Game game)
    {
        var existing = Games.FirstOrDefault(g => g.Id == game.Id);
        if (existing is not null)
        {
            existing.Refresh(game);
        }
        else if (!game.IsHidden)
        {
            Games.Add(new GameViewModel(game));
        }
    }

    private void RemoveById(Guid gameId)
    {
        var existing = Games.FirstOrDefault(g => g.Id == gameId);
        if (existing is not null)
        {
            Games.Remove(existing);
            if (SelectedGame == existing) SelectedGame = null;
        }
    }

    private void RunOnUiThread(Action action)
    {
        if (_dispatcher.CheckAccess()) action();
        else _dispatcher.Invoke(action);
    }
}
