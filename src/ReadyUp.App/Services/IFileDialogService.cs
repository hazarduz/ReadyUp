namespace ReadyUp.App.Services;

/// <summary>Thin abstraction over Win32 file/folder pickers so view models don't call WPF dialog types directly.</summary>
public interface IFileDialogService
{
    string? PickExecutable();
    string? PickFolder();
    string? PickImageFile();
}
