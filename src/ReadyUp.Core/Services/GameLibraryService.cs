using ReadyUp.Core.Interfaces;
using ReadyUp.Core.Models;

namespace ReadyUp.Core.Services;

public sealed class GameLibraryService : IGameLibraryService
{
    private readonly IGameRepository _repository;
    private readonly IArtCacheStore _artCache;

    public event EventHandler<Game>? GameAdded;
    public event EventHandler<Game>? GameUpdated;
    public event EventHandler<Guid>? GameRemoved;

    public GameLibraryService(IGameRepository repository, IArtCacheStore artCache)
    {
        _repository = repository;
        _artCache = artCache;
    }

    public Task<IReadOnlyList<Game>> GetLibraryAsync(CancellationToken ct = default)
        => _repository.GetAllAsync(ct);

    public async Task<Game> UpsertFromScanResultAsync(ScanResult result, CancellationToken ct = default)
    {
        var normalizedPath = NormalizePath(result.ExecutablePath);
        var existing = await _repository.FindByExecutablePathAsync(normalizedPath, ct).ConfigureAwait(false);
        if (existing is not null)
        {
            return existing;
        }

        var displayTitle = TitleNormalizer.ToDisplayTitle(result.Title);
        var game = new Game
        {
            Title = displayTitle,
            SortingTitle = TitleNormalizer.ToSortingTitle(displayTitle),
            ExecutablePath = normalizedPath,
            InstallDirectory = result.InstallDirectory,
            Publisher = result.Publisher,
            Source = result.Source,
        };

        await _repository.UpsertAsync(game, ct).ConfigureAwait(false);
        GameAdded?.Invoke(this, game);
        return game;
    }

    public async Task ApplyMetadataAsync(Guid gameId, MetadataRecord record, CancellationToken ct = default)
    {
        var game = await _repository.GetByIdAsync(gameId, ct).ConfigureAwait(false);
        if (game is null) return;

        bool changed = TryAssign(game, nameof(Game.Title), record.Title, v => game.Title = v!);
        changed |= TryAssign(game, nameof(Game.Description), record.Description, v => game.Description = v);
        changed |= TryAssign(game, nameof(Game.Publisher), record.Publisher, v => game.Publisher = v);
        changed |= TryAssign(game, nameof(Game.Developer), record.Developer, v => game.Developer = v);

        if (record.Tags is { Count: > 0 } && !game.ManuallyEditedFields.Contains(nameof(Game.Tags)))
        {
            foreach (var tag in record.Tags)
            {
                if (!game.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
                    game.Tags.Add(tag);
            }
            changed = true;
        }

        if (changed)
        {
            game.SortingTitle = TitleNormalizer.ToSortingTitle(game.Title);
            await _repository.UpsertAsync(game, ct).ConfigureAwait(false);
            GameUpdated?.Invoke(this, game);
        }
    }

    public async Task ApplyManualEditAsync(Guid gameId, Action<Game> mutate, IEnumerable<string> editedFieldNames, CancellationToken ct = default)
    {
        var game = await _repository.GetByIdAsync(gameId, ct).ConfigureAwait(false);
        if (game is null) return;

        mutate(game);
        foreach (var field in editedFieldNames)
        {
            game.ManuallyEditedFields.Add(field);
        }

        if (game.ManuallyEditedFields.Contains(nameof(Game.Title)))
        {
            game.SortingTitle = TitleNormalizer.ToSortingTitle(game.Title);
        }

        await _repository.UpsertAsync(game, ct).ConfigureAwait(false);
        GameUpdated?.Invoke(this, game);
    }

    public async Task SetArtAsync(Guid gameId, ArtAssetType type, string localPath, ArtSource source, string? sourceUrl, CancellationToken ct = default)
    {
        var game = await _repository.GetByIdAsync(gameId, ct).ConfigureAwait(false);
        if (game is null) return;

        game.SetArtPath(type, localPath);
        await _repository.UpsertAsync(game, ct).ConfigureAwait(false);
        await _repository.SaveArtAssetAsync(new ArtAsset
        {
            GameId = gameId,
            Type = type,
            LocalPath = localPath,
            Source = source,
            SourceUrl = sourceUrl,
        }, ct).ConfigureAwait(false);

        GameUpdated?.Invoke(this, game);
    }

    public async Task RecordPlaySessionAsync(Guid gameId, TimeSpan duration, CancellationToken ct = default)
    {
        var game = await _repository.GetByIdAsync(gameId, ct).ConfigureAwait(false);
        if (game is null) return;

        game.PlaytimeMinutes += Math.Max(0, (long)duration.TotalMinutes);
        game.LastPlayedUtc = DateTime.UtcNow;

        await _repository.UpsertAsync(game, ct).ConfigureAwait(false);
        GameUpdated?.Invoke(this, game);
    }

    public async Task RemoveAsync(Guid gameId, CancellationToken ct = default)
    {
        await _repository.DeleteAsync(gameId, ct).ConfigureAwait(false);
        _artCache.DeleteAllFor(gameId);
        GameRemoved?.Invoke(this, gameId);
    }

    private static bool TryAssign(Game game, string fieldName, string? incoming, Action<string?> assign)
    {
        if (string.IsNullOrWhiteSpace(incoming)) return false;
        if (game.ManuallyEditedFields.Contains(fieldName)) return false;

        assign(incoming);
        return true;
    }

    private static string NormalizePath(string path)
        => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar).ToLowerInvariant();
}
