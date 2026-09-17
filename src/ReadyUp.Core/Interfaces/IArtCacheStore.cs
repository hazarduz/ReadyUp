using ReadyUp.Core.Models;

namespace ReadyUp.Core.Interfaces;

/// <summary>
/// Manages the on-disk art cache (%LOCALAPPDATA%\ReadyUp\ArtCache\{gameId}\...).
/// Implemented by ReadyUp.Data.
/// </summary>
public interface IArtCacheStore
{
    /// <summary>Full path a given asset type would be written to for a game (does not guarantee the file exists).</summary>
    string GetPath(Guid gameId, ArtAssetType type);

    Task<string> SaveFromFileAsync(Guid gameId, ArtAssetType type, string sourceFilePath, CancellationToken ct = default);
    Task<string> SaveFromStreamAsync(Guid gameId, ArtAssetType type, Stream source, CancellationToken ct = default);
    Task<string> SaveFromBytesAsync(Guid gameId, ArtAssetType type, byte[] bytes, CancellationToken ct = default);

    void DeleteAllFor(Guid gameId);
}

/// <summary>
/// Tracks which install directories have already been scanned (and their last-modified
/// fingerprint) so re-scans can skip unchanged folders. Implemented by ReadyUp.Data.
/// </summary>
public interface IScanCache
{
    Task LoadAsync(CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);

    /// <summary>True if <paramref name="directoryPath"/> was already scanned and its fingerprint is unchanged.</summary>
    bool IsUnchanged(string directoryPath, string fingerprint);

    void Record(string directoryPath, string fingerprint);
}
