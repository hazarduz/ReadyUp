using System.Windows;
using ReadyUp.App.ViewModels;

namespace ReadyUp.App.Views;

/// <summary>
/// Desktop-mode "Change Game Art" flow (right-click a tile → Change Game
/// Art...). The gamepad/full-screen equivalent (hold Y) reuses the same
/// <see cref="ChangeArtViewModel"/> rendered inside
/// <see cref="GameOptionsOverlay"/> instead of this modal window.
/// </summary>
public partial class ChangeArtDialog : Window
{
    public ChangeArtDialog(ChangeArtViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
