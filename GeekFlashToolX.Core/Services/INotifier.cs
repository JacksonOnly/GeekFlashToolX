namespace GeekFlashToolX.Core.Services;

public enum NotificationKind { Information, Success, Warning, Error }

/// <summary>Non-modal feedback inside the current window. Call on the UI thread.</summary>
public interface INotifier
{
    /// <summary>Zero expiration keeps feedback visible until the returned handle is disposed.</summary>
    IDisposable Show(string title, string message, NotificationKind kind = NotificationKind.Information,
        TimeSpan? expiration = null);
}
