using System.Windows;
using ReadyUp.App.ViewModels;

namespace ReadyUp.App.Views;

/// <summary>Simple modal form for manually correcting a game's title/description/publisher/developer.</summary>
public partial class EditMetadataDialog : Window
{
    public string EditedTitle => TitleBox.Text.Trim();
    public string? EditedDescription => string.IsNullOrWhiteSpace(DescriptionBox.Text) ? null : DescriptionBox.Text;
    public string? EditedPublisher => string.IsNullOrWhiteSpace(PublisherBox.Text) ? null : PublisherBox.Text;
    public string? EditedDeveloper => string.IsNullOrWhiteSpace(DeveloperBox.Text) ? null : DeveloperBox.Text;

    public EditMetadataDialog(GameViewModel game)
    {
        InitializeComponent();

        TitleBox.Text = game.Title;
        DescriptionBox.Text = game.Description ?? string.Empty;
        PublisherBox.Text = game.Publisher ?? string.Empty;
        DeveloperBox.Text = game.Developer ?? string.Empty;

        PublisherBox.ToolTip = "Publisher";
        DeveloperBox.ToolTip = "Developer";
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TitleBox.Text))
        {
            MessageBox.Show(this, "Title cannot be empty.", "ReadyUp", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
