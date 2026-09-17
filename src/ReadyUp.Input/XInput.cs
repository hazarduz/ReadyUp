using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace ReadyUp.Input;

/// <summary>
/// Minimal raw P/Invoke wrapper over xinput1_4.dll. Deliberately hand-rolled
/// instead of pulling in a third-party gamepad package: XInput alone covers
/// the overwhelming majority of PC controllers (native Xbox controllers,
/// and third-party/Bluetooth pads through their XInput-emulation modes).
/// </summary>
[SupportedOSPlatform("windows")]
internal static class XInput
{
    private const string DllName = "xinput1_4.dll";
    public const int ErrorSuccess = 0;
    public const int MaxControllers = 4;

    [StructLayout(LayoutKind.Sequential)]
    public struct XInputGamepad
    {
        public ushort wButtons;
        public byte bLeftTrigger;
        public byte bRightTrigger;
        public short sThumbLX;
        public short sThumbLY;
        public short sThumbRX;
        public short sThumbRY;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XInputState
    {
        public uint dwPacketNumber;
        public XInputGamepad Gamepad;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XInputVibration
    {
        public ushort wLeftMotorSpeed;
        public ushort wRightMotorSpeed;
    }

    [DllImport(DllName, EntryPoint = "XInputGetState")]
    public static extern int XInputGetState(int dwUserIndex, out XInputState pState);

    [DllImport(DllName, EntryPoint = "XInputSetState")]
    public static extern int XInputSetState(int dwUserIndex, ref XInputVibration pVibration);
}
