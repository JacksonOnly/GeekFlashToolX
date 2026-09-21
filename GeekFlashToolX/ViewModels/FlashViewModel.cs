using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Threading;
using GeekFlashCore.Protocol.Qcom;
using GeekFlashCore.UsbWatcher.Abstractions;
using GeekFlashCore.UsbWatcher.Extensions;
using GeekFlashToolX.Core.Services;
using GeekFlashToolX.Services;
using GeekFlashToolX.Views.Page;
using ReactiveUI;

namespace GeekFlashToolX.ViewModels;

public sealed record FlashUsbDevice(UsbDeviceInfo Info, bool IsSupported)
{
    public string Label => Info.FriendlyName ?? Info.Description ?? Info.HardwareId ?? "USB 设备";
}

public sealed class FlashViewModel : ViewModelBase
{
    private static readonly QcomDeviceIdentify QcomIdentify = new();
    private readonly IUsbDeviceMonitor? _monitor;
    private readonly IUsbDeviceEnumerator? _enumerator;
    private readonly IQcomDeviceConnector _connector;
    private readonly List<DevicePageRule> _rules = [];
    private readonly Dictionary<(string DeviceId, string RuleId), FlashTabItemViewModel> _deviceTabs = [];
    private readonly HashSet<string> _dismissedDeviceIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private Task _monitorWork = Task.CompletedTask;
    private FlashTabItemViewModel? _selectedTab;
    private FlashUsbDevice? _selectedDevice;
    private string _deviceKeyword = "";
    private ConnectionTutorial? _selectedTutorial;
    private bool _isUsbMonitoring;
    private bool _isRefreshingDevices;
    private bool _isMonitoringBusy;
    private string _deviceListStatus = "展开列表时扫描 USB 设备";
    private string? _monitoringError;
    private int _deviceGeneration;
    private bool _disposed;

    public FlashViewModel(ILocalizationService localization) : this(localization, null, null) { }

    public FlashViewModel(ILocalizationService localization, IUsbDeviceMonitor? monitor,
        IUsbDeviceEnumerator? enumerator, IQcomDeviceConnector? connector = null) : base(localization)
    {
        _monitor = monitor;
        _enumerator = enumerator;
        _connector = connector ?? new QcomDeviceConnector();
        _isUsbMonitoring = monitor is not null;
        TutorialModes = CreateTutorials();
        _selectedTutorial = TutorialModes[0];
        var mainTab = new FlashTabItemViewModel("刷机", new FlashOperationPage { DataContext = this }, isClosable: false);
        Tabs.Add(mainTab);
        _selectedTab = mainTab;
        RegisterDevicePage<QcomDeviceConnectionViewModel, QcomDeviceConnectionView>(
            "qcom-edl", IsConnectable,
            device => new QcomDeviceConnectionViewModel(device, _connector),
            device => $"EDL · {device.ExtractPortName()}");

        if (_monitor is not null)
        {
            _monitor.DeviceAdded += OnDeviceAdded;
            _monitor.DeviceRemoved += OnDeviceRemoved;
        }
    }

    public ObservableCollection<FlashTabItemViewModel> Tabs { get; } = [];
    public ObservableCollection<FlashUsbDevice> Devices { get; } = [];
    public ObservableCollection<FlashUsbDevice> FilteredDevices { get; } = [];
    public IReadOnlyList<ConnectionTutorial> TutorialModes { get; }
    public FlashTabItemViewModel? SelectedTab { get => _selectedTab; set => this.RaiseAndSetIfChanged(ref _selectedTab, value); }
    public FlashUsbDevice? SelectedDevice
    {
        get => _selectedDevice;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedDevice, value);
            this.RaisePropertyChanged(nameof(CanConnectSelectedDevice));
        }
    }
    public bool CanConnectSelectedDevice => SelectedDevice is { IsSupported: true } selected &&
        string.Equals(DeviceKeyword, selected.Label, StringComparison.Ordinal);
    public string DeviceKeyword
    {
        get => _deviceKeyword;
        set
        {
            this.RaiseAndSetIfChanged(ref _deviceKeyword, value);
            FilterDeviceList();
            this.RaisePropertyChanged(nameof(CanConnectSelectedDevice));
        }
    }
    public ConnectionTutorial? SelectedTutorial { get => _selectedTutorial; set => this.RaiseAndSetIfChanged(ref _selectedTutorial, value); }
    public bool IsRefreshingDevices { get => _isRefreshingDevices; private set => this.RaiseAndSetIfChanged(ref _isRefreshingDevices, value); }
    public string DeviceListStatus { get => _deviceListStatus; private set => this.RaiseAndSetIfChanged(ref _deviceListStatus, value); }
    public string MonitoringStatus => _monitoringError ?? (_isMonitoringBusy ? "切换中…" : _monitor?.IsMonitoring == true ? "监听中" : "已暂停");
    public bool CanToggleMonitoring => _monitor is not null;

    public bool IsUsbMonitoring
    {
        get => _isUsbMonitoring;
        set
        {
            if (_isUsbMonitoring == value) return;
            this.RaiseAndSetIfChanged(ref _isUsbMonitoring, value);
            QueueMonitoringUpdate();
        }
    }

    public void RegisterDevicePage<TViewModel, TView>(string ruleId, Func<UsbDeviceInfo, bool> supports,
        Func<UsbDeviceInfo, TViewModel> createViewModel, Func<UsbDeviceInfo, string> title)
        where TViewModel : class where TView : Control, new()
    {
        Dispatcher.UIThread.VerifyAccess();
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleId);
        ArgumentNullException.ThrowIfNull(supports);
        ArgumentNullException.ThrowIfNull(createViewModel);
        ArgumentNullException.ThrowIfNull(title);
        if (_rules.Any(rule => rule.Id == ruleId))
            throw new InvalidOperationException($"Device page rule '{ruleId}' is already registered.");
        ViewLocator.Instance.EnsureView<TViewModel, TView>();
        var rule = new DevicePageRule(ruleId, supports,
            device => createViewModel(device) ?? throw new InvalidOperationException("Device page factory returned null."), title);
        _rules.Add(rule);
        foreach (var device in Devices.Select(item => item.Info)) AddDevicePage(device, rule);
    }

    public void StartMonitoring()
    {
        Dispatcher.UIThread.VerifyAccess();
        ObjectDisposedException.ThrowIf(_disposed, this);
        QueueMonitoringUpdate();
    }

    private void QueueMonitoringUpdate()
    {
        if (_monitor is null || _disposed) return;
        _monitorWork = ApplyMonitoringAsync(_monitorWork);
    }

    private async Task ApplyMonitoringAsync(Task previous)
    {
        await previous;
        if (_monitor is null || _disposed) return;
        var requested = _isUsbMonitoring;
        _isMonitoringBusy = true;
        this.RaisePropertyChanged(nameof(MonitoringStatus));
        try
        {
            await Task.Run(() =>
            {
                if (requested && !_monitor.IsMonitoring) _monitor.StartMonitoring();
                else if (!requested && _monitor.IsMonitoring) _monitor.StopMonitoring();
            });
            if (_disposed) return;
            _monitoringError = null;
            if (requested) await RefreshDevicesAsync();
        }
        catch (Exception exception)
        {
            if (_disposed) return;
            _monitoringError = exception.Message;
            this.RaiseAndSetIfChanged(ref _isUsbMonitoring, _monitor.IsMonitoring, nameof(IsUsbMonitoring));
            Serilog.Log.Warning(exception, "USB monitoring could not be changed.");
        }
        finally
        {
            if (!_disposed)
            {
                _isMonitoringBusy = false;
                this.RaisePropertyChanged(nameof(MonitoringStatus));
            }
        }
    }

    public async Task RefreshDevicesAsync()
    {
        Dispatcher.UIThread.VerifyAccess();
        if (_disposed || _enumerator is null || !await _refreshGate.WaitAsync(0)) return;
        IsRefreshingDevices = true;
        DeviceListStatus = "正在扫描 USB 设备…";
        var generation = _deviceGeneration;
        try
        {
            var snapshot = await Task.Run(() => _enumerator.GetDevices().ToArray(), _lifetime.Token);
            if (_disposed) return;
            if (generation != _deviceGeneration)
            {
                DeviceListStatus = "设备已变化，正在重新扫描…";
                return;
            }
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var device in snapshot)
            {
                if (string.IsNullOrWhiteSpace(device.HardwareId)) continue;
                ids.Add(device.HardwareId);
                UpsertDevice(device);
                foreach (var rule in _rules)
                    if (AddDevicePage(device, rule) is { } tab && ReferenceEquals(SelectedTab, Tabs[0])) SelectedTab = tab;
            }
            foreach (var old in Devices.Where(item => !ids.Contains(item.Info.HardwareId ?? "")).ToArray())
                DeviceDisconnected(old.Info);
            FilterDeviceList();
            DeviceListStatus = Devices.Count == 0 ? "未发现 USB 设备" : $"已发现 {Devices.Count} 个 USB 设备";
        }
        catch (OperationCanceledException) when (_disposed) { }
        catch (Exception exception)
        {
            DeviceListStatus = $"读取设备失败：{exception.Message}";
            Serilog.Log.Warning(exception, "USB device enumeration failed.");
        }
        finally
        {
            if (!_disposed) IsRefreshingDevices = false;
            _refreshGate.Release();
            if (_disposed) ReleaseRefreshResources();
            else if (generation != _deviceGeneration) _ = RefreshDevicesAsync();
        }
    }

    public void DeviceConnected(UsbDeviceInfo device)
    {
        Dispatcher.UIThread.VerifyAccess();
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(device);
        _deviceGeneration++;
        UpsertDevice(device);
        FilterDeviceList();
        foreach (var rule in _rules)
            if (AddDevicePage(device, rule) is { } tab) SelectedTab = tab;
        DeviceListStatus = $"已发现 {Devices.Count} 个 USB 设备";
    }

    public void DeviceDisconnected(UsbDeviceInfo device)
    {
        Dispatcher.UIThread.VerifyAccess();
        if (_disposed) return;
        ArgumentNullException.ThrowIfNull(device);
        var id = device.HardwareId;
        if (string.IsNullOrWhiteSpace(id)) return;
        _deviceGeneration++;
        _dismissedDeviceIds.Remove(id);
        var item = Devices.FirstOrDefault(item => SameDevice(item.Info, device));
        if (item is not null)
        {
            if (ReferenceEquals(SelectedDevice, item)) SelectedDevice = null;
            Devices.Remove(item);
        }
        FilterDeviceList();
        foreach (var key in _deviceTabs.Keys.Where(key => key.DeviceId.Equals(id, StringComparison.OrdinalIgnoreCase)).ToArray())
            RemoveDeviceTab(key);
        DeviceListStatus = Devices.Count == 0 ? "未发现 USB 设备" : $"已发现 {Devices.Count} 个 USB 设备";
    }

    public override void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _lifetime.Cancel();
        if (_monitor is not null)
        {
            _monitor.DeviceAdded -= OnDeviceAdded;
            _monitor.DeviceRemoved -= OnDeviceRemoved;
            var monitorWork = _monitorWork;
            var monitor = _monitor;
            _ = Task.Run(async () =>
            {
                try
                {
                    await monitorWork.ConfigureAwait(false);
                    if (monitor.IsMonitoring) monitor.StopMonitoring();
                    (monitor as IDisposable)?.Dispose();
                }
                catch (Exception exception) { Serilog.Log.Warning(exception, "USB monitoring cleanup failed."); }
            });
        }
        foreach (var tab in Tabs.Skip(1))
        {
            if (tab.Content is QcomDeviceConnectionViewModel qcom)
            {
                qcom.CloseRequested -= OnDevicePageCloseRequested;
                qcom.PropertyChanged -= OnDevicePagePropertyChanged;
            }
            ViewLocator.Instance.Release(tab.Content);
            (tab.Content as IDisposable)?.Dispose();
        }
        if (Tabs.Count > 0 && Tabs[0].Content is Control operationPage)
            operationPage.DataContext = null;
        Tabs.Clear();
        _deviceTabs.Clear();
        FilteredDevices.Clear();
        if (!IsRefreshingDevices) ReleaseRefreshResources();
        base.Dispose();
    }

    public void OpenSelectedDevice(string protocol)
    {
        Dispatcher.UIThread.VerifyAccess();
        if (!string.Equals(protocol, "Qualcomm", StringComparison.Ordinal)) return;
        if (SelectedDevice is not { IsSupported: true } selected)
        {
            DeviceListStatus = "请选择支持连接的 Qualcomm EDL 设备";
            return;
        }
        _dismissedDeviceIds.Remove(selected.Info.HardwareId!);
        foreach (var rule in _rules) AddDevicePage(selected.Info, rule);
        var key = (selected.Info.HardwareId!.ToUpperInvariant(), "qcom-edl");
        if (_deviceTabs.TryGetValue(key, out var tab)) SelectedTab = tab;
    }

    public async Task CloseTabAsync(FlashTabItemViewModel tab)
    {
        Dispatcher.UIThread.VerifyAccess();
        if (_disposed || !tab.IsClosable || !tab.CanClose || !Tabs.Contains(tab)) return;
        if (tab.Content is QcomDeviceConnectionViewModel qcom)
        {
            if (qcom.IsBusy) return;
            await qcom.CloseAsync();
            return;
        }
        var entry = _deviceTabs.FirstOrDefault(pair => ReferenceEquals(pair.Value, tab));
        if (entry.Key != default)
        {
            _dismissedDeviceIds.Add(entry.Key.DeviceId);
            RemoveDeviceTab(entry.Key);
        }
    }

    private void FilterDeviceList()
    {
        var keyword = DeviceKeyword.Trim();
        var matches = Devices.Where(item => keyword.Length == 0 ||
            item.Label.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
            (item.Info.HardwareId?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false)).ToArray();
        FilteredDevices.Clear();
        foreach (var item in matches) FilteredDevices.Add(item);
    }

    private void OnDevicePagePropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(QcomDeviceConnectionViewModel.IsBusy) ||
            sender is not QcomDeviceConnectionViewModel model) return;
        var tab = _deviceTabs.Values.FirstOrDefault(item => ReferenceEquals(item.Content, model));
        if (tab is not null) tab.CanClose = !model.IsBusy;
    }

    private void OnDevicePageCloseRequested(object? sender, EventArgs args)
    {
        if (sender is not QcomDeviceConnectionViewModel model || _disposed) return;
        var key = _deviceTabs.FirstOrDefault(pair => ReferenceEquals(pair.Value.Content, model)).Key;
        if (key == default) return;
        _dismissedDeviceIds.Add(key.DeviceId);
        RemoveDeviceTab(key);
    }

    private void RemoveDeviceTab((string DeviceId, string RuleId) key)
    {
        if (!_deviceTabs.Remove(key, out var tab)) return;
        if (ReferenceEquals(SelectedTab, tab)) SelectedTab = Tabs[0];
        Tabs.Remove(tab);
        if (tab.Content is QcomDeviceConnectionViewModel qcom)
        {
            qcom.CloseRequested -= OnDevicePageCloseRequested;
            qcom.PropertyChanged -= OnDevicePagePropertyChanged;
        }
        ViewLocator.Instance.Release(tab.Content);
        (tab.Content as IDisposable)?.Dispose();
    }

    private void UpsertDevice(UsbDeviceInfo device)
    {
        if (string.IsNullOrWhiteSpace(device.HardwareId)) return;
        var item = new FlashUsbDevice(device, IsConnectable(device));
        var old = Devices.FirstOrDefault(existing => SameDevice(existing.Info, device));
        if (old is null) Devices.Add(item);
        else if (old.Info.FriendlyName != device.FriendlyName ||
                 old.Info.Description != device.Description ||
                 old.Info.HardwareId != device.HardwareId ||
                 old.Info.VendorId != device.VendorId ||
                 old.Info.ProductId != device.ProductId ||
                 old.IsSupported != item.IsSupported)
        {
            var index = Devices.IndexOf(old);
            Devices[index] = item;
            if (ReferenceEquals(SelectedDevice, old)) SelectedDevice = item;
        }
    }

    private static bool IsConnectable(UsbDeviceInfo device) =>
        QcomIdentify.Identify(device).IsSuccess && device.ExtractPortName() is not null;

    private void ReleaseRefreshResources()
    {
        _refreshGate.Dispose();
        _lifetime.Dispose();
    }

    private static bool SameDevice(UsbDeviceInfo first, UsbDeviceInfo second) =>
        string.Equals(first.HardwareId, second.HardwareId, StringComparison.OrdinalIgnoreCase);

    private FlashTabItemViewModel? AddDevicePage(UsbDeviceInfo device, DevicePageRule rule)
    {
        var id = device.HardwareId;
        if (string.IsNullOrWhiteSpace(id) || _dismissedDeviceIds.Contains(id) || !rule.Supports(device)) return null;
        var key = (id.ToUpperInvariant(), rule.Id);
        if (_deviceTabs.ContainsKey(key)) return null;
        var tab = new FlashTabItemViewModel(rule.Title(device), rule.CreateViewModel(device), isClosable: true);
        if (tab.Content is QcomDeviceConnectionViewModel qcom)
        {
            tab.CanClose = !qcom.IsBusy;
            qcom.CloseRequested += OnDevicePageCloseRequested;
            qcom.PropertyChanged += OnDevicePagePropertyChanged;
        }
        _deviceTabs.Add(key, tab);
        Tabs.Add(tab);
        return tab;
    }

    private void OnDeviceAdded(object? sender, UsbDeviceEventArgs args) =>
        Dispatcher.UIThread.Post(() =>
        {
            if (_disposed || !IsUsbMonitoring) return;
            try { DeviceConnected(args.Device); }
            catch (Exception exception) { Serilog.Log.Warning(exception, "Could not add a USB device page."); }
        });

    private void OnDeviceRemoved(object? sender, UsbDeviceEventArgs args) =>
        Dispatcher.UIThread.Post(() =>
        {
            if (_disposed) return;
            try { DeviceDisconnected(args.Device); }
            catch (Exception exception) { Serilog.Log.Warning(exception, "Could not remove a USB device page."); }
        });

    private static IReadOnlyList<ConnectionTutorial> CreateTutorials() =>
    [
        new("ADB 模式", "适用于系统已启动且已开启 USB 调试的设备。", [
            new("01", "开启开发者选项", "在手机设置中启用 USB 调试。"),
            new("02", "连接数据线", "使用支持数据传输的 USB 线连接电脑。"),
            new("03", "确认授权", "在手机上允许当前电脑进行调试。")]),
        new("Fastboot 模式", "适用于已进入 Bootloader / Fastboot 的设备。", [
            new("01", "进入 Fastboot", "关机后使用设备对应的组合键进入。"),
            new("02", "连接电脑", "保持模式界面并连接 USB 数据线。"),
            new("03", "检查设备", "等待工具显示已连接状态。")]),
        new("Recovery 模式", "适用于已进入第三方或官方 Recovery 的设备。", [
            new("01", "进入 Recovery", "按 Recovery 提示开启 ADB 或侧载模式。"),
            new("02", "连接电脑", "使用支持数据传输的 USB 线连接电脑。"),
            new("03", "检查设备", "等待工具显示已连接状态。")])
    ];

    private sealed record DevicePageRule(string Id, Func<UsbDeviceInfo, bool> Supports,
        Func<UsbDeviceInfo, object> CreateViewModel, Func<UsbDeviceInfo, string> Title);
}

public sealed class FlashTabItemViewModel(string title, object content, bool isClosable = false) : ReactiveObject
{
    private bool _canClose = true;
    public string Title { get; } = title;
    public object Content { get; } = content;
    public bool IsClosable { get; } = isClosable;
    public bool CanClose { get => _canClose; set => this.RaiseAndSetIfChanged(ref _canClose, value); }
}
