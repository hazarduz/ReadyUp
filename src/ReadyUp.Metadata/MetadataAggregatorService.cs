using ReadyUp.Core.Interfaces;
using ReadyUp.Core.Models;

namespace ReadyUp.Metadata;

/// <summary>
/// Runs every registered <see cref="IMetadataProvider"/> concurrently
/// (each bounded by <see cref="ProviderTimeout"/> so a slow/offline API
/// never blocks the others) and merges results into the game via
/// <see cref="IGameLibraryService.ApplyMetadataAsync"/>, applied in
/// ascending confidence order so higher-confidence providers win ties
/// without ever touching a field the user has manually edited. Also
/// auto-fetches art (icon/box art/banner/background) for any slot the
/// game doesn't already have art in, so a fresh scan comes in fully
/// dressed instead of requiring a manual "Change Game Art" per game.
/// </summary>
public sealed class MetadataAggregatorService
{
    private static readonly TimeSpan ProviderTimeout = TimeSpan.FromSeconds(10);

    private static readonly ArtAssetType[] AutoFetchArtTypes =
    {
        ArtAssetType.Icon, ArtAssetType.BoxArt, ArtAssetType.Banner, ArtAssetType.Background,
    };

    private readonly IReadOnlyList<IMetadataProvider> _metadataProviders;
    private readonly IReadOnlyList<IArtProvider> _artProviders;
    private readonly IArtCacheStore _artCache;
    private readonly IGameLibraryService _libraryService;

    public MetadataAggregatorService(
        IEnumerable<IMetadataProvider> metadataProviders,
        IEnumerable<IArtProvider> artProviders,
        IArtCacheStore artCache,
        IGameLibraryService libraryService)
    {
        _metadataProviders = metadataProviders.ToList();
        _artProviders = artProviders.ToList();
        _artCache = artCache;
        _libraryService = libraryService;
    }

    public async Task EnrichAsync(Game game, CancellationToken ct = default)
    {
        await EnrichTextMetadataAsync(game, ct).ConfigureAwait(false);
        await AutoFetchArtAsync(game, ct).ConfigureAwait(false);
    }

    private async Task EnrichTextMetadataAsync(Game game, CancellationToken ct)
    {
        var available = _metadataProviders.Where(p => p.IsAvailable).ToList();
        if (available.Count == 0) return;

        var fetchTasks = available.Select(provider => FetchWithTimeoutAsync(provider, game, ct)).ToArray();
        var results = await Task.WhenAll(fetchTasks).ConfigureAwait(false);

        foreach (var record in results.Where(r => r is not null).OrderBy(r => r!.Confidence))
        {
            await _libraryService.ApplyMetadataAsync(game.Id, record!, ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// For each art slot the game doesn't already have a path for, tries each
    /// available provider in turn and applies the first candidate found.
    /// Never overwrites an existing path, whether it was set by a previous
    /// auto-fetch or the user's own "Change Game Art" choice.
    /// </summary>
    private async Task AutoFetchArtAsync(Game game, CancellationToken ct)
    {
        var available = _artProviders.Where(p => p.IsAvailable).ToList();
        if (available.Count == 0) return;

        foreach (var type in AutoFetchArtTypes)
        {
            if (!string.IsNullOrWhiteSpace(game.ArtPathFor(type))) continue;

            foreach (var provider in available)
            {
                if (await TryApplyArtAsync(provider, game, type, ct).ConfigureAwait(false))
                {
                    break;
                }
            }
        }
    }

    private async Task<bool> TryApplyArtAsync(IArtProvider provider, Game game, ArtAssetType type, CancellationToken ct)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(ProviderTimeout);

        try
        {
            var candidates = await provider.SearchAsync(game, type, timeoutCts.Token).ConfigureAwait(false);
            var best = candidates.FirstOrDefault();
            if (best is null) return false;

            var destination = _artCache.GetPath(game.Id, type);
            await provider.ApplyAsync(best, destination, timeoutCts.Token).ConfigureAwait(false);

            var source = provider is LocalHeuristicMetadataProvider ? ArtSource.ExtractedFromExecutable : ArtSource.SteamGridDb;
            await _libraryService.SetArtAsync(game.Id, type, destination, source, best.FullImageUrl, ct).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return false; // This provider timed out for this art type; try the next one.
        }
        catch (Exception)
        {
            return false; // A misbehaving/offline provider must not fail the whole enrichment pass.
        }
    }

    private static async Task<MetadataRecord?> FetchWithTimeoutAsync(IMetadataProvider provider, Game game, CancellationToken ct)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(ProviderTimeout);

        try
        {
            return await provider.FetchAsync(game, timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return null; // This provider timed out; others still complete independently.
        }
        catch (Exception)
        {
            return null; // A misbehaving/offline provider must not fail the whole enrichment pass.
        }
    }
}
