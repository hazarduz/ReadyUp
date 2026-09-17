using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using ReadyUp.App.Services;
using ReadyUp.Core.Interfaces;
using ReadyUp.Input;

namespace ReadyUp.App;

public partial class App : System.Windows.Application
{
    private ServiceProvider? _serviceProvider;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _serviceProvider = AppHost.BuildServiceProvider();

        var settingsService = _serviceProvider.GetRequiredService<ISettingsService>();
        await settingsService.LoadAsync();

        _serviceProvider.GetRequiredService<GamepadService>().Start();

        var windowModeManager = _serviceProvider.GetRequiredService<WindowModeManager>();
        windowModeManager.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.GetService<GamepadService>()?.Dispose();
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}
