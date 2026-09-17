namespace ReadyUp.Core.Models;

/// <summary>
/// A single entry in the user's game library. This is the persisted,
/// framework-agnostic model — the WPF layer wraps it in a GameViewModel
/// for data binding rather than binding to this type directly.
/// </summary>
public sealed class Game
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required string Title { get; set; }

    /// <summary>Normalized title used for alphabetical sorting (e.g. "Witcher 3, The").</summary>
    public string SortingTitle { get; set; } = string.Empty;

    public string? Description { get; set; }
    public string? Publisher { get; set; }
    public string? Developer { get; set; }

    public required string ExecutablePath { get; set; }
    public required string InstallDirectory { get; set; }
    public string? LaunchArguments { get; set; }

    public GameSource Source { get; set; } = GameSource.Manual;

    public string? IconPath { get; set; }
    public string? BannerPath { get; set; }
    public string? BoxArtPath { get; set; }
    public string? BackgroundPath { get; set; }

    public long PlaytimeMinutes { get; set; }
    public DateTime? LastPlayedUtc { get; set; }
    public DateTime DateAddedUtc { get; set; } = DateTime.UtcNow;

    public List<string> Tags { get; set; } = new();

    public bool IsHidden { get; set; }
    public bool IsFavorite { get; set; }

    /// <summary>
    /// Names of <see cref="Game"/> properties the user has manually edited.
    /// The metadata aggregator will never overwrite these automatically.
    /// </summary>
    public HashSet<string> ManuallyEditedFields { get; set; } = new(StringComparer.Ordinal);

    public string? ArtPathFor(ArtAssetType type) => type switch
    {
        ArtAssetType.Icon => IconPath,
        ArtAssetType.Banner => BannerPath,
        ArtAssetType.BoxArt => BoxArtPath,
        ArtAssetType.Background => BackgroundPath,
        _ => null,
    };

    public void SetArtPath(ArtAssetType type, string? path)
    {
        switch (type)
        {
            case ArtAssetType.Icon: IconPath = path; break;
            case ArtAssetType.Banner: BannerPath = path; break;
            case ArtAssetType.BoxArt: BoxArtPath = path; break;
            case ArtAssetType.Background: BackgroundPath = path; break;
        }
    }
}
