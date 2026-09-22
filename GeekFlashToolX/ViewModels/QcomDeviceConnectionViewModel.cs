using GeekFlashCore.UsbWatcher.Abstractions;
using GeekFlashCore.UsbWatcher.Extensions;
using GeekFlashToolX.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace GeekFlashToolX.ViewModels;

/// <summary>One connection session per detected EDL device.</summary>
public sealed partial class QcomDeviceConnectionViewModel : ObservableObject, IDisposable
{
    private readonly IQcomDeviceConnector _connector;
    private readonly IUiInteractionService? _ui;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private IQcomDeviceSession? _session;
    [ObservableProperty] private string _loaderPath = "";
    [ObservableProperty] private string _status = "等待探测";
    [ObservableProperty] private string _details = "";
    [ObservableProperty] private string _connectionLog = "等待设备操作。";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanProbe))]
    [NotifyPropertyChangedFor(nameof(CanConnect))]
    [NotifyPropertyChangedFor(nameof(CanClose))]
    private bool _isBusy;
    private bool _disposed;

    public QcomDeviceConnectionViewModel(UsbDeviceInfo device, IQcomDeviceConnector connector,
        IUiInteractionService? ui = null)
    {
        Device = device;
        _connector = connector;
        _ui = ui;
    }

    public UsbDeviceInfo Device { get; }
    public string DeviceName => Device.FriendlyName ?? Device.Description ?? "Qualcomm EDL device";
    public string PortName => Device.ExtractPortName() ?? "无 COM 端口";
    public string HardwareId => Device.HardwareId ?? "未知硬件 ID";
    public bool IsConnected => _session is not null;
    public bool CanProbe => !IsBusy && !IsConnected;
    public bool CanConnect => !IsBusy && !IsConnected;
    public bool CanClose => !IsBusy;
    public event EventHandler? CloseRequested;

    [RelayCommand]
    private async Task BrowseLoaderAsync()
    {
        if (_ui is null) return;
        if (await _ui.PickFirehoseLoaderAsync() is { } path) LoaderPath = path;
    }

    [RelayCommand]
    private async Task ProbeAsync() => await RunOperationAsync(async token =>
    {
        Status = "正在探测 Sahara…";
        AppendLog("开始探测 Sahara 协议");
        var details = await _connector.ProbeAsync(Device, token);
        token.ThrowIfCancellationRequested();
        Details = details;
        Status = "探测完成";
        AppendLog($"探测完成：{details}");
    });

    [RelayCommand]
    private async Task ConnectAsync() => await RunOperationAsync(async token =>
    {
        if (_session is not null) return;
        if (string.IsNullOrWhiteSpace(LoaderPath))
        {
            Status = "请先选择匹配设备的 Firehose Loader";
            AppendLog(Status);
            return;
        }
        Status = "正在建立 EDL 联机…";
        AppendLog("正在通过 Sahara 加载 Firehose Loader");
        var session = await _connector.ConnectAsync(Device, LoaderPath, token);
        if (_disposed || token.IsCancellationRequested)
        {
            session.Dispose();
            return;
        }
        _session = session;
        Details = _session.Summary;
        Status = "已连接";
        AppendLog($"设备已连接：{_session.Summary}");
        OnPropertyChanged(nameof(IsConnected));
        RaiseConnectionActionsChanged();
    });

    private async Task DisconnectAsync() => await RunOperationAsync(async token =>
    {
        var session = _session;
        if (session is null) return;
        _session = null;
        OnPropertyChanged(nameof(IsConnected));
        RaiseConnectionActionsChanged();
        Status = "正在断开…";
        AppendLog(Status);
        await session.DisconnectAsync(token);
        Status = "已断开";
        AppendLog(Status);
    });

    [RelayCommand]
    public async Task CloseAsync()
    {
        if (_disposed || IsBusy) return;
        await DisconnectAsync();
        if (!_disposed) CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private async Task RunOperationAsync(Func<CancellationToken, Task> action)
    {
        if (_disposed || !await _operationGate.WaitAsync(0)) return;
        IsBusy = true;
        try { await action(_lifetime.Token); }
        catch (OperationCanceledException) when (_disposed) { }
        catch (Exception exception)
        {
            Status = "操作失败";
            Details = exception.Message;
            AppendLog($"操作失败：{exception.Message}");
            Serilog.Log.Warning(exception, "EDL connection operation failed.");
        }
        finally
        {
            if (!_disposed) IsBusy = false;
            _operationGate.Release();
            if (_disposed) ReleaseOperationResources();
        }
    }

    private void AppendLog(string message) =>
        ConnectionLog += $"{Environment.NewLine}[{DateTime.Now:HH:mm:ss}] {message}";

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _lifetime.Cancel();
        var session = _session;
        if (session is not null)
            _ = Task.Run(() =>
            {
                try { session.Dispose(); }
                catch (Exception exception) { Serilog.Log.Warning(exception, "EDL connection cleanup failed."); }
            });
        _session = null;
        if (!IsBusy) ReleaseOperationResources();
    }

    private void ReleaseOperationResources()
    {
        _operationGate.Dispose();
        _lifetime.Dispose();
    }

    private void RaiseConnectionActionsChanged()
    {
        OnPropertyChanged(nameof(CanProbe));
        OnPropertyChanged(nameof(CanConnect));
        OnPropertyChanged(nameof(CanClose));
    }
}
