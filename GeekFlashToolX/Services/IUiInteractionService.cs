using GeekFlashToolX.ViewModels;

namespace GeekFlashToolX.Services;

public interface IUiInteractionService
{
    Task<string?> SelectProtocolAsync();
    Task<string?> PickFirehoseLoaderAsync();
    Task ShowLogPreviewAsync(LogPreviewViewModel preview);
    Task CopyTextAsync(string text);
    void ClosePreview();
    void MinimizeMainWindow();
    void ToggleMainWindowMaximize();
    void CloseMainWindow();
}
