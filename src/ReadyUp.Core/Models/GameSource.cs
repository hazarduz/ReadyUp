namespace ReadyUp.Core.Models;

/// <summary>How a <see cref="Game"/> entry was added to the library.</summary>
public enum GameSource
{
    ScannedDrive,
    ScannedRegistry,
    Manual,
    Steam,
    EpicGamesStore,
    Gog,
}

/// <summary>The four replaceable art slots for a game.</summary>
public enum ArtAssetType
{
    Icon,
    Banner,
    BoxArt,
    Background,
}

/// <summary>Where an <see cref="ArtAsset"/>'s image originated.</summary>
public enum ArtSource
{
    ExtractedFromExecutable,
    LocalFile,
    SteamGridDb,
    Generated,
}

/// <summary>Which window mode the shell is currently presenting.</summary>
public enum ShellMode
{
    Windowed,
    FullScreen,
}
