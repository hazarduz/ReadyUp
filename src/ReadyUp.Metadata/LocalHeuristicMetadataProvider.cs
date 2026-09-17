using ReadyUp.Core.Interfaces;
using ReadyUp.Core.Models;
using ReadyUp.Core.Services;

namespace ReadyUp.Metadata;

/// <summary>
/// Zero-configuration provider that always runs first: normalizes the
/// title and extracts the executable's embedded icon. Requires no network
/// access or API keys, so it's the baseline every game gets even fully
/// offline.
/// </summary>
public sealed class LocalHeuristicMetadataProvider : IMetadataProvider, IArtProvider
{
    public string Name => "Local Heuristics";
    public bool IsAvailable => true;

    public Task<MetadataRecord?> FetchAsync(Game game, CancellationToken ct = default)
    {
        var record = new MetadataRecord
        {
            ProviderName = Name,
            Confidence = 0.4,
            Title = TitleNormalizer.ToDisplayTitle(game.Title),
        };
        return Task.FromResult<MetadataRecord?>(record);
    }

    public Task<IReadOnlyList<ArtCandidate>> SearchAsync(Game game, ArtAssetType type, CancellationToken ct = default)
    {
        if (type != ArtAssetType.Icon)
        {
            return Task.FromResult<IReadOnlyList<ArtCandidate>>(Array.Empty<ArtCandidate>());
        }

        var candidate = new ArtCandidate
        {
            Type = ArtAssetType.Icon,
            ProviderName = Name,
            ThumbnailUrl = game.ExecutablePath,
            FullImageUrl = game.ExecutablePath,
        };

        return Task.FromResult<IReadOnlyList<ArtCandidate>>(new[] { candidate });
    }

    public async Task ApplyAsync(ArtCandidate candidate, string destinationPath, CancellationToken ct = default)
    {
        var bytes = IconExtractor.TryExtractPng(candidate.FullImageUrl);
        if (bytes is null)
        {
            throw new InvalidOperationException($"Could not extract an icon from '{candidate.FullImageUrl}'.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        await File.WriteAllBytesAsync(destinationPath, bytes, ct).ConfigureAwait(false);
    }
}
