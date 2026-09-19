using GeekFlashToolX.Core.Models;

namespace GeekFlashToolX.Core.Services;

public interface IUpdateService
{
    Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default);
    Task IgnoreVersionAsync(string version, CancellationToken cancellationToken = default);
}

public interface IUpdateCoordinator
{
    Task CheckAndNotifyAsync(bool automatic = false, CancellationToken cancellationToken = default);
}

public interface IExternalLauncher
{
    Task OpenUriAsync(Uri uri);
    Task OpenFolderAsync(string path);
    Task OpenFileAsync(string path);
}
