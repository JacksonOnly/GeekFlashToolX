namespace GeekFlashToolX.Core.Services;

public interface IDialogService
{
    Task ShowMessageAsync(string title, string message, CancellationToken cancellationToken = default);
}
