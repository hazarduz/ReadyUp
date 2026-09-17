using System.Windows;
using ReadyUp.App.ViewModels;

namespace ReadyUp.App.Views;

/// <summary>
/// Settings screen: UI scale, startup mode, controller deadzone, drive-scan
/// options, library folders, and optional online-provider API keys.
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;

    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = _viewModel;

        // PasswordBox intentionally doesn't support data binding (avoids the secret
        // sitting in a bindable dependency property / memory dump); wire it manually.
        IgdbSecretBox.Password = _viewModel.IgdbClientSecret ?? string.Empty;
    }

    private void IgdbSecretBox_PasswordChanged(object sender, RoutedEventArgs e)
        => _viewModel.IgdbClientSecret = IgdbSecretBox.Password;

    private void SaveButton_Click(object sender, RoutedEventArgs e) => Close();
}
