using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.Versioning;

namespace ReadyUp.Metadata;

/// <summary>Pulls the embedded shell icon out of a game's executable, upscaled/encoded as PNG bytes.</summary>
[SupportedOSPlatform("windows")]
public static class IconExtractor
{
    public static byte[]? TryExtractPng(string executablePath)
    {
        if (!OperatingSystem.IsWindows() || !File.Exists(executablePath)) return null;

        try
        {
            using var icon = Icon.ExtractAssociatedIcon(executablePath);
            if (icon is null) return null;

            using var bitmap = icon.ToBitmap();
            using var stream = new MemoryStream();
            bitmap.Save(stream, ImageFormat.Png);
            return stream.ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }
}
