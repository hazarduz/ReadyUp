using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using Microsoft.Win32;
using ReadyUp.Core.Interfaces;
using ReadyUp.Core.Models;

namespace ReadyUp.Scanning;

/// <summary>
/// Reads the Windows "Add/Remove Programs" registry entries
/// (HKLM/HKCU \SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall, both
/// 32- and 64-bit views) and treats non-system entries with a resolvable
/// install location as game candidates.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class RegistryInstalledAppsScanner : IGameScanner
{
    private static readonly string[] SystemComponentPublishers =
    {
        "Microsoft Corporation", "Microsoft", "Adobe Systems Incorporated", "Adobe Inc.",
        "Oracle Corporation", "Google LLC", "Google Inc.", "Mozilla",
    };

    public string Name => "Registry (Installed Programs)";

    public async IAsyncEnumerable<ScanResult> ScanAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            yield break;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (hive, view, subKeyPath) in EnumerateUninstallRoots())
        {
            ct.ThrowIfCancellationRequested();

            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var uninstallKey = baseKey.OpenSubKey(subKeyPath);
            if (uninstallKey is null) continue;

            foreach (var subKeyName in uninstallKey.GetSubKeyNames())
            {
                ct.ThrowIfCancellationRequested();

                ScanResult? result = null;
                try
                {
                    using var entry = uninstallKey.OpenSubKey(subKeyName);
                    result = TryBuildResult(entry);
                }
                catch (Exception) when (ct.IsCancellationRequested is false)
                {
                    // Skip unreadable/malformed entries; a single bad registry key shouldn't abort the whole scan.
                }

                if (result is null) continue;
                if (!seen.Add(result.ExecutablePath)) continue;

                yield return result;
            }
        }

        await Task.CompletedTask;
    }

    private static IEnumerable<(RegistryHive Hive, RegistryView View, string SubKeyPath)> EnumerateUninstallRoots()
    {
        const string path32 = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
        const string pathWow64 = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall";

        yield return (RegistryHive.LocalMachine, RegistryView.Registry64, path32);
        yield return (RegistryHive.LocalMachine, RegistryView.Registry32, pathWow64);
        yield return (RegistryHive.CurrentUser, RegistryView.Registry64, path32);
    }

    private static ScanResult? TryBuildResult(RegistryKey? entry)
    {
        if (entry is null) return null;

        if (entry.GetValue("SystemComponent") is int and 1) return null;

        var displayName = entry.GetValue("DisplayName") as string;
        if (string.IsNullOrWhiteSpace(displayName)) return null;

        var publisher = entry.GetValue("Publisher") as string;
        if (publisher is not null && SystemComponentPublishers.Contains(publisher, StringComparer.OrdinalIgnoreCase))
        {
            return null;
        }

        var installLocation = entry.GetValue("InstallLocation") as string;
        var displayIcon = entry.GetValue("DisplayIcon") as string;

        var installDirectory = !string.IsNullOrWhiteSpace(installLocation) && Directory.Exists(installLocation)
            ? installLocation
            : null;

        var executablePath = ResolveExecutable(displayIcon, installDirectory);
        if (executablePath is null) return null;

        installDirectory ??= Path.GetDirectoryName(executablePath) ?? executablePath;

        var candidates = SafeEnumerateExecutables(installDirectory);
        var best = ExecutableHeuristics.PickBestExecutable(candidates.Append(executablePath), installDirectory)
                   ?? executablePath;

        var confidence = ExecutableHeuristics.Score(best, installDirectory);
        if (confidence <= 0.0) return null;

        return new ScanResult
        {
            Title = displayName!,
            ExecutablePath = best,
            InstallDirectory = installDirectory,
            Source = GameSource.ScannedRegistry,
            Publisher = publisher,
            Confidence = Math.Max(confidence, 0.5), // registry-sourced entries start with a trust floor.
        };
    }

    private static string? ResolveExecutable(string? displayIcon, string? installDirectory)
    {
        if (!string.IsNullOrWhiteSpace(displayIcon))
        {
            var iconPath = displayIcon.Split(',')[0].Trim('"');
            if (iconPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(iconPath))
            {
                return iconPath;
            }
        }

        if (!string.IsNullOrWhiteSpace(installDirectory) && Directory.Exists(installDirectory))
        {
            return SafeEnumerateExecutables(installDirectory).FirstOrDefault();
        }

        return null;
    }

    private static IEnumerable<string> SafeEnumerateExecutables(string directory)
    {
        try
        {
            return Directory.EnumerateFiles(directory, "*.exe", SearchOption.AllDirectories).Take(200);
        }
        catch (UnauthorizedAccessException)
        {
            return Enumerable.Empty<string>();
        }
        catch (IOException)
        {
            return Enumerable.Empty<string>();
        }
    }
}
