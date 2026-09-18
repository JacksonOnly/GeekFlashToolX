namespace GeekFlashToolX.Core.Services;

public interface IStartupService
{
    Task InitializeAsync(IProgress<double>? progress = null, CancellationToken cancellationToken = default);
}
