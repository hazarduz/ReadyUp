using System.Windows;
using System.Windows.Input;
using ReadyUp.App.Behaviors;
using ReadyUp.App.Services;
using ReadyUp.App.ViewModels;
using ReadyUp.Core.Models;
using ReadyUp.Input;

namespace ReadyUp.App.Views;

/// <summary>The windowed desktop shell: toolbar + scrollable game grid + status bar.</summary>
public partial class MainWindow : Window
{
    private readonly LibraryViewModel _viewModel;
    private readonly UiScaleService _uiScaleService;
    private readonly WindowModeManager _windowModeManager;
    private readonly IServiceProvider _services;
    private readonly GamepadNavigationManager _gamepadNavigationManager;
    private GamepadFocusBehavior? _gamepadFocus;

    public MainWindow(
        LibraryViewModel viewModel,
        UiScaleService uiScaleService,
        WindowModeManager windowModeManager,
        GamepadNavigationManager gamepadNavigationManager,
        IServiceProvider services)
    {
        InitializeComponent();

        _viewModel = viewModel;
        _uiScaleService = uiScaleService;
        _windowModeManager = windowModeManager;
        _services = services;
        _gamepadNavigationManager = gamepadNavigationManager;
        DataContext = _viewModel;

        UiScaleTransform.ScaleX = UiScaleTransform.ScaleY = _uiScaleService.CurrentScale;
        _uiScaleService.ScaleChanged += (_, scale) => Dispatcher.Invoke(() =>
        {
            UiScaleTransform.ScaleX = UiScaleTransform.ScaleY = scale;
        });

        _gamepadFocus = new GamepadFocusBehavior(this, gamepadNavigationManager);
        _gamepadFocus.GameOptionsRequested += (_, dataContext) =>
        {
            if (dataContext is GameViewModel game) OpenChangeArtDialog(game, ArtAssetType.BoxArt);
        };

        Loaded += async (_, _) => await _viewModel.InitializeAsync();
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
        {
            _windowModeManager.ShowFullScreen();
            e.Handled = true;
        }
    }

    private void FullScreenButton_Click(object sender, RoutedEventArgs e) => _windowModeManager.ShowFullScreen();

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var settingsViewModel = (SettingsViewModel)_services.GetService(typeof(SettingsViewModel))!;
        var settingsWindow = new SettingsWindow(settingsViewModel) { Owner = this };
        GamepadFocusBehavior.AttachToDialog(settingsWindow, _gamepadNavigationManager);
        settingsWindow.ShowDialog();
    }

    private void GameTile_ChangeArtRequested(object sender, GameViewModel game) => OpenChangeArtDialog(game, ArtAssetType.BoxArt);

    private void GameTile_EditMetadataRequested(object sender, GameViewModel game)
    {
        var dialog = new EditMetadataDialog(game) { Owner = this };
        GamepadFocusBehavior.AttachToDialog(dialog, _gamepadNavigationManager);
        if (dialog.ShowDialog() == true)
        {
            _ = _viewModel.ApplyMetadataEditAsync(game, dialog.EditedTitle, dialog.EditedDescription, dialog.EditedPublisher, dialog.EditedDeveloper);
        }
    }

    private void OpenChangeArtDialog(GameViewModel game, ArtAssetType initialType)
    {
        var viewModel = (ChangeArtViewModel)_services.GetService(typeof(ChangeArtViewModel))!;
        viewModel.Initialize(game, initialType);

        var dialog = new ChangeArtDialog(viewModel) { Owner = this };
        GamepadFocusBehavior.AttachToDialog(dialog, _gamepadNavigationManager);
        dialog.ShowDialog();
    }
}
