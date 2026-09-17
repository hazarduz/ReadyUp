using System.Runtime.Versioning;
using System.Threading;

namespace ReadyUp.Input;

/// <summary>
/// Polls up to 4 XInput controller slots on a background timer and raises
/// edge-triggered button events plus deadzone-filtered stick state. The
/// ~8 ms tick is chosen so input never becomes the bottleneck on
/// high-refresh-rate (120 Hz+) displays; all events fire on a background
/// thread pool thread, so consumers (WPF behaviors/view models) are
/// responsible for marshalling to the UI dispatcher.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class GamepadService : IDisposable
{
    private const int PollIntervalMs = 8;

    private readonly Timer _timer;
    private readonly GamepadState[] _lastStates = new GamepadState[XInput.MaxControllers];
    private readonly float _deadzone;
    private bool _disposed;

    /// <summary>Raised once per button that transitions from released to pressed.</summary>
    public event EventHandler<GamepadButtonEventArgs>? ButtonPressed;

    /// <summary>Raised once per button that transitions from pressed to released.</summary>
    public event EventHandler<GamepadButtonEventArgs>? ButtonReleased;

    /// <summary>Raised on every poll where at least one connected controller's state changed.</summary>
    public event EventHandler<GamepadState>? StateChanged;

    public GamepadService(float deadzone = 0.22f)
    {
        _deadzone = Math.Clamp(deadzone, 0f, 0.9f);
        for (var i = 0; i < _lastStates.Length; i++)
        {
            _lastStates[i] = GamepadState.Disconnected(i);
        }

        _timer = new Timer(_ => Poll(), null, Timeout.Infinite, Timeout.Infinite);
    }

    public void Start() => _timer.Change(0, PollIntervalMs);

    public void Stop() => _timer.Change(Timeout.Infinite, Timeout.Infinite);

    public GamepadState GetState(int playerIndex) => _lastStates[playerIndex];

    /// <summary>Fires a short vibration pulse, e.g. to acknowledge a focus move hitting the grid edge.</summary>
    public void Vibrate(int playerIndex, float leftMotor, float rightMotor)
    {
        if (!OperatingSystem.IsWindows() || playerIndex is < 0 or >= XInput.MaxControllers) return;

        var vibration = new XInput.XInputVibration
        {
            wLeftMotorSpeed = (ushort)(Math.Clamp(leftMotor, 0f, 1f) * ushort.MaxValue),
            wRightMotorSpeed = (ushort)(Math.Clamp(rightMotor, 0f, 1f) * ushort.MaxValue),
        };
        XInput.XInputSetState(playerIndex, ref vibration);
    }

    private void Poll()
    {
        if (!OperatingSystem.IsWindows()) return;

        for (var i = 0; i < XInput.MaxControllers; i++)
        {
            var result = XInput.XInputGetState(i, out var raw);
            var newState = result == XInput.ErrorSuccess
                ? BuildState(i, raw)
                : GamepadState.Disconnected(i);

            var previous = _lastStates[i];
            _lastStates[i] = newState;

            if (!newState.IsConnected && !previous.IsConnected) continue;

            if (newState != previous)
            {
                StateChanged?.Invoke(this, newState);
            }

            RaiseButtonEdgeEvents(previous, newState);
        }
    }

    private void RaiseButtonEdgeEvents(GamepadState previous, GamepadState current)
    {
        foreach (GamepadButton button in Enum.GetValues<GamepadButton>())
        {
            if (button == GamepadButton.None) continue;

            var wasPressed = previous.IsPressed(button);
            var isPressed = current.IsPressed(button);

            if (isPressed && !wasPressed)
            {
                ButtonPressed?.Invoke(this, new GamepadButtonEventArgs { PlayerIndex = current.PlayerIndex, Button = button });
            }
            else if (!isPressed && wasPressed)
            {
                ButtonReleased?.Invoke(this, new GamepadButtonEventArgs { PlayerIndex = current.PlayerIndex, Button = button });
            }
        }
    }

    private GamepadState BuildState(int playerIndex, XInput.XInputState raw)
    {
        var pad = raw.Gamepad;
        return new GamepadState(
            PlayerIndex: playerIndex,
            IsConnected: true,
            Buttons: (GamepadButton)pad.wButtons,
            LeftThumbX: ApplyDeadzone(pad.sThumbLX),
            LeftThumbY: ApplyDeadzone(pad.sThumbLY),
            RightThumbX: ApplyDeadzone(pad.sThumbRX),
            RightThumbY: ApplyDeadzone(pad.sThumbRY),
            LeftTrigger: pad.bLeftTrigger / 255f,
            RightTrigger: pad.bRightTrigger / 255f);
    }

    private float ApplyDeadzone(short rawAxis)
    {
        var normalized = rawAxis / (rawAxis < 0 ? 32768f : 32767f);
        return Math.Abs(normalized) < _deadzone ? 0f : normalized;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Dispose();
    }
}
