using System.Runtime.CompilerServices;
using ReadyUp.Core.Interfaces;
using ReadyUp.Core.Models;

namespace ReadyUp.Scanning;

/// <summary>
/// Walks known game-install roots (Steam/Epic/GOG/Battle.net/Program Files/
/// user-added library folders) treating each immediate subdirectory as one
/// candidate game, plus an optional depth-limited raw walk of whole drives
/// for anything installed outside those roots. Unchanged directories are
/// skipped via <see cref="IScanCache"/> so repeat scans are fast.
/// </summary>
public sealed class DriveGameScanner : IGameScanner
{
    private readonly IScanCache _scanCache;
    private readonly IReadOnlyList<string> _additionalRoots;
    private readonly IReadOnlyList<string> _excludedFolders;
    private readonly bool _enableRawDriveScan;
    private readonly int _rawDriveScanMaxDepth;

    public string Name => "Drive Scan";

    public DriveGameScanner(
        IScanCache scanCache,
        IReadOnlyList<string>? additionalRoots = null,
        IReadOnlyList<string>? excludedFolders = null,
        bool enableRawDriveScan = true,
        int rawDriveScanMaxDepth = 4)
    {
        _scanCache = scanCache;
        _additionalRoots = additionalRoots ?? Array.Empty<string>();
        _excludedFolders = excludedFolders ?? Array.Empty<string>();
        _enableRawDriveScan = enableRawDriveScan;
        _rawDriveScanMaxDepth = rawDriveScanMaxDepth;
    }

    public async IAsyncEnumerable<ScanResult> ScanAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        var roots = KnownInstallLocations.GetCandidateRoots().Concat(_additionalRoots).Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var root in roots)
        {
            ct.ThrowIfCancellationRequested();

            foreach (var candidate in ScanRootForGameFolders(root))
            {
                if (candidate is not null) yield return candidate;
            }
        }

        if (_enableRawDriveScan)
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (drive.DriveType != DriveType.Fixed || !drive.IsReady) continue;
                ct.ThrowIfCancellationRequested();

                foreach (var candidate in WalkDriveShallow(drive.RootDirectory.FullName, depth: 0))
                {
                    if (candidate is not null) yield return candidate;
                }
            }
        }

        await Task.CompletedTask;
    }

    private IEnumerable<ScanResult?> ScanRootForGameFolders(string root)
    {
        IEnumerable<string> subdirectories;
        try
        {
            subdirectories = Directory.EnumerateDirectories(root);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            yield break;
        }

        foreach (var directory in subdirectories)
        {
            if (IsExcluded(directory)) continue;
            yield return TryBuildResult(directory, GameSource.ScannedDrive);
        }
    }

    private IEnumerable<ScanResult?> WalkDriveShallow(string directory, int depth)
    {
        if (depth > _rawDriveScanMaxDepth) yield break;
        if (IsExcluded(directory)) yield break;
        if (LooksLikeSystemOrToolingFolder(directory)) yield break;

        var localExecutables = SafeEnumerateFiles(directory, "*.exe");
        var result = TryBuildResultFromExecutables(directory, localExecutables, GameSource.ScannedDrive);
        if (result is not null)
        {
            yield return result;
            yield break; // A directory that already resolved to a game candidate doesn't need deeper recursion.
        }

        foreach (var sub in SafeEnumerateDirectories(directory))
        {
            foreach (var nested in WalkDriveShallow(sub, depth + 1))
            {
                yield return nested;
            }
        }
    }

    private ScanResult? TryBuildResult(string directory, GameSource source)
    {
        var fingerprint = ComputeFingerprint(directory);
        if (fingerprint is null) return null;

        if (_scanCache.IsUnchanged(directory, fingerprint))
        {
            return null; // Already scanned and nothing changed since.
        }

        var executables = SafeEnumerateFiles(directory, "*.exe", SearchOption.AllDirectories);
        var result = TryBuildResultFromExecutables(directory, executables, source);

        _scanCache.Record(directory, fingerprint);
        return result;
    }

    private static ScanResult? TryBuildResultFromExecutables(string directory, IEnumerable<string> executables, GameSource source)
    {
        var best = ExecutableHeuristics.PickBestExecutable(executables, directory);
        if (best is null) return null;

        var title = TitleFromFolderName(directory);
        return new ScanResult
        {
            Title = title,
            ExecutablePath = best,
            InstallDirectory = directory,
            Source = source,
            Confidence = ExecutableHeuristics.Score(best, directory),
        };
    }

    private static string TitleFromFolderName(string directory)
        => Path.GetFileName(directory.TrimEnd(Path.DirectorySeparatorChar));

    private bool IsExcluded(string directory)
        => _excludedFolders.Any(excluded => directory.StartsWith(excluded, StringComparison.OrdinalIgnoreCase));

    private static bool LooksLikeSystemOrToolingFolder(string directory)
    {
        var name = Path.GetFileName(directory.TrimEnd(Path.DirectorySeparatorChar)).ToLowerInvariant();
        return name is "windows" or "$recycle.bin" or "system volume information" or "programdata"
            or "node_modules" or ".git" or "appdata" or "recovery" or "perflogs";
    }

    private static string? ComputeFingerprint(string directory)
    {
        try
        {
            var files = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).Take(5000).ToList();
            var latestWrite = files.Count == 0
                ? Directory.GetLastWriteTimeUtc(directory)
                : files.Max(f => File.GetLastWriteTimeUtc(f));
            return $"{files.Count}:{latestWrite:O}";
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return null;
        }
    }

    private static IEnumerable<string> SafeEnumerateFiles(string directory, string pattern, SearchOption option = SearchOption.TopDirectoryOnly)
    {
        try
        {
            return Directory.EnumerateFiles(directory, pattern, option).Take(500).ToList();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return Enumerable.Empty<string>();
        }
    }

    private static IEnumerable<string> SafeEnumerateDirectories(string directory)
    {
        try
        {
            return Directory.EnumerateDirectories(directory).ToList();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return Enumerable.Empty<string>();
        }
    }
}
