using ReadyUp.Core.Interfaces;
using ReadyUp.Core.Models;

namespace ReadyUp.Metadata;

/// <summary>
/// Runs every registered <see cref="IMetadataProvider"/> concurrently
/// (each bounded by <see cref="ProviderTimeout"/> so a slow/offline API
/// never blocks the others) and merges results into the game via
/// <see cref="IGameLibraryService.ApplyMetadataAsync"/>, applied in
/// ascending confidence order so higher-confidence providers win ties
/// without ever touching a field the user has manually edited.
/// </summary>
public sealed class MetadataAggregatorService
{
    private static readonly TimeSpan ProviderTimeout = TimeSpan.FromSeconds(10);

    private readonly IReadOnlyList<IMetadataProvider> _providers;
    private readonly IGameLibraryService _libraryService;

    public MetadataAggregatorService(IEnumerable<IMetadataProvider> providers, IGameLibraryService libraryService)
    {
        _providers = providers.ToList();
        _libraryService = libraryService;
    }

    public async Task EnrichAsync(Game game, CancellationToken ct = default)
    {
        var available = _providers.Where(p => p.IsAvailable).ToList();
        if (available.Count == 0) return;

        var fetchTasks = available.Select(provider => FetchWithTimeoutAsync(provider, game, ct)).ToArray();
        var results = await Task.WhenAll(fetchTasks).ConfigureAwait(false);

        foreach (var record in results.Where(r => r is not null).OrderBy(r => r!.Confidence))
        {
            await _libraryService.ApplyMetadataAsync(game.Id, record!, ct).ConfigureAwait(false);
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
