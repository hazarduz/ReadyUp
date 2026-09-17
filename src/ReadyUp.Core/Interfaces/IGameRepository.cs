using ReadyUp.Core.Models;

namespace ReadyUp.Core.Interfaces;

/// <summary>Persistence contract for the game library, implemented by ReadyUp.Data.</summary>
public interface IGameRepository
{
    Task<IReadOnlyList<Game>> GetAllAsync(CancellationToken ct = default);
    Task<Game?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Game?> FindByExecutablePathAsync(string executablePath, CancellationToken ct = default);
    Task UpsertAsync(Game game, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    Task SaveArtAssetAsync(ArtAsset asset, CancellationToken ct = default);
    Task<IReadOnlyList<ArtAsset>> GetArtAssetsAsync(Guid gameId, CancellationToken ct = default);
}
