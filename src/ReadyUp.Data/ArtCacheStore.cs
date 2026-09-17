using ReadyUp.Core.Interfaces;
using ReadyUp.Core.Models;

namespace ReadyUp.Data;

/// <summary>
/// Manages %LOCALAPPDATA%\ReadyUp\ArtCache\{gameId}\{type}.png. Files are
/// always written under a fixed name per type so bound Image controls can
/// keep a stable Uri while the underlying bytes change (paired with
/// BitmapCacheOption.OnLoad on the WPF side to avoid a stale file handle).
/// </summary>
public sealed class ArtCacheStore : IArtCacheStore
{
    public string GetPath(Guid gameId, ArtAssetType type)
        => Path.Combine(AppPaths.ArtCacheRoot, gameId.ToString(), $"{type.ToString().ToLowerInvariant()}.png");

    public async Task<string> SaveFromFileAsync(Guid gameId, ArtAssetType type, string sourceFilePath, CancellationToken ct = default)
    {
        var destination = PrepareDestination(gameId, type);
        await using var source = File.OpenRead(sourceFilePath);
        await using var target = File.Create(destination);
        await source.CopyToAsync(target, ct).ConfigureAwait(false);
        return destination;
    }

    public async Task<string> SaveFromStreamAsync(Guid gameId, ArtAssetType type, Stream source, CancellationToken ct = default)
    {
        var destination = PrepareDestination(gameId, type);
        await using var target = File.Create(destination);
        await source.CopyToAsync(target, ct).ConfigureAwait(false);
        return destination;
    }

    public async Task<string> SaveFromBytesAsync(Guid gameId, ArtAssetType type, byte[] bytes, CancellationToken ct = default)
    {
        var destination = PrepareDestination(gameId, type);
        await File.WriteAllBytesAsync(destination, bytes, ct).ConfigureAwait(false);
        return destination;
    }

    public void DeleteAllFor(Guid gameId)
    {
        var directory = Path.Combine(AppPaths.ArtCacheRoot, gameId.ToString());
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private string PrepareDestination(Guid gameId, ArtAssetType type)
    {
        var destination = GetPath(gameId, type);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        return destination;
    }
}
