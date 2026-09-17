namespace ReadyUp.Scanning;

/// <summary>
/// Scores candidate executables on how likely they are to be the "main"
/// launch target of a game, versus an uninstaller, redistributable
/// installer, launcher stub, or crash-reporter helper that happens to sit
/// in the same install folder.
/// </summary>
public static class ExecutableHeuristics
{
    private static readonly string[] ExcludedNameFragments =
    {
        "unins", "uninstall", "setup", "redist", "vcredist", "directx", "dxsetup",
        "crashreporter", "crashpad", "crashhandler", "helper", "updater", "update",
        "launcher_helper", "cleanup", "repair", "activation", "dotnetfx", "vc_redist",
        "battleye", "easyanticheat", "eac_", "steamerrorreporter", "unitycrashhandler",
    };

    private static readonly string[] ExcludedDirectoryFragments =
    {
        "_commonredist", "redist", "directx", "vcredist", "__installer", "battleye",
        "easyanticheat",
    };

    private static readonly string[] PreferredNameFragments = { "game", "launch", "play" };

    /// <summary>Returns a 0-1 confidence score; callers typically drop anything below ~0.15.</summary>
    public static double Score(string executablePath, string installRoot)
    {
        var fileName = Path.GetFileNameWithoutExtension(executablePath).ToLowerInvariant();
        var relativeDir = Path.GetDirectoryName(executablePath)?.ToLowerInvariant() ?? string.Empty;
        var fileSize = SafeFileSize(executablePath);

        foreach (var fragment in ExcludedNameFragments)
        {
            if (fileName.Contains(fragment)) return 0.0;
        }

        foreach (var fragment in ExcludedDirectoryFragments)
        {
            if (relativeDir.Contains(fragment)) return 0.0;
        }

        double score = 0.4;

        // Tiny executables are usually bootstrappers/launchers, not the real game binary.
        if (fileSize is > 0 and < 200_000) score -= 0.15;
        if (fileSize > 20_000_000) score += 0.15;

        // An exe named after its own folder is a strong "this is the game" signal.
        var folderName = Path.GetFileName(Path.GetDirectoryName(executablePath) ?? string.Empty).ToLowerInvariant();
        if (!string.IsNullOrEmpty(folderName) && (fileName.Contains(folderName) || folderName.Contains(fileName)))
        {
            score += 0.25;
        }

        // Executable sitting directly in the install root (not a nested "bin"/"redist" folder) is favored.
        if (string.Equals(Path.GetFullPath(relativeDir).TrimEnd(Path.DirectorySeparatorChar),
                           Path.GetFullPath(installRoot).TrimEnd(Path.DirectorySeparatorChar),
                           StringComparison.OrdinalIgnoreCase))
        {
            score += 0.15;
        }

        foreach (var fragment in PreferredNameFragments)
        {
            if (fileName.Contains(fragment)) { score += 0.1; break; }
        }

        return Math.Clamp(score, 0.0, 1.0);
    }

    /// <summary>Picks the highest-scoring executable under <paramref name="installRoot"/>, or null if none qualify.</summary>
    public static string? PickBestExecutable(IEnumerable<string> candidateExecutables, string installRoot, double minimumConfidence = 0.15)
    {
        string? best = null;
        double bestScore = minimumConfidence;

        foreach (var candidate in candidateExecutables)
        {
            var score = Score(candidate, installRoot);
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return best;
    }

    private static long SafeFileSize(string path)
    {
        try
        {
            return new FileInfo(path).Length;
        }
        catch
        {
            return -1;
        }
    }
}
