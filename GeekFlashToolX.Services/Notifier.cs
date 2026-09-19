using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Threading;
using GeekFlashToolX.Core.Services;

namespace GeekFlashToolX.Services;

public sealed class Notifier(Func<Window?> ownerProvider) : INotifier
{
    private Window? _owner;
    private WindowNotificationManager? _manager;

    public IDisposable Show(string title, string message, NotificationKind kind = NotificationKind.Information,
        TimeSpan? expiration = null)
    {
        Dispatcher.UIThread.VerifyAccess();
        var owner = ownerProvider();
        if (owner is null || !owner.IsVisible) return new NotificationHandle(null, null);
        if (!ReferenceEquals(owner, _owner))
        {
            if (_owner is not null) _owner.Closed -= OnOwnerClosed;
            _manager?.CloseAll();
            _owner = owner;
            _manager = new WindowNotificationManager(owner)
            {
                Position = NotificationPosition.TopRight,
                MaxItems = 3,
                // Keep notifications below the application's custom title bar.
                Margin = new Thickness(16, 64, 16, 16),
            };
            owner.Closed += OnOwnerClosed;
        }

        var type = kind switch
        {
            NotificationKind.Success => NotificationType.Success,
            NotificationKind.Warning => NotificationType.Warning,
            NotificationKind.Error => NotificationType.Error,
            _ => NotificationType.Information,
        };
        var notification = new Notification(title, message, type, expiration ?? TimeSpan.FromSeconds(5));
        _manager!.Show(notification);
        return new NotificationHandle(_manager, notification);
    }

    private void OnOwnerClosed(object? sender, EventArgs args)
    {
        if (_owner is not null) _owner.Closed -= OnOwnerClosed;
        _manager?.CloseAll();
        _manager = null;
        _owner = null;
    }

    private sealed class NotificationHandle(WindowNotificationManager? manager, Notification? notification) : IDisposable
    {
        private int _disposed;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0 || manager is null || notification is null) return;
            if (Dispatcher.UIThread.CheckAccess()) manager.Close(notification);
            else Dispatcher.UIThread.Post(() => manager.Close(notification));
        }
    }
}
