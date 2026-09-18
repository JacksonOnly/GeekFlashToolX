using GeekFlashToolX.Core.Models;

namespace GeekFlashToolX.Core.Services;

public interface IAppSettingsService
{
    AppSettings Current { get; }

    string SettingsFilePath { get; }

    Task LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(CancellationToken cancellationToken = default);
}
