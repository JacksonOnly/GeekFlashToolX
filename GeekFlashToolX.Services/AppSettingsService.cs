using System.Text.Json;
using GeekFlashToolX.Core.Models;
using GeekFlashToolX.Core.Services;
using GeekFlashToolX.Services.Serialization;

namespace GeekFlashToolX.Services;

public sealed class AppSettingsService : IAppSettingsService
{
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    public AppSettingsService(string? settingsFilePath = null)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        SettingsFilePath = settingsFilePath ?? Path.Combine(appData, "GeekFlashTool-X", "settings.json");
    }

    public AppSettings Current { get; private set; } = new();

    public string SettingsFilePath { get; }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(SettingsFilePath))
        {
            return;
        }

        await _fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var stream = File.OpenRead(SettingsFilePath);
            Current = await JsonSerializer.DeserializeAsync(stream, AppJsonContext.Default.AppSettings, cancellationToken)
                .ConfigureAwait(false) ?? new AppSettings();
        }
        catch (JsonException)
        {
            Current = new AppSettings();
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        await _fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var directory = Path.GetDirectoryName(SettingsFilePath)!;
            Directory.CreateDirectory(directory);
            var temporaryPath = SettingsFilePath + ".tmp";
            await using (var stream = File.Create(temporaryPath))
            {
                await JsonSerializer.SerializeAsync(stream, Current, AppJsonContext.Default.AppSettings, cancellationToken)
                    .ConfigureAwait(false);
            }

            File.Move(temporaryPath, SettingsFilePath, true);
        }
        finally
        {
            _fileLock.Release();
        }
    }
}
