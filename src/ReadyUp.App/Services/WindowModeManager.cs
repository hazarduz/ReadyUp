using System.Windows;
using System.Windows.Media.Animation;
using Microsoft.Extensions.DependencyInjection;
using ReadyUp.App.Views;
using ReadyUp.Core.Interfaces;
using ReadyUp.Core.Models;

namespace ReadyUp.App.Services;

/// <summary>
/// Owns the single <see cref="MainWindow"/> (windowed) and
/// <see cref="FullScreenWindow"/> (Big Picture) instances and switches
/// between them. The window not currently shown is hidden rather than
/// closed, so its visual tree/animations/scroll position survive repeated
/// toggling. Both windows are bound to the same LibraryViewModel instance.
///
/// The windows are resolved lazily from <see cref="IServiceProvider"/> (not
/// constructor-injected) specifically to avoid a circular dependency: both
/// MainWindow and FullScreenWindow take this manager as a constructor
/// dependency so they can trigger mode switches, and the DI container
/// cannot construct two singletons that each require the other. Resolving
/// them on first use instead means this manager itself has no dependency
/// on either window, breaking the cycle.
/// </summary>
public sealed class WindowModeManager
{
    private static readonly TimeSpan FadeDuration = TimeSpan.FromMilliseconds(150);

    private readonly IServiceProvider _services;
    private readonly ISettingsService _settingsService;
    private MainWindow? _mainWindow;
    private FullScreenWindow? _fullScreenWindow;

    public ShellMode CurrentMode { get; private set; } = ShellMode.Windowed;

    public event EventHandler<ShellMode>? ModeChanged;

    public WindowModeManager(IServiceProvider services, ISettingsService settingsService)
    {
        _services = services;
        _settingsService = settingsService;
    }

    private MainWindow MainWindowInstance => _mainWindow ??= _services.GetRequiredService<MainWindow>();
    private FullScreenWindow FullScreenWindowInstance => _fullScreenWindow ??= _services.GetRequiredService<FullScreenWindow>();

    /// <summary>Shows the preferred startup window. Call once from App.OnStartup.</summary>
    public void Start()
    {
        if (_settingsService.Current.PreferredStartupMode == ShellMode.FullScreen)
        {
            ShowFullScreen();
        }
        else
        {
            ShowWindowed();
        }
    }

    public void Toggle()
    {
        if (CurrentMode == ShellMode.Windowed) ShowFullScreen();
        else ShowWindowed();
    }

    public void ShowWindowed()
    {
        ConfigureWindowed(MainWindowInstance);
        TransitionTo(MainWindowInstance, FullScreenWindowInstance);
        CurrentMode = ShellMode.Windowed;
        ModeChanged?.Invoke(this, CurrentMode);
    }

    public void ShowFullScreen()
    {
        ConfigureFullScreen(FullScreenWindowInstance);
        TransitionTo(FullScreenWindowInstance, MainWindowInstance);
        CurrentMode = ShellMode.FullScreen;
        ModeChanged?.Invoke(this, CurrentMode);
    }

    private static void ConfigureWindowed(Window window)
    {
        window.WindowStyle = WindowStyle.SingleBorderWindow;
        window.ResizeMode = ResizeMode.CanResize;
        window.WindowState = WindowState.Normal;
        window.Topmost = false;
    }

    private static void ConfigureFullScreen(Window window)
    {
        // WindowStyle=None + WindowState=Maximized automatically fills whichever
        // monitor the window currently lives on (including per-monitor DPI/work
        // area), so no manual monitor-bounds math is needed.
        window.WindowStyle = WindowStyle.None;
        window.ResizeMode = ResizeMode.NoResize;
        window.Topmost = true;
        window.WindowState = WindowState.Maximized;
    }

    private static void TransitionTo(Window incoming, Window outgoing)
    {
        incoming.Opacity = 0;
        incoming.Show();
        incoming.Activate();
        AnimateOpacity(incoming, 0, 1, () => { });

        if (outgoing.IsVisible)
        {
            AnimateOpacity(outgoing, 1, 0, () =>
            {
                outgoing.Hide();
                outgoing.Opacity = 1;
            });
        }
    }

    private static void AnimateOpacity(Window window, double from, double to, Action onCompleted)
    {
        var animation = new DoubleAnimation(from, to, FadeDuration);
        animation.Completed += (_, _) => onCompleted();
        window.BeginAnimation(UIElement.OpacityProperty, animation);
    }
}
