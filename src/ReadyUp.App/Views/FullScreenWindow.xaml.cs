using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using ReadyUp.App.Behaviors;
using ReadyUp.App.Services;
using ReadyUp.App.ViewModels;
using ReadyUp.Core.Models;
using ReadyUp.Input;

namespace ReadyUp.App.Views;

/// <summary>
/// The controller-first "Big Picture" shell: a horizontal game shelf over a
/// full-bleed background of the currently focused title, with gamepad-only
/// overlays for game options (Y) and the main menu (Start), laid out
/// Steam-Big-Picture style.
/// </summary>
public partial class FullScreenWindow : Window
{
    private readonly LibraryViewModel _viewModel;
    private readonly UiScaleService _uiScaleService;
    private readonly WindowModeManager _windowModeManager;
    private readonly IServiceProvider _services;
    private readonly GamepadNavigationManager _gamepadNavigationManager;
    private GamepadFocusBehavior? _gamepadFocus;
    private GameViewModel? _optionsGame;

    public FullScreenWindow(
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
            if (dataContext is GameViewModel game) ShowGameOptions(game);
        };
        _gamepadFocus.MainMenuRequested += (_, _) => ShowMainMenu();
        _gamepadFocus.BackRequested += (_, _) => CloseAnyOverlay();

        Loaded += async (_, _) => await _viewModel.InitializeAsync();
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (!CloseAnyOverlay())
            {
                _windowModeManager.ShowWindowed();
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
        {
            _windowModeManager.ShowWindowed();
            e.Handled = true;
        }
    }

    private void GameTile_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: GameViewModel game }) return;
        UpdateBackground(game.BackgroundPath ?? game.BoxArtPath);
    }

    private void UpdateBackground(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            BackgroundImage.Source = null;
            return;
        }

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.DecodePixelWidth = 1920;
        bitmap.UriSource = new Uri(path, UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();
        BackgroundImage.Source = bitmap;
    }

    private void ShowGameOptions(GameViewModel game)
    {
        _optionsGame = game;
        GameOptionsTitle.Text = game.Title;
        GameOptionsOverlayGrid.Visibility = Visibility.Visible;
        MainMenuOverlayGrid.Visibility = Visibility.Collapsed;
    }

    private void ShowMainMenu()
    {
        if (GameOptionsOverlayGrid.Visibility == Visibility.Visible) return;
        MainMenuOverlayGrid.Visibility = Visibility.Visible;
    }

    /// <summary>Closes whichever overlay is currently open. Returns true if one was closed.</summary>
    private bool CloseAnyOverlay()
    {
        if (GameOptionsOverlayGrid.Visibility == Visibility.Visible)
        {
            GameOptionsOverlayGrid.Visibility = Visibility.Collapsed;
            return true;
        }

        if (MainMenuOverlayGrid.Visibility == Visibility.Visible)
        {
            MainMenuOverlayGrid.Visibility = Visibility.Collapsed;
            return true;
        }

        return false;
    }

    private void GameOptions_Launch_Click(object sender, RoutedEventArgs e)
    {
        if (_optionsGame is not null) _viewModel.LaunchGame(_optionsGame);
        CloseAnyOverlay();
    }

    private void GameOptions_ChangeArt_Click(object sender, RoutedEventArgs e)
    {
        if (_optionsGame is null) return;

        var viewModel = (ChangeArtViewModel)_services.GetService(typeof(ChangeArtViewModel))!;
        viewModel.Initialize(_optionsGame, ArtAssetType.BoxArt);

        var dialog = new ChangeArtDialog(viewModel) { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        GamepadFocusBehavior.AttachToDialog(dialog, _gamepadNavigationManager);
        dialog.ShowDialog();
        CloseAnyOverlay();
    }

    private void GameOptions_ToggleFavorite_Click(object sender, RoutedEventArgs e)
    {
        if (_optionsGame is null) return;
        _viewModel.SelectedGame = _optionsGame;
        _viewModel.ToggleFavoriteCommand.Execute(null);
        CloseAnyOverlay();
    }

    private void GameOptions_Remove_Click(object sender, RoutedEventArgs e)
    {
        if (_optionsGame is null) return;
        _viewModel.SelectedGame = _optionsGame;
        _viewModel.RemoveSelectedGameCommand.Execute(null);
        CloseAnyOverlay();
    }

    private void GameOptions_Close_Click(object sender, RoutedEventArgs e) => CloseAnyOverlay();

    private void MainMenu_ExitToWindowed_Click(object sender, RoutedEventArgs e)
    {
        CloseAnyOverlay();
        _windowModeManager.ShowWindowed();
    }

    private void MainMenu_ExitApp_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

    private void MainMenu_Close_Click(object sender, RoutedEventArgs e) => CloseAnyOverlay();
}
