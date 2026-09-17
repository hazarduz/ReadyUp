using System.Runtime.Versioning;
using System.Threading;

namespace ReadyUp.Input;

/// <summary>
/// Translates raw <see cref="GamepadService"/> state into semantic
/// <see cref="NavigationCommand"/>s: D-pad/left-stick → directional
/// navigation (with keyboard-style initial-delay + repeat-rate while held,
/// so holding a direction scrolls the grid smoothly instead of needing
/// repeated taps), A → Accept, B → Back, Y → OpenGameOptions,
/// Start → OpenMainMenu. This is the single place gamepad button meaning
/// is defined; WPF views only ever see <see cref="NavigationCommand"/>.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class GamepadNavigationManager : IDisposable
{
    private static readonly TimeSpan InitialRepeatDelay = TimeSpan.FromMilliseconds(400);
    private static readonly TimeSpan RepeatInterval = TimeSpan.FromMilliseconds(140);
    private const float StickActivationThreshold = 0.5f;

    private readonly GamepadService _gamepadService;
    private readonly Dictionary<int, DirectionRepeatState> _repeatStateByPlayer = new();
    private readonly object _syncRoot = new();
    private readonly Timer _repeatTimer;

    public event EventHandler<NavigationCommandEventArgs>? CommandIssued;

    public GamepadNavigationManager(GamepadService gamepadService)
    {
        _gamepadService = gamepadService;
        _gamepadService.StateChanged += OnStateChanged;
        _gamepadService.ButtonPressed += OnButtonPressed;

        _repeatTimer = new Timer(_ => TickRepeats(), null, 0, 16);
    }

    private void OnStateChanged(object? sender, GamepadState state)
    {
        NavigationCommand? toFireImmediately = null;

        lock (_syncRoot)
        {
            var direction = ResolveDirection(state);

            if (!_repeatStateByPlayer.TryGetValue(state.PlayerIndex, out var repeatState))
            {
                repeatState = new DirectionRepeatState();
                _repeatStateByPlayer[state.PlayerIndex] = repeatState;
            }

            if (direction == repeatState.CurrentDirection) return;

            repeatState.CurrentDirection = direction;

            if (direction is { } newDirection)
            {
                toFireImmediately = newDirection;
                repeatState.NextFireUtc = DateTime.UtcNow + InitialRepeatDelay;
            }
            else
            {
                repeatState.NextFireUtc = null;
            }
        }

        if (toFireImmediately is { } command)
        {
            Raise(state.PlayerIndex, command);
        }
    }

    private void TickRepeats()
    {
        List<(int PlayerIndex, NavigationCommand Command)>? toFire = null;
        var now = DateTime.UtcNow;

        lock (_syncRoot)
        {
            foreach (var (playerIndex, repeatState) in _repeatStateByPlayer)
            {
                if (repeatState.CurrentDirection is not { } direction || repeatState.NextFireUtc is not { } nextFire) continue;
                if (now < nextFire) continue;

                (toFire ??= new()).Add((playerIndex, direction));
                repeatState.NextFireUtc = now + RepeatInterval;
            }
        }

        if (toFire is null) return;
        foreach (var (playerIndex, command) in toFire)
        {
            Raise(playerIndex, command);
        }
    }

    private void OnButtonPressed(object? sender, GamepadButtonEventArgs e)
    {
        var command = e.Button switch
        {
            GamepadButton.A => NavigationCommand.Accept,
            GamepadButton.B => NavigationCommand.Back,
            GamepadButton.Y => NavigationCommand.OpenGameOptions,
            GamepadButton.Start => NavigationCommand.OpenMainMenu,
            _ => (NavigationCommand?)null,
        };

        if (command is { } resolved)
        {
            Raise(e.PlayerIndex, resolved);
        }
    }

    private static NavigationCommand? ResolveDirection(GamepadState state)
    {
        if (state.IsPressed(GamepadButton.DPadUp)) return NavigationCommand.Up;
        if (state.IsPressed(GamepadButton.DPadDown)) return NavigationCommand.Down;
        if (state.IsPressed(GamepadButton.DPadLeft)) return NavigationCommand.Left;
        if (state.IsPressed(GamepadButton.DPadRight)) return NavigationCommand.Right;

        if (state.LeftThumbY > StickActivationThreshold) return NavigationCommand.Up;
        if (state.LeftThumbY < -StickActivationThreshold) return NavigationCommand.Down;
        if (state.LeftThumbX < -StickActivationThreshold) return NavigationCommand.Left;
        if (state.LeftThumbX > StickActivationThreshold) return NavigationCommand.Right;

        return null;
    }

    private void Raise(int playerIndex, NavigationCommand command)
        => CommandIssued?.Invoke(this, new NavigationCommandEventArgs { PlayerIndex = playerIndex, Command = command });

    public void Dispose()
    {
        _gamepadService.StateChanged -= OnStateChanged;
        _gamepadService.ButtonPressed -= OnButtonPressed;
        _repeatTimer.Dispose();
    }

    private sealed class DirectionRepeatState
    {
        public NavigationCommand? CurrentDirection;
        public DateTime? NextFireUtc;
    }
}
