using System.Windows.Input;
using GeekFlashCore.UsbWatcher.Abstractions;
using GeekFlashCore.UsbWatcher.Extensions;
using GeekFlashToolX.Services;
using ReactiveUI;

namespace GeekFlashToolX.ViewModels;

/// <summary>One connection session per detected EDL device.</summary>
public sealed class QcomDeviceConnectionViewModel : ReactiveObject, IDisposable
{
    private readonly IQcomDeviceConnector _connector;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private IQcomDeviceSession? _session;
    private string _loaderPath = "";
    private string _status = "等待探测";
    private string _details = "";
    private bool _isBusy;
    private bool _disposed;

    public QcomDeviceConnectionViewModel(UsbDeviceInfo device, IQcomDeviceConnector connector)
    {
        Device = device;
        _connector = connector;
        ProbeCommand = ReactiveCommand.CreateFromTask(ProbeAsync);
        ConnectCommand = ReactiveCommand.CreateFromTask(ConnectAsync);
        CloseCommand = ReactiveCommand.CreateFromTask(CloseAsync);
    }

    public UsbDeviceInfo Device { get; }
    public string DeviceName => Device.FriendlyName ?? Device.Description ?? "Qualcomm EDL device";
    public string PortName => Device.ExtractPortName() ?? "无 COM 端口";
    public string HardwareId => Device.HardwareId ?? "未知硬件 ID";
    public bool IsConnected => _session is not null;
    public bool CanProbe => !IsBusy && !IsConnected;
    public bool CanConnect => !IsBusy && !IsConnected;
    public bool CanClose => !IsBusy;
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            this.RaiseAndSetIfChanged(ref _isBusy, value);
            RaiseConnectionActionsChanged();
        }
    }
    public string LoaderPath { get => _loaderPath; set => this.RaiseAndSetIfChanged(ref _loaderPath, value); }
    public string Status { get => _status; private set => this.RaiseAndSetIfChanged(ref _status, value); }
    public string Details { get => _details; private set => this.RaiseAndSetIfChanged(ref _details, value); }
    public ICommand ProbeCommand { get; }
    public ICommand ConnectCommand { get; }
    public ICommand CloseCommand { get; }
    public event EventHandler? CloseRequested;

    private async Task ProbeAsync() => await RunOperationAsync(async token =>
    {
        Status = "正在探测 Sahara…";
        var details = await _connector.ProbeAsync(Device, token);
        token.ThrowIfCancellationRequested();
        Details = details;
        Status = "探测完成";
    });

    private async Task ConnectAsync() => await RunOperationAsync(async token =>
    {
        if (_session is not null) return;
        if (string.IsNullOrWhiteSpace(LoaderPath))
        {
            Status = "请先选择匹配设备的 Firehose Loader";
            return;
        }
        Status = "正在建立 EDL 联机…";
        var session = await _connector.ConnectAsync(Device, LoaderPath, token);
        if (_disposed || token.IsCancellationRequested)
        {
            session.Dispose();
            return;
        }
        _session = session;
        Details = _session.Summary;
        Status = "已连接";
        this.RaisePropertyChanged(nameof(IsConnected));
        RaiseConnectionActionsChanged();
    });

    private async Task DisconnectAsync() => await RunOperationAsync(async token =>
    {
        var session = _session;
        if (session is null) return;
        _session = null;
        this.RaisePropertyChanged(nameof(IsConnected));
        RaiseConnectionActionsChanged();
        Status = "正在断开…";
        await session.DisconnectAsync(token);
        Status = "已断开";
    });

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
            Serilog.Log.Warning(exception, "EDL connection operation failed.");
        }
        finally
        {
            if (!_disposed) IsBusy = false;
            _operationGate.Release();
            if (_disposed) ReleaseOperationResources();
        }
    }

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
        (ProbeCommand as IDisposable)?.Dispose();
        (ConnectCommand as IDisposable)?.Dispose();
        (CloseCommand as IDisposable)?.Dispose();
        if (!IsBusy) ReleaseOperationResources();
    }

    private void ReleaseOperationResources()
    {
        _operationGate.Dispose();
        _lifetime.Dispose();
    }

    private void RaiseConnectionActionsChanged()
    {
        this.RaisePropertyChanged(nameof(CanProbe));
        this.RaisePropertyChanged(nameof(CanConnect));
        this.RaisePropertyChanged(nameof(CanClose));
    }
}
