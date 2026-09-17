namespace ReadyUp.Core.Models;

/// <summary>
/// Record of one applied art image for a game, kept alongside the
/// denormalized path fields on <see cref="Game"/> so the art cache can be
/// audited/cleaned independently of the game row.
/// </summary>
public sealed class ArtAsset
{
    public required Guid GameId { get; init; }
    public required ArtAssetType Type { get; init; }
    public required string LocalPath { get; set; }
    public string? SourceUrl { get; set; }
    public ArtSource Source { get; set; } = ArtSource.LocalFile;
    public int? WidthPixels { get; set; }
    public int? HeightPixels { get; set; }
    public DateTime AppliedUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// A single candidate image returned by an <see cref="Interfaces.IArtProvider"/>
/// search, before the user picks one to apply.
/// </summary>
public sealed class ArtCandidate
{
    public required string ThumbnailUrl { get; init; }
    public required string FullImageUrl { get; init; }
    public required ArtAssetType Type { get; init; }
    public string? ProviderName { get; init; }
    public int? WidthPixels { get; init; }
    public int? HeightPixels { get; init; }
}
