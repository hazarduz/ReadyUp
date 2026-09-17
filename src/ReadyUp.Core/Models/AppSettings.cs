namespace ReadyUp.Core.Models;

public sealed class AppSettings
{
    /// <summary>UI scale factor applied via LayoutTransform. Clamped to [0.75, 1.5].</summary>
    public double UiScale { get; set; } = 1.0;

    public ShellMode PreferredStartupMode { get; set; } = ShellMode.Windowed;

    /// <summary>Radial deadzone applied to gamepad thumbsticks, 0-1.</summary>
    public double GamepadDeadzone { get; set; } = 0.22;

    /// <summary>Additional folders the drive scanner should search besides the well-known install roots.</summary>
    public List<string> AdditionalLibraryFolders { get; set; } = new();

    /// <summary>Folders excluded from drive scans (e.g. system/tooling directories).</summary>
    public List<string> ExcludedFolders { get; set; } = new();

    public bool EnableRawDriveScan { get; set; } = true;
    public int RawDriveScanMaxDepth { get; set; } = 4;

    public string? SteamGridDbApiKey { get; set; }
    public string? IgdbClientId { get; set; }
    public string? IgdbClientSecret { get; set; }

    public bool EnableOnlineMetadata { get; set; } = true;

    /// <summary>Target UI frame pacing hint (e.g. matched to a 120/144/165 Hz display).</summary>
    public int TargetRefreshHz { get; set; } = 60;
}
