using Microsoft.Win32;

namespace ReadyUp.App.Services;

public sealed class FileDialogService : IFileDialogService
{
    public string? PickExecutable()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select Game Executable",
            Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*",
            CheckFileExists = true,
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Game Install Folder",
        };
        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }

    public string? PickImageFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select Image",
            Filter = "Image files (*.png;*.jpg;*.jpeg;*.bmp;*.ico)|*.png;*.jpg;*.jpeg;*.bmp;*.ico|All files (*.*)|*.*",
            CheckFileExists = true,
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
