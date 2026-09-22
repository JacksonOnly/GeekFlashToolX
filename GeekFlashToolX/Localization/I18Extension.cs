using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Metadata;
using Avalonia.Threading;
using GeekFlashToolX.Core.Services;
using System.Globalization;

[assembly: XmlnsDefinition("https://github.com/avaloniaui", "GeekFlashToolX.Localization")]

namespace GeekFlashToolX.Localization;

/// <summary>
/// Creates a live, one-way localization binding: <c>{I18 Home.Title}</c>.
/// A single composite-format argument can be supplied with the <c>Format</c>
/// property, including a binding: <c>{I18 Logs.Count, Arg1={Binding Count}}</c>.
/// </summary>
public sealed class I18Extension
{
    public I18Extension(object key) => Key = key;

    /// <summary>Translation key, optionally supplied as a binding.</summary>
    public object? Key { get; set; }

    /// <summary>Composite-format arguments, in order.</summary>
    public object? Arg1 { get; set; }
    public object? Arg2 { get; set; }
    public object? Arg3 { get; set; }
    public object? Arg4 { get; set; }
    public object? Arg5 { get; set; }
    public object? Arg6 { get; set; }
    public object? Arg7 { get; set; }
    public object? Arg8 { get; set; }

    public BindingBase ProvideValue()
    {
        var key = Key;
        if (key is null) throw new ArgumentException("A translation key is required.", nameof(Key));
        var localization = App.Services?.GetService(typeof(ILocalizationService)) as ILocalizationService;
        if (localization is null && !Design.IsDesignMode)
            throw new InvalidOperationException("Register ILocalizationService before loading localized views.");

        var keyBinding = key as BindingBase;
        var arguments = new object?[] { Arg1, Arg2, Arg3, Arg4, Arg5, Arg6, Arg7, Arg8 };
        var argumentCount = arguments.TakeWhile(static value => value is not null).Count();
        if (keyBinding is not null || argumentCount > 0)
        {
            var formatted = new MultiBinding
            {
                Converter = new DynamicFormatConverter(localization),
            };
            formatted.Bindings.Add(new CultureObservable(localization).ToBinding());
            formatted.Bindings.Add(keyBinding ?? new Binding { Source = key });
            foreach (var argument in arguments.Take(argumentCount))
            {
                formatted.Bindings.Add(argument is BindingBase binding
                    ? binding
                    : new Binding { Source = argument });
            }
            return formatted;
        }

        if (argumentCount > 0)
            return new FormattedTranslationObservable(localization, (string)key, arguments[..argumentCount]).ToBinding();

        // Subscribe per binding, so Avalonia releases the handler when the binding is disposed.
        // No reflection or DataContext dependency: this also works inside item templates.
        return new TranslationObservable(localization, (string)key).ToBinding();
    }

    private sealed class FormattedTranslationObservable(
        ILocalizationService? localization,
        string key,
        object?[] arguments) : IObservable<string>
    {
        public IDisposable Subscribe(IObserver<string> observer) =>
            new FormattedSubscription(localization, key, arguments, observer);
    }

    private sealed class TranslationObservable(ILocalizationService? localization, string key) : IObservable<string>
    {
        public IDisposable Subscribe(IObserver<string> observer) => new Subscription(localization, key, observer);
    }

    private sealed class Subscription : IDisposable
    {
        private readonly ILocalizationService? _localization;
        private readonly string _key;
        private IObserver<string>? _observer;

        public Subscription(ILocalizationService? localization, string key, IObserver<string> observer)
        {
            _localization = localization;
            _key = key;
            _observer = observer;
            if (_localization is not null) _localization.CultureChanged += OnCultureChanged;
            OnCultureChanged(null, EventArgs.Empty);
        }

        private void OnCultureChanged(object? sender, EventArgs args)
        {
            if (Dispatcher.UIThread.CheckAccess()) Publish();
            else Dispatcher.UIThread.Post(Publish);
        }

        private void Publish() => Volatile.Read(ref _observer)?.OnNext(_localization?.String(_key) ?? _key);

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _observer, null) is null) return;
            if (_localization is not null) _localization.CultureChanged -= OnCultureChanged;
        }
    }

    private sealed class FormattedSubscription : IDisposable
    {
        private readonly ILocalizationService? _localization;
        private readonly string _key;
        private readonly object?[] _arguments;
        private IObserver<string>? _observer;

        public FormattedSubscription(
            ILocalizationService? localization,
            string key,
            object?[] arguments,
            IObserver<string> observer)
        {
            _localization = localization;
            _key = key;
            _arguments = arguments;
            _observer = observer;
            if (_localization is not null) _localization.CultureChanged += OnCultureChanged;
            OnCultureChanged(null, EventArgs.Empty);
        }

        private void OnCultureChanged(object? sender, EventArgs args)
        {
            if (Dispatcher.UIThread.CheckAccess()) Publish();
            else Dispatcher.UIThread.Post(Publish);
        }

        private void Publish()
        {
            var observer = Volatile.Read(ref _observer);
            if (observer is null) return;

            var value = _localization?.String(_key) ?? _key;
            observer.OnNext(FormatValue(value, _arguments));
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _observer, null) is null) return;
            if (_localization is not null) _localization.CultureChanged -= OnCultureChanged;
        }
    }

    private sealed class CultureObservable(ILocalizationService? localization) : IObservable<string>
    {
        public IDisposable Subscribe(IObserver<string> observer) => new CultureSubscription(localization, observer);
    }

    private sealed class CultureSubscription : IDisposable
    {
        private readonly ILocalizationService? _localization;
        private IObserver<string>? _observer;

        public CultureSubscription(ILocalizationService? localization, IObserver<string> observer)
        {
            _localization = localization;
            _observer = observer;
            if (_localization is not null) _localization.CultureChanged += OnCultureChanged;
            OnCultureChanged(null, EventArgs.Empty);
        }

        private void OnCultureChanged(object? sender, EventArgs args)
        {
            void Publish() => Volatile.Read(ref _observer)?.OnNext(_localization?.CurrentLanguageCode ?? string.Empty);
            if (Dispatcher.UIThread.CheckAccess()) Publish(); else Dispatcher.UIThread.Post(Publish);
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _observer, null) is null) return;
            if (_localization is not null) _localization.CultureChanged -= OnCultureChanged;
        }
    }

    private sealed class DynamicFormatConverter(ILocalizationService? localization) : IMultiValueConverter
    {
        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Count < 2 || values[1] is not string key) return AvaloniaProperty.UnsetValue;
            var format = localization?.String(key) ?? key;
            return FormatValue(format, values.Skip(2).ToArray());
        }

        public object? ConvertBack(IList<object?> values, Type[] targetTypes, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    private static string FormatValue(string format, object?[] arguments)
    {
        try
        {
            return string.Format(CultureInfo.CurrentCulture, format, arguments);
        }
        catch (FormatException)
        {
            // Keep a useful translation visible if a resource has an invalid format string.
            return format;
        }
    }
}
