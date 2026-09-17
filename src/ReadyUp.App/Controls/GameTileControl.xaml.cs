using System.Windows;
using System.Windows.Controls;
using ReadyUp.App.ViewModels;

namespace ReadyUp.App.Controls;

/// <summary>
/// One game's tile in the library grid. Launching is a normal Button
/// click/Command binding (works identically for mouse, keyboard Enter, and
/// gamepad Accept via GamepadFocusBehavior's ButtonAutomationPeer.Invoke).
/// Actions that need to open another window (Change Art, Edit Metadata)
/// bubble up as events instead of owning a window reference themselves.
/// </summary>
public partial class GameTileControl : UserControl
{
    public event EventHandler<GameViewModel>? ChangeArtRequested;
    public event EventHandler<GameViewModel>? EditMetadataRequested;

    public GameTileControl()
    {
        InitializeComponent();
    }

    private GameViewModel? Game => DataContext as GameViewModel;

    private LibraryViewModel? FindLibraryViewModel(DependencyObject source)
    {
        // Walk up the visual tree to the owning ItemsControl, whose DataContext is the LibraryViewModel.
        var current = source;
        while (current is not null)
        {
            if (current is FrameworkElement { DataContext: LibraryViewModel vm })
            {
                return vm;
            }
            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    private void LaunchMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (Game is null) return;
        FindLibraryViewModel(this)?.LaunchGame(Game);
    }

    private void ChangeArtMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (Game is not null) ChangeArtRequested?.Invoke(this, Game);
    }

    private void EditMetadataMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (Game is not null) EditMetadataRequested?.Invoke(this, Game);
    }

    private void ToggleFavoriteMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var vm = FindLibraryViewModel(this);
        if (vm is null || Game is null) return;
        vm.SelectedGame = Game;
        vm.ToggleFavoriteCommand.Execute(null);
    }

    private void RemoveMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var vm = FindLibraryViewModel(this);
        if (vm is null || Game is null) return;
        vm.SelectedGame = Game;
        vm.RemoveSelectedGameCommand.Execute(null);
    }
}
