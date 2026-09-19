using System.Diagnostics;
using GeekFlashToolX.Core.Services;

namespace GeekFlashToolX.Services;

public sealed class ExternalLauncher : IExternalLauncher
{
    public Task OpenUriAsync(Uri uri)
    {
        if (!uri.IsAbsoluteUri || uri.Scheme != Uri.UriSchemeHttps)
            throw new ArgumentException("Only absolute HTTPS URLs are supported.", nameof(uri));
        Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        return Task.CompletedTask;
    }

    public Task OpenFolderAsync(string path)
    {
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo(Path.GetFullPath(path)) { UseShellExecute = true });
        return Task.CompletedTask;
    }

    public Task OpenFileAsync(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath)) throw new FileNotFoundException("Log file was not found.", fullPath);
        Process.Start(new ProcessStartInfo(fullPath) { UseShellExecute = true });
        return Task.CompletedTask;
    }
}
