using System.Windows;
using System.Windows.Threading;
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

        // Without these, a startup failure (bad DI wiring, a missing native
        // dependency, a locked settings file, ...) kills the process before
        // any window ever appears, with nothing visible to the user at all.
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;

        try
        {
            _serviceProvider = AppHost.BuildServiceProvider();

            var settingsService = _serviceProvider.GetRequiredService<ISettingsService>();
            await settingsService.LoadAsync();

            _serviceProvider.GetRequiredService<GamepadService>().Start();

            var windowModeManager = _serviceProvider.GetRequiredService<WindowModeManager>();
            windowModeManager.Start();
        }
        catch (Exception ex)
        {
            ShowFatalErrorAndShutdown(ex);
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        ShowFatalErrorAndShutdown(e.Exception);
    }

    private void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        // The CLR is already terminating the process at this point (this fires for
        // exceptions on background threads, e.g. the gamepad polling timer); a
        // synchronous MessageBox is the only way left to make the failure visible.
        if (e.ExceptionObject is Exception ex)
        {
            MessageBox.Show(
                $"ReadyUp hit an unrecoverable error and needs to close:\n\n{ex}",
                "ReadyUp - Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ShowFatalErrorAndShutdown(Exception ex)
    {
        MessageBox.Show(
            $"ReadyUp failed to start:\n\n{ex}",
            "ReadyUp - Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
        Shutdown(-1);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.GetService<GamepadService>()?.Dispose();
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}
