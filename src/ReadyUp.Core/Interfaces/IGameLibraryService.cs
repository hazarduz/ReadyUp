using ReadyUp.Core.Models;

namespace ReadyUp.Core.Interfaces;

/// <summary>
/// Central orchestration point for the library: dedupes scan results into
/// <see cref="Game"/> rows, merges metadata without clobbering manual edits,
/// and records playtime/art changes. UI view models talk to this, not
/// directly to <see cref="IGameRepository"/>.
/// </summary>
public interface IGameLibraryService
{
    event EventHandler<Game>? GameAdded;
    event EventHandler<Game>? GameUpdated;
    event EventHandler<Guid>? GameRemoved;

    Task<IReadOnlyList<Game>> GetLibraryAsync(CancellationToken ct = default);

    /// <summary>Inserts a new game or returns the existing one for the same executable path.</summary>
    Task<Game> UpsertFromScanResultAsync(ScanResult result, CancellationToken ct = default);

    /// <summary>Merges an automatic metadata record into a game, skipping any manually-edited fields.</summary>
    Task ApplyMetadataAsync(Guid gameId, MetadataRecord record, CancellationToken ct = default);

    /// <summary>Applies a manual field edit from the UI and marks the field(s) as user-owned going forward.</summary>
    Task ApplyManualEditAsync(Guid gameId, Action<Game> mutate, IEnumerable<string> editedFieldNames, CancellationToken ct = default);

    Task SetArtAsync(Guid gameId, ArtAssetType type, string localPath, ArtSource source, string? sourceUrl, CancellationToken ct = default);

    Task RecordPlaySessionAsync(Guid gameId, TimeSpan duration, CancellationToken ct = default);

    Task RemoveAsync(Guid gameId, CancellationToken ct = default);
}
