using Microsoft.Data.Sqlite;

namespace ReadyUp.Data;

internal static class SchemaInitializer
{
    public static void EnsureSchema(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS games (
                id                     TEXT PRIMARY KEY,
                title                  TEXT NOT NULL,
                sorting_title          TEXT NOT NULL,
                description            TEXT NULL,
                publisher              TEXT NULL,
                developer              TEXT NULL,
                executable_path        TEXT NOT NULL,
                install_directory      TEXT NOT NULL,
                launch_arguments       TEXT NULL,
                source                 TEXT NOT NULL,
                icon_path              TEXT NULL,
                banner_path            TEXT NULL,
                box_art_path           TEXT NULL,
                background_path        TEXT NULL,
                playtime_minutes       INTEGER NOT NULL DEFAULT 0,
                last_played_utc        TEXT NULL,
                date_added_utc         TEXT NOT NULL,
                tags_json              TEXT NOT NULL DEFAULT '[]',
                is_hidden              INTEGER NOT NULL DEFAULT 0,
                is_favorite            INTEGER NOT NULL DEFAULT 0,
                manually_edited_json   TEXT NOT NULL DEFAULT '[]'
            );

            CREATE UNIQUE INDEX IF NOT EXISTS ix_games_executable_path
                ON games (executable_path);

            CREATE TABLE IF NOT EXISTS art_assets (
                game_id       TEXT NOT NULL,
                type          TEXT NOT NULL,
                local_path    TEXT NOT NULL,
                source_url    TEXT NULL,
                source        TEXT NOT NULL,
                width_px      INTEGER NULL,
                height_px     INTEGER NULL,
                applied_utc   TEXT NOT NULL,
                PRIMARY KEY (game_id, type)
            );
            """;
        cmd.ExecuteNonQuery();
    }
}
