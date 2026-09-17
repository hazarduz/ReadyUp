using ReadyUp.Core.Interfaces;
using ReadyUp.Core.Models;

namespace ReadyUp.Scanning;

/// <summary>Lets the user pick an .exe directly, or a folder to be heuristically resolved to one, bypassing scan confidence thresholds.</summary>
public sealed class ManualGameAdder : IManualGameAdder
{
    public ScanResult FromExecutable(string executablePath)
    {
        if (!File.Exists(executablePath))
            throw new FileNotFoundException("Selected executable does not exist.", executablePath);

        var installDirectory = Path.GetDirectoryName(executablePath) ?? executablePath;
        return new ScanResult
        {
            Title = Path.GetFileNameWithoutExtension(executablePath),
            ExecutablePath = executablePath,
            InstallDirectory = installDirectory,
            Source = GameSource.Manual,
            Confidence = 1.0, // user-confirmed, bypass heuristic filtering entirely.
        };
    }

    public ScanResult? FromDirectory(string directoryPath)
    {
        if (!Directory.Exists(directoryPath)) return null;

        var executables = Directory.EnumerateFiles(directoryPath, "*.exe", SearchOption.AllDirectories).Take(500);
        var best = ExecutableHeuristics.PickBestExecutable(executables, directoryPath, minimumConfidence: 0.0);
        if (best is null) return null;

        return new ScanResult
        {
            Title = Path.GetFileName(directoryPath.TrimEnd(Path.DirectorySeparatorChar)),
            ExecutablePath = best,
            InstallDirectory = directoryPath,
            Source = GameSource.Manual,
            Confidence = 1.0,
        };
    }
}
