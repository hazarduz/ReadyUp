namespace ReadyUp.Data;

/// <summary>Centralizes every on-disk location ReadyUp reads/writes under %LOCALAPPDATA%.</summary>
public static class AppPaths
{
    public static string RootDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ReadyUp");

    public static string DatabasePath { get; } = Path.Combine(RootDirectory, "library.db");
    public static string SettingsPath { get; } = Path.Combine(RootDirectory, "settings.json");
    public static string ScanCachePath { get; } = Path.Combine(RootDirectory, "scan-cache.json");
    public static string ArtCacheRoot { get; } = Path.Combine(RootDirectory, "ArtCache");

    public static void EnsureCreated()
    {
        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(ArtCacheRoot);
    }
}
