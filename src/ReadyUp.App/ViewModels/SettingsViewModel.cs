using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReadyUp.App.Services;
using ReadyUp.Core.Interfaces;
using ReadyUp.Core.Models;

namespace ReadyUp.App.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly UiScaleService _uiScaleService;
    private readonly IFileDialogService _fileDialogService;

    [ObservableProperty]
    private double _uiScale;

    [ObservableProperty]
    private ShellMode _preferredStartupMode;

    [ObservableProperty]
    private double _gamepadDeadzone;

    [ObservableProperty]
    private bool _enableRawDriveScan;

    [ObservableProperty]
    private int _rawDriveScanMaxDepth;

    [ObservableProperty]
    private bool _enableOnlineMetadata;

    [ObservableProperty]
    private string? _steamGridDbApiKey;

    [ObservableProperty]
    private string? _igdbClientId;

    [ObservableProperty]
    private string? _igdbClientSecret;

    public ObservableCollection<string> AdditionalLibraryFolders { get; } = new();

    public SettingsViewModel(ISettingsService settingsService, UiScaleService uiScaleService, IFileDialogService fileDialogService)
    {
        _settingsService = settingsService;
        _uiScaleService = uiScaleService;
        _fileDialogService = fileDialogService;

        var current = _settingsService.Current;
        _uiScale = current.UiScale;
        _preferredStartupMode = current.PreferredStartupMode;
        _gamepadDeadzone = current.GamepadDeadzone;
        _enableRawDriveScan = current.EnableRawDriveScan;
        _rawDriveScanMaxDepth = current.RawDriveScanMaxDepth;
        _enableOnlineMetadata = current.EnableOnlineMetadata;
        _steamGridDbApiKey = current.SteamGridDbApiKey;
        _igdbClientId = current.IgdbClientId;
        _igdbClientSecret = current.IgdbClientSecret;

        foreach (var folder in current.AdditionalLibraryFolders)
        {
            AdditionalLibraryFolders.Add(folder);
        }
    }

    partial void OnUiScaleChanged(double value) => _ = _uiScaleService.SetScaleAsync(value);

    [RelayCommand]
    private async Task AddLibraryFolderAsync()
    {
        var folder = _fileDialogService.PickFolder();
        if (string.IsNullOrWhiteSpace(folder) || AdditionalLibraryFolders.Contains(folder)) return;

        AdditionalLibraryFolders.Add(folder);
        await SaveAsync().ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task RemoveLibraryFolderAsync(string folder)
    {
        AdditionalLibraryFolders.Remove(folder);
        await SaveAsync().ConfigureAwait(false);
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        await _settingsService.UpdateAsync(settings =>
        {
            settings.UiScale = UiScale;
            settings.PreferredStartupMode = PreferredStartupMode;
            settings.GamepadDeadzone = GamepadDeadzone;
            settings.EnableRawDriveScan = EnableRawDriveScan;
            settings.RawDriveScanMaxDepth = RawDriveScanMaxDepth;
            settings.EnableOnlineMetadata = EnableOnlineMetadata;
            settings.SteamGridDbApiKey = SteamGridDbApiKey;
            settings.IgdbClientId = IgdbClientId;
            settings.IgdbClientSecret = IgdbClientSecret;
            settings.AdditionalLibraryFolders = AdditionalLibraryFolders.ToList();
        }).ConfigureAwait(false);
    }
}
