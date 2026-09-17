using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using ReadyUp.Input;

namespace ReadyUp.App.Behaviors;

/// <summary>
/// Bridges <see cref="GamepadNavigationManager"/> (which raises semantic
/// commands on a background thread) into WPF's existing directional-focus
/// system, so gamepad, keyboard, and mouse all converge on the same focus
/// model — a view only ever has to opt into the framework's normal
/// KeyboardNavigation, plus handle <see cref="BackRequested"/>,
/// <see cref="GameOptionsRequested"/>, and <see cref="MainMenuRequested"/>.
/// One instance is attached per top-level window (MainWindow / FullScreenWindow).
/// </summary>
public sealed class GamepadFocusBehavior : IDisposable
{
    private readonly Window _window;
    private readonly GamepadNavigationManager _navigationManager;
    private readonly Dispatcher _dispatcher;

    /// <summary>Raised when Back (B) is pressed while this window is active; the view decides what "back" means (close an overlay, exit the app, ...).</summary>
    public event EventHandler? BackRequested;

    /// <summary>Raised when Y is held on the currently focused game tile.</summary>
    public event EventHandler<object?>? GameOptionsRequested;

    /// <summary>Raised when Start is pressed.</summary>
    public event EventHandler? MainMenuRequested;

    public GamepadFocusBehavior(Window window, GamepadNavigationManager navigationManager)
    {
        _window = window;
        _navigationManager = navigationManager;
        _dispatcher = window.Dispatcher;
        _navigationManager.CommandIssued += OnCommandIssued;
    }

    private void OnCommandIssued(object? sender, NavigationCommandEventArgs e)
    {
        if (!_window.IsVisible || !_window.IsActive) return;

        _dispatcher.BeginInvoke(() => Handle(e.Command));
    }

    private void Handle(NavigationCommand command)
    {
        switch (command)
        {
            case NavigationCommand.Up: MoveFocus(FocusNavigationDirection.Up); break;
            case NavigationCommand.Down: MoveFocus(FocusNavigationDirection.Down); break;
            case NavigationCommand.Left: MoveFocus(FocusNavigationDirection.Left); break;
            case NavigationCommand.Right: MoveFocus(FocusNavigationDirection.Right); break;
            case NavigationCommand.Accept: ActivateFocusedElement(); break;
            case NavigationCommand.Back: BackRequested?.Invoke(this, EventArgs.Empty); break;
            case NavigationCommand.OpenGameOptions:
                GameOptionsRequested?.Invoke(this, (Keyboard.FocusedElement as FrameworkElement)?.DataContext);
                break;
            case NavigationCommand.OpenMainMenu: MainMenuRequested?.Invoke(this, EventArgs.Empty); break;
        }
    }

    private void MoveFocus(FocusNavigationDirection direction)
    {
        if (Keyboard.FocusedElement is UIElement focused)
        {
            focused.MoveFocus(new TraversalRequest(direction));
        }
        else
        {
            _window.MoveFocus(new TraversalRequest(direction));
        }
    }

    private static void ActivateFocusedElement()
    {
        // GameTileControl and dialog action buttons are all concrete Buttons, so
        // ButtonAutomationPeer's Invoke pattern is the correct, accessibility-standard
        // way to "click" whatever currently has keyboard focus without a mouse.
        if (Keyboard.FocusedElement is Button button)
        {
            var peer = new ButtonAutomationPeer(button);
            if (peer.GetPattern(PatternInterface.Invoke) is IInvokeProvider invokeProvider)
            {
                invokeProvider.Invoke();
            }
        }
    }

    public void Dispose() => _navigationManager.CommandIssued -= OnCommandIssued;

    /// <summary>
    /// Convenience for modal dialogs (Change Art, Edit Metadata, Settings):
    /// attaches directional/Accept navigation and maps Back (B) to closing
    /// the dialog, self-disposing when the window closes.
    /// </summary>
    public static GamepadFocusBehavior AttachToDialog(Window dialog, GamepadNavigationManager navigationManager)
    {
        var behavior = new GamepadFocusBehavior(dialog, navigationManager);
        behavior.BackRequested += (_, _) => dialog.Close();
        dialog.Closed += (_, _) => behavior.Dispose();
        return behavior;
    }
}
