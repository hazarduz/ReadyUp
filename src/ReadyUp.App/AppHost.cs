using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using ReadyUp.App.Services;
using ReadyUp.App.ViewModels;
using ReadyUp.App.Views;
using ReadyUp.Core.Interfaces;
using ReadyUp.Core.Models;
using ReadyUp.Core.Services;
using ReadyUp.Data;
using ReadyUp.Input;
using ReadyUp.Metadata;
using ReadyUp.Scanning;

namespace ReadyUp.App;

/// <summary>Composition root: wires every subsystem (Core/Data/Scanning/Metadata/Input) into one DI container for the WPF shell.</summary>
public static class AppHost
{
    public static ServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();

        // --- Data ---
        services.AddSingleton<IGameRepository, SqliteGameRepository>();
        services.AddSingleton<IArtCacheStore, ArtCacheStore>();
        services.AddSingleton<ISettingsService, JsonSettingsService>();
        services.AddSingleton<IScanCache, JsonScanCache>();

        // --- Core orchestration ---
        services.AddSingleton<IGameLibraryService, GameLibraryService>();

        // --- Scanning ---
        services.AddSingleton<IManualGameAdder, ManualGameAdder>();
        services.AddSingleton<IGameScanner, RegistryInstalledAppsScanner>();
        services.AddSingleton<IGameScanner>(sp =>
        {
            var settings = sp.GetRequiredService<ISettingsService>().Current;
            return new DriveGameScanner(
                sp.GetRequiredService<IScanCache>(),
                settings.AdditionalLibraryFolders,
                settings.ExcludedFolders,
                settings.EnableRawDriveScan,
                settings.RawDriveScanMaxDepth);
        });
        services.AddSingleton<CompositeGameScanner>();

        // --- Metadata ---
        services.AddHttpClient();
        services.AddSingleton<LocalHeuristicMetadataProvider>();
        services.AddSingleton<IMetadataProvider>(sp => sp.GetRequiredService<LocalHeuristicMetadataProvider>());
        services.AddSingleton<IArtProvider>(sp => sp.GetRequiredService<LocalHeuristicMetadataProvider>());
        services.AddSingleton<IArtProvider>(sp => new SteamGridDbArtProvider(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(SteamGridDbArtProvider)),
            () => sp.GetRequiredService<ISettingsService>().Current.SteamGridDbApiKey));
        services.AddSingleton<IMetadataProvider>(sp => new IgdbMetadataProvider(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(IgdbMetadataProvider)),
            () =>
            {
                var settings = sp.GetRequiredService<ISettingsService>().Current;
                return (settings.IgdbClientId, settings.IgdbClientSecret);
            }));
        services.AddSingleton<MetadataAggregatorService>();

        // --- Input ---
        services.AddSingleton<GamepadService>(sp =>
            new GamepadService(sp.GetRequiredService<ISettingsService>().Current.GamepadDeadzoneAsFloat()));
        services.AddSingleton<GamepadNavigationManager>();

        // --- App-level services ---
        services.AddSingleton<UiScaleService>();
        services.AddSingleton<WindowModeManager>();
        services.AddSingleton<IFileDialogService, FileDialogService>();

        // --- View models ---
        services.AddSingleton<LibraryViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<ChangeArtViewModel>();

        // --- Windows ---
        services.AddSingleton<MainWindow>();
        services.AddSingleton<FullScreenWindow>();

        return services.BuildServiceProvider();
    }
}

internal static class SettingsExtensions
{
    /// <summary>AppSettings stores deadzone as a double (0-1); XInput consumption wants a float.</summary>
    public static float GamepadDeadzoneAsFloat(this AppSettings settings) => (float)settings.GamepadDeadzone;
}
