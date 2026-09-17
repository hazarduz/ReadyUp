namespace ReadyUp.Core.Models;

/// <summary>One candidate game surfaced by a scanner, before it is merged into the library.</summary>
public sealed class ScanResult
{
    public required string Title { get; init; }
    public required string ExecutablePath { get; init; }
    public required string InstallDirectory { get; init; }
    public required GameSource Source { get; init; }
    public string? Publisher { get; init; }

    /// <summary>0-1 heuristic confidence that this is really a game (vs. an uninstaller/tool/redistributable).</summary>
    public double Confidence { get; init; } = 0.5;
}
