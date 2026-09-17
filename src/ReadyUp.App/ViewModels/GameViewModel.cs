using CommunityToolkit.Mvvm.ComponentModel;
using ReadyUp.Core.Models;

namespace ReadyUp.App.ViewModels;

/// <summary>WPF-bindable wrapper around a <see cref="Game"/>. Views never bind to the Core model directly.</summary>
public sealed partial class GameViewModel : ObservableObject
{
    public Guid Id => Game.Id;

    [ObservableProperty]
    private Game _game;

    public string Title => Game.Title;
    public string? Description => Game.Description;
    public string? Publisher => Game.Publisher;
    public string? Developer => Game.Developer;

    public string? IconPath => Game.IconPath;
    public string? BannerPath => Game.BannerPath;
    public string? BoxArtPath => Game.BoxArtPath;
    public string? BackgroundPath => Game.BackgroundPath;

    public string PlaytimeDisplay => Game.PlaytimeMinutes switch
    {
        <= 0 => "Never played",
        < 60 => $"{Game.PlaytimeMinutes} min",
        _ => $"{Game.PlaytimeMinutes / 60}h {Game.PlaytimeMinutes % 60}m",
    };

    public string LastPlayedDisplay => Game.LastPlayedUtc is { } lastPlayed
        ? lastPlayed.ToLocalTime().ToString("MMM d, yyyy")
        : "Never";

    public bool IsFavorite => Game.IsFavorite;

    public GameViewModel(Game game)
    {
        _game = game;
    }

    /// <summary>Refreshes every bindable property after the underlying <see cref="Game"/> changes (e.g. metadata/art applied).</summary>
    public void Refresh(Game updated)
    {
        Game = updated;
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Description));
        OnPropertyChanged(nameof(Publisher));
        OnPropertyChanged(nameof(Developer));
        OnPropertyChanged(nameof(IconPath));
        OnPropertyChanged(nameof(BannerPath));
        OnPropertyChanged(nameof(BoxArtPath));
        OnPropertyChanged(nameof(BackgroundPath));
        OnPropertyChanged(nameof(PlaytimeDisplay));
        OnPropertyChanged(nameof(LastPlayedDisplay));
        OnPropertyChanged(nameof(IsFavorite));
    }
}
