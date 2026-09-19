using System.Globalization;
using GeekFlashToolX.Core.Models;
using GeekFlashToolX.Core.Services;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Context;
using Serilog.Core;
using Serilog.Events;

namespace GeekFlashToolX.Services;

/// <summary>Bridges both Core logging APIs without making the GUI contracts depend on either package.</summary>
public static class CoreLogAdapter
{
    public const string OperationIdProperty = "WorkOperationId";

    /// <summary>Configure once at startup to route Core's static Serilog.Log through operation scopes.</summary>
    public static Logger CreateApplicationLogger(ILogService logs) => new LoggerConfiguration()
        .MinimumLevel.Verbose().Enrich.FromLogContext().WriteTo.Sink(new ApplicationSink(logs)).CreateLogger();

    /// <summary>Enter on the calling async flow, and dispose in reverse order after awaited Core work.</summary>
    public static IDisposable EnterScope(IOperationLog operation) => LogContext.PushProperty(OperationIdProperty, operation.Id);

    public static Logger CreateLogger(IOperationLog operation) => new LoggerConfiguration()
        .MinimumLevel.Verbose().WriteTo.Sink(new OperationSink(operation)).CreateLogger();

    /// <summary>The caller owns the factory; disposing it does not complete the operation.</summary>
    public static ILoggerFactory CreateLoggerFactory(IOperationLog operation) => LoggerFactory.Create(builder =>
        builder.SetMinimumLevel(LogLevel.Trace).AddProvider(new OperationLoggerProvider(operation)));

    private sealed class ApplicationSink(ILogService logs) : ILogEventSink
    {
        public void Emit(LogEvent logEvent)
        {
            var id = logEvent.Properties.TryGetValue(OperationIdProperty, out var property) && property is ScalarValue { Value: string value }
                ? value : null;
            logs.Write((WorkLogLevel)logEvent.Level, logEvent.RenderMessage(CultureInfo.InvariantCulture), logEvent.Exception, id);
        }
    }

    private sealed class OperationSink(IOperationLog operation) : ILogEventSink
    {
        public void Emit(LogEvent logEvent) => operation.Write((WorkLogLevel)logEvent.Level,
            logEvent.RenderMessage(CultureInfo.InvariantCulture), logEvent.Exception);
    }

    private sealed class OperationLoggerProvider(IOperationLog operation) : ILoggerProvider
    {
        public Microsoft.Extensions.Logging.ILogger CreateLogger(string categoryName) => new OperationLogger(operation, categoryName);
        public void Dispose() { }
    }

    private sealed class OperationLogger(IOperationLog operation, string category) : Microsoft.Extensions.Logging.ILogger
    {
        private readonly LoggerExternalScopeProvider _scopes = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => _scopes.Push(state);
        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;
            var scopes = new List<string>();
            _scopes.ForEachScope((scope, items) => items.Add(scope?.ToString() ?? ""), scopes);
            operation.Write((WorkLogLevel)logLevel, $"[{category}] {string.Join(" / ", scopes)} {formatter(state, exception)}", exception);
        }
    }
}
