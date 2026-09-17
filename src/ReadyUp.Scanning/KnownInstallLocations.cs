namespace ReadyUp.Scanning;

/// <summary>Well-known roots where PC game launchers install titles, searched before falling back to a raw drive walk.</summary>
public static class KnownInstallLocations
{
    public static IEnumerable<string> GetCandidateRoots()
    {
        var roots = new List<string>();

        foreach (var drive in DriveInfo.GetDrives())
        {
            if (drive.DriveType is not (DriveType.Fixed or DriveType.Removable) || !drive.IsReady) continue;

            var root = drive.RootDirectory.FullName;
            TryAdd(roots, Path.Combine(root, "Program Files"));
            TryAdd(roots, Path.Combine(root, "Program Files (x86)"));
            TryAdd(roots, Path.Combine(root, "Games"));

            // Steam library folders (default + common secondary-drive convention).
            TryAdd(roots, Path.Combine(root, "SteamLibrary", "steamapps", "common"));
            TryAdd(roots, Path.Combine(root, "Program Files (x86)", "Steam", "steamapps", "common"));

            // Epic Games Store.
            TryAdd(roots, Path.Combine(root, "Program Files", "Epic Games"));

            // GOG Galaxy default.
            TryAdd(roots, Path.Combine(root, "GOG Games"));

            // Battle.net / Blizzard default.
            TryAdd(roots, Path.Combine(root, "Program Files (x86)", "Battle.net"));

            // Xbox / Microsoft Store apps (WindowsApps requires elevation to enumerate; scanner should catch UnauthorizedAccessException).
            TryAdd(roots, Path.Combine(root, "XboxGames"));
        }

        return roots;
    }

    private static void TryAdd(List<string> list, string path)
    {
        if (Directory.Exists(path) && !list.Contains(path, StringComparer.OrdinalIgnoreCase))
        {
            list.Add(path);
        }
    }
}
