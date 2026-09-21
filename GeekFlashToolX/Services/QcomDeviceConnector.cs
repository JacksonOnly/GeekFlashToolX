using GeekFlashCore.Protocol.Abstractions;
using GeekFlashCore.Protocol.Qcom;
using GeekFlashCore.Protocol.Qcom.Abstractions;
using GeekFlashCore.Transport.SerialPort;
using GeekFlashCore.UsbWatcher.Abstractions;
using GeekFlashCore.UsbWatcher.Extensions;

namespace GeekFlashToolX.Services;

public interface IQcomDeviceConnector
{
    Task<string> ProbeAsync(UsbDeviceInfo device, CancellationToken cancellationToken);
    Task<IQcomDeviceSession> ConnectAsync(UsbDeviceInfo device, string loaderPath, CancellationToken cancellationToken);
}

public interface IQcomDeviceSession : IDisposable
{
    string Summary { get; }
    Task DisconnectAsync(CancellationToken cancellationToken);
}

/// <summary>Uses the same EDL identification, COM transport and QcomProtocol sequence as the Core CLI.</summary>
public sealed class QcomDeviceConnector : IQcomDeviceConnector
{
    public async Task<string> ProbeAsync(UsbDeviceInfo device, CancellationToken cancellationToken)
    {
        using var protocol = CreateProtocol(device, null);
        var target = await Task.Run(() => protocol.ProbeSahara(), cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return $"Sahara v{target.Version} · 模式 {target.Mode} · 最大包 {target.MaximumPacketSizeSupported} B";
    }

    public async Task<IQcomDeviceSession> ConnectAsync(
        UsbDeviceInfo device, string loaderPath, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(loaderPath);
        var path = Path.GetFullPath(loaderPath.Trim().Trim('"'));
        if (!File.Exists(path)) throw new FileNotFoundException("找不到 Firehose Loader。", path);
        var protocol = CreateProtocol(device, new FileSaharaImageProvider(path));
        try
        {
            // DetectProtocol and serial I/O are synchronous inside ConnectAsync.
            await Task.Run(() => protocol.ConnectAsync(ct: cancellationToken), cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            var target = protocol.TargetInfo;
            var summary = $"已连接 · {target?.Vendor} · {target?.SocName ?? "未知 SoC"}";
            return new Session(protocol, summary);
        }
        catch
        {
            protocol.Dispose();
            throw;
        }
    }

    private static QcomProtocol CreateProtocol(UsbDeviceInfo device, ISaharaImageProvider? imageProvider)
    {
        ArgumentNullException.ThrowIfNull(device);
        if (!new QcomDeviceIdentify().Identify(device).IsSuccess)
            throw new NotSupportedException("当前设备不是 Qualcomm EDL 9008。");
        var port = device.ExtractPortName() ?? throw new InvalidOperationException("设备没有可用的 COM 端口。");
        var transport = SerialPortTransportFactory.Create(port);
        try { return new QcomProtocol(transport, imageProvider: imageProvider); }
        catch { transport.Dispose(); throw; }
    }

    private sealed class Session(QcomProtocol protocol, string summary) : IQcomDeviceSession
    {
        private int _disposed;
        public string Summary { get; } = summary;

        public async Task DisconnectAsync(CancellationToken cancellationToken)
        {
            if (Volatile.Read(ref _disposed) != 0) return;
            try { await Task.Run(() => protocol.DisconnectAsync(ct: cancellationToken), cancellationToken); }
            finally { Dispose(); }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            protocol.Dispose();
        }
    }

    private sealed class FileSaharaImageProvider(string path) : ISaharaImageProvider
    {
        public ValueTask<SaharaImageEntryResponse> ResolveAsync(
            SaharaImageEntryRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var source = new FileDataSource(path);
            return ValueTask.FromResult(new SaharaImageEntryResponse([new SaharaImageEntry(13, source.Length, source)]));
        }
    }

    private sealed class FileDataSource(string path) : IDataSource
    {
        private readonly string _path = Path.GetFullPath(path);
        public long Length => new FileInfo(_path).Length;
        public Stream OpenStream() => File.Open(_path, FileMode.Open, FileAccess.Read, FileShare.Read);
        public ValueTask<Stream> OpenStreamAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(OpenStream());
        }
    }
}
