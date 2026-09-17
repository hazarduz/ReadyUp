using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReadyUp.App.Services;
using ReadyUp.Core.Interfaces;
using ReadyUp.Core.Models;

namespace ReadyUp.App.ViewModels;

/// <summary>
/// Backs the "Change Game Art" dialog (desktop right-click) and the
/// gamepad-driven game-options overlay (full-screen, Y button) — the same
/// SteamGridDB-Decky-style flow, just rendered with different views.
/// </summary>
public sealed partial class ChangeArtViewModel : ObservableObject
{
    private readonly IGameLibraryService _libraryService;
    private readonly IArtCacheStore _artCache;
    private readonly IReadOnlyList<IArtProvider> _artProviders;
    private readonly IFileDialogService _fileDialogService;

    [ObservableProperty]
    private GameViewModel? _game;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedTypeLabel))]
    private ArtAssetType _selectedType = ArtAssetType.BoxArt;

    [ObservableProperty]
    private bool _isSearching;

    [ObservableProperty]
    private string? _statusMessage;

    public ObservableCollection<ArtCandidate> SearchResults { get; } = new();

    public string SelectedTypeLabel => SelectedType switch
    {
        ArtAssetType.Icon => "Icon",
        ArtAssetType.Banner => "Banner",
        ArtAssetType.BoxArt => "Box Art",
        ArtAssetType.Background => "Background",
        _ => SelectedType.ToString(),
    };

    public ChangeArtViewModel(
        IGameLibraryService libraryService,
        IArtCacheStore artCache,
        IEnumerable<IArtProvider> artProviders,
        IFileDialogService fileDialogService)
    {
        _libraryService = libraryService;
        _artCache = artCache;
        _artProviders = artProviders.ToList();
        _fileDialogService = fileDialogService;
    }

    public void Initialize(GameViewModel game, ArtAssetType initialType)
    {
        Game = game;
        SelectedType = initialType;
        SearchResults.Clear();
        StatusMessage = null;
    }

    partial void OnSelectedTypeChanged(ArtAssetType value) => SearchResults.Clear();

    [RelayCommand]
    private void SelectArtType(ArtAssetType type) => SelectedType = type;

    [RelayCommand]
    private async Task BrowseLocalFileAsync()
    {
        var game = Game;
        if (game is null) return;

        var path = _fileDialogService.PickImageFile();
        if (string.IsNullOrWhiteSpace(path)) return;

        var destination = await _artCache.SaveFromFileAsync(game.Id, SelectedType, path);
        await _libraryService.SetArtAsync(game.Id, SelectedType, destination, ArtSource.LocalFile, sourceUrl: null);
        StatusMessage = $"{SelectedTypeLabel} updated from local file.";
    }

    [RelayCommand]
    private async Task SearchOnlineAsync()
    {
        var game = Game;
        if (game is null) return;

        IsSearching = true;
        SearchResults.Clear();
        try
        {
            foreach (var provider in _artProviders.Where(p => p.IsAvailable))
            {
                var results = await provider.SearchAsync(game.Game, SelectedType);
                foreach (var candidate in results)
                {
                    SearchResults.Add(candidate);
                }
            }

            StatusMessage = SearchResults.Count == 0
                ? "No art found. Configure a SteamGridDB API key in Settings, or browse a local file."
                : $"{SearchResults.Count} result(s) found.";
        }
        finally
        {
            IsSearching = false;
        }
    }

    [RelayCommand]
    private async Task ApplyCandidateAsync(ArtCandidate candidate)
    {
        var game = Game;
        if (game is null) return;

        var provider = _artProviders.FirstOrDefault(p => p.Name == candidate.ProviderName);
        if (provider is null) return;

        var destination = _artCache.GetPath(game.Id, SelectedType);
        await provider.ApplyAsync(candidate, destination);
        await _libraryService.SetArtAsync(game.Id, SelectedType, destination, ArtSource.SteamGridDb, candidate.FullImageUrl);
        StatusMessage = $"{SelectedTypeLabel} updated from {candidate.ProviderName}.";
    }
}
