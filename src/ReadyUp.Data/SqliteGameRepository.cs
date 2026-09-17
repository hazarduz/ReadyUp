using System.Text.Json;
using Microsoft.Data.Sqlite;
using ReadyUp.Core.Interfaces;
using ReadyUp.Core.Models;

namespace ReadyUp.Data;

/// <summary>
/// SQLite-backed <see cref="IGameRepository"/>. A single file
/// (%LOCALAPPDATA%\ReadyUp\library.db) holds the whole library, which keeps
/// backup/restore and portability trivial for a 2-table schema.
/// </summary>
public sealed class SqliteGameRepository : IGameRepository, IDisposable
{
    private readonly string _connectionString;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public SqliteGameRepository(string? databasePath = null)
    {
        AppPaths.EnsureCreated();
        var path = databasePath ?? AppPaths.DatabasePath;
        _connectionString = new SqliteConnectionStringBuilder { DataSource = path }.ToString();

        using var connection = Open();
        SchemaInitializer.EnsureSchema(connection);
    }

    public async Task<IReadOnlyList<Game>> GetAllAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            using var connection = Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT * FROM games ORDER BY sorting_title COLLATE NOCASE;";
            using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);

            var games = new List<Game>();
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                games.Add(ReadGame(reader));
            }
            return games;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<Game?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            using var connection = Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT * FROM games WHERE id = $id;";
            cmd.Parameters.AddWithValue("$id", id.ToString());
            using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            return await reader.ReadAsync(ct).ConfigureAwait(false) ? ReadGame(reader) : null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<Game?> FindByExecutablePathAsync(string executablePath, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            using var connection = Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT * FROM games WHERE executable_path = $path;";
            cmd.Parameters.AddWithValue("$path", executablePath);
            using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            return await reader.ReadAsync(ct).ConfigureAwait(false) ? ReadGame(reader) : null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task UpsertAsync(Game game, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            using var connection = Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                INSERT INTO games (
                    id, title, sorting_title, description, publisher, developer,
                    executable_path, install_directory, launch_arguments, source,
                    icon_path, banner_path, box_art_path, background_path,
                    playtime_minutes, last_played_utc, date_added_utc, tags_json,
                    is_hidden, is_favorite, manually_edited_json)
                VALUES (
                    $id, $title, $sortingTitle, $description, $publisher, $developer,
                    $executablePath, $installDirectory, $launchArguments, $source,
                    $iconPath, $bannerPath, $boxArtPath, $backgroundPath,
                    $playtimeMinutes, $lastPlayedUtc, $dateAddedUtc, $tagsJson,
                    $isHidden, $isFavorite, $manuallyEditedJson)
                ON CONFLICT(id) DO UPDATE SET
                    title = excluded.title,
                    sorting_title = excluded.sorting_title,
                    description = excluded.description,
                    publisher = excluded.publisher,
                    developer = excluded.developer,
                    executable_path = excluded.executable_path,
                    install_directory = excluded.install_directory,
                    launch_arguments = excluded.launch_arguments,
                    source = excluded.source,
                    icon_path = excluded.icon_path,
                    banner_path = excluded.banner_path,
                    box_art_path = excluded.box_art_path,
                    background_path = excluded.background_path,
                    playtime_minutes = excluded.playtime_minutes,
                    last_played_utc = excluded.last_played_utc,
                    tags_json = excluded.tags_json,
                    is_hidden = excluded.is_hidden,
                    is_favorite = excluded.is_favorite,
                    manually_edited_json = excluded.manually_edited_json;
                """;

            BindGame(cmd, game);
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            using var connection = Open();
            using var tx = connection.BeginTransaction();

            using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = "DELETE FROM art_assets WHERE game_id = $id;";
                cmd.Parameters.AddWithValue("$id", id.ToString());
                await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }

            using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = "DELETE FROM games WHERE id = $id;";
                cmd.Parameters.AddWithValue("$id", id.ToString());
                await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }

            tx.Commit();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveArtAssetAsync(ArtAsset asset, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            using var connection = Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                INSERT INTO art_assets (game_id, type, local_path, source_url, source, width_px, height_px, applied_utc)
                VALUES ($gameId, $type, $localPath, $sourceUrl, $source, $width, $height, $appliedUtc)
                ON CONFLICT(game_id, type) DO UPDATE SET
                    local_path = excluded.local_path,
                    source_url = excluded.source_url,
                    source = excluded.source,
                    width_px = excluded.width_px,
                    height_px = excluded.height_px,
                    applied_utc = excluded.applied_utc;
                """;
            cmd.Parameters.AddWithValue("$gameId", asset.GameId.ToString());
            cmd.Parameters.AddWithValue("$type", asset.Type.ToString());
            cmd.Parameters.AddWithValue("$localPath", asset.LocalPath);
            cmd.Parameters.AddWithValue("$sourceUrl", (object?)asset.SourceUrl ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$source", asset.Source.ToString());
            cmd.Parameters.AddWithValue("$width", (object?)asset.WidthPixels ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$height", (object?)asset.HeightPixels ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$appliedUtc", asset.AppliedUtc.ToString("O"));
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<ArtAsset>> GetArtAssetsAsync(Guid gameId, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            using var connection = Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT * FROM art_assets WHERE game_id = $gameId;";
            cmd.Parameters.AddWithValue("$gameId", gameId.ToString());
            using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);

            var assets = new List<ArtAsset>();
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                assets.Add(new ArtAsset
                {
                    GameId = Guid.Parse(reader.GetString(reader.GetOrdinal("game_id"))),
                    Type = Enum.Parse<ArtAssetType>(reader.GetString(reader.GetOrdinal("type"))),
                    LocalPath = reader.GetString(reader.GetOrdinal("local_path")),
                    SourceUrl = reader.IsDBNull(reader.GetOrdinal("source_url")) ? null : reader.GetString(reader.GetOrdinal("source_url")),
                    Source = Enum.Parse<ArtSource>(reader.GetString(reader.GetOrdinal("source"))),
                    WidthPixels = reader.IsDBNull(reader.GetOrdinal("width_px")) ? null : reader.GetInt32(reader.GetOrdinal("width_px")),
                    HeightPixels = reader.IsDBNull(reader.GetOrdinal("height_px")) ? null : reader.GetInt32(reader.GetOrdinal("height_px")),
                    AppliedUtc = DateTime.Parse(reader.GetString(reader.GetOrdinal("applied_utc"))),
                });
            }
            return assets;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private static void BindGame(SqliteCommand cmd, Game game)
    {
        cmd.Parameters.AddWithValue("$id", game.Id.ToString());
        cmd.Parameters.AddWithValue("$title", game.Title);
        cmd.Parameters.AddWithValue("$sortingTitle", string.IsNullOrWhiteSpace(game.SortingTitle) ? game.Title : game.SortingTitle);
        cmd.Parameters.AddWithValue("$description", (object?)game.Description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$publisher", (object?)game.Publisher ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$developer", (object?)game.Developer ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$executablePath", game.ExecutablePath);
        cmd.Parameters.AddWithValue("$installDirectory", game.InstallDirectory);
        cmd.Parameters.AddWithValue("$launchArguments", (object?)game.LaunchArguments ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$source", game.Source.ToString());
        cmd.Parameters.AddWithValue("$iconPath", (object?)game.IconPath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$bannerPath", (object?)game.BannerPath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$boxArtPath", (object?)game.BoxArtPath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$backgroundPath", (object?)game.BackgroundPath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$playtimeMinutes", game.PlaytimeMinutes);
        cmd.Parameters.AddWithValue("$lastPlayedUtc", (object?)game.LastPlayedUtc?.ToString("O") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$dateAddedUtc", game.DateAddedUtc.ToString("O"));
        cmd.Parameters.AddWithValue("$tagsJson", JsonSerializer.Serialize(game.Tags));
        cmd.Parameters.AddWithValue("$isHidden", game.IsHidden ? 1 : 0);
        cmd.Parameters.AddWithValue("$isFavorite", game.IsFavorite ? 1 : 0);
        cmd.Parameters.AddWithValue("$manuallyEditedJson", JsonSerializer.Serialize(game.ManuallyEditedFields));
    }

    private static Game ReadGame(SqliteDataReader reader)
    {
        string? GetNullableString(string column)
        {
            var ordinal = reader.GetOrdinal(column);
            return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
        }

        var tagsJson = reader.GetString(reader.GetOrdinal("tags_json"));
        var manuallyEditedJson = reader.GetString(reader.GetOrdinal("manually_edited_json"));

        return new Game
        {
            Id = Guid.Parse(reader.GetString(reader.GetOrdinal("id"))),
            Title = reader.GetString(reader.GetOrdinal("title")),
            SortingTitle = reader.GetString(reader.GetOrdinal("sorting_title")),
            Description = GetNullableString("description"),
            Publisher = GetNullableString("publisher"),
            Developer = GetNullableString("developer"),
            ExecutablePath = reader.GetString(reader.GetOrdinal("executable_path")),
            InstallDirectory = reader.GetString(reader.GetOrdinal("install_directory")),
            LaunchArguments = GetNullableString("launch_arguments"),
            Source = Enum.Parse<GameSource>(reader.GetString(reader.GetOrdinal("source"))),
            IconPath = GetNullableString("icon_path"),
            BannerPath = GetNullableString("banner_path"),
            BoxArtPath = GetNullableString("box_art_path"),
            BackgroundPath = GetNullableString("background_path"),
            PlaytimeMinutes = reader.GetInt64(reader.GetOrdinal("playtime_minutes")),
            LastPlayedUtc = GetNullableString("last_played_utc") is { } lp ? DateTime.Parse(lp) : null,
            DateAddedUtc = DateTime.Parse(reader.GetString(reader.GetOrdinal("date_added_utc"))),
            Tags = JsonSerializer.Deserialize<List<string>>(tagsJson) ?? new(),
            IsHidden = reader.GetInt32(reader.GetOrdinal("is_hidden")) != 0,
            IsFavorite = reader.GetInt32(reader.GetOrdinal("is_favorite")) != 0,
            ManuallyEditedFields = new HashSet<string>(
                JsonSerializer.Deserialize<List<string>>(manuallyEditedJson) ?? new(), StringComparer.Ordinal),
        };
    }
}
