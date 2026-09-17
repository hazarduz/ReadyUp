namespace ReadyUp.Input;

/// <summary>XInput digital buttons, matching the XINPUT_GAMEPAD bitmask values.</summary>
[Flags]
public enum GamepadButton : ushort
{
    None = 0x0000,
    DPadUp = 0x0001,
    DPadDown = 0x0002,
    DPadLeft = 0x0004,
    DPadRight = 0x0008,
    Start = 0x0010,
    Back = 0x0020,
    LeftThumb = 0x0040,
    RightThumb = 0x0080,
    LeftShoulder = 0x0100,
    RightShoulder = 0x0200,
    A = 0x1000,
    B = 0x2000,
    X = 0x4000,
    Y = 0x8000,
}

/// <summary>Semantic UI-navigation commands derived from raw gamepad state, decoupled from any specific button layout.</summary>
public enum NavigationCommand
{
    Up,
    Down,
    Left,
    Right,
    Accept,
    Back,
    OpenGameOptions,
    OpenMainMenu,
    ToggleFullScreen,
}
