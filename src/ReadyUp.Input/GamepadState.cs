namespace ReadyUp.Input;

/// <summary>Immutable snapshot of one controller's state for a single poll, after deadzone processing.</summary>
public readonly record struct GamepadState(
    int PlayerIndex,
    bool IsConnected,
    GamepadButton Buttons,
    float LeftThumbX,
    float LeftThumbY,
    float RightThumbX,
    float RightThumbY,
    float LeftTrigger,
    float RightTrigger)
{
    public static GamepadState Disconnected(int playerIndex) => new(playerIndex, false, GamepadButton.None, 0, 0, 0, 0, 0, 0);

    public bool IsPressed(GamepadButton button) => (Buttons & button) == button;
}

/// <summary>Raised when a button transitions from up to down, or a stick crosses into/out of the navigation deadzone.</summary>
public sealed class GamepadButtonEventArgs : EventArgs
{
    public required int PlayerIndex { get; init; }
    public required GamepadButton Button { get; init; }
}

public sealed class NavigationCommandEventArgs : EventArgs
{
    public required int PlayerIndex { get; init; }
    public required NavigationCommand Command { get; init; }
}
