using ReadyUp.Core.Models;

namespace ReadyUp.Core.Interfaces;

/// <summary>Supplies text metadata (title/description/publisher/...) for a game.</summary>
public interface IMetadataProvider
{
    string Name { get; }

    /// <summary>True if this provider can currently run (e.g. has a configured API key / is reachable).</summary>
    bool IsAvailable { get; }

    Task<MetadataRecord?> FetchAsync(Game game, CancellationToken ct = default);
}

/// <summary>Supplies candidate art images (icon/banner/box art/background) for a game.</summary>
public interface IArtProvider
{
    string Name { get; }
    bool IsAvailable { get; }

    Task<IReadOnlyList<ArtCandidate>> SearchAsync(Game game, ArtAssetType type, CancellationToken ct = default);

    /// <summary>Downloads/copies a chosen candidate into <paramref name="destinationPath"/>.</summary>
    Task ApplyAsync(ArtCandidate candidate, string destinationPath, CancellationToken ct = default);
}
