using System.Windows.Input;
using GeekFlashToolX.Core.Services;
using IconPacks.Avalonia.Codicons;
using ReactiveUI;

namespace GeekFlashToolX.ViewModels;

public sealed record HomeLink(string TitleKey, Uri Url)
{
    public string Host => Url.Host;
}

public sealed record HomeLinkGroup(
    string TitleKey,
    string DescriptionKey,
    PackIconCodiconsKind Icon,
    IReadOnlyList<HomeLink> Links);

public sealed class HomeViewModel : ViewModelBase
{
    private readonly IExternalLauncher _launcher;
    private string _error = string.Empty;

    public HomeViewModel(ILocalizationService localization, IExternalLauncher launcher) : base(localization)
    {
        _launcher = launcher;
        LinkGroups = CreateLinkGroups();
        OpenLinkCommand = ReactiveCommand.CreateFromTask<Uri>(OpenLinkAsync);
    }

    public string CurrentVersion => $"v{typeof(HomeViewModel).Assembly.GetName().Version?.ToString(3) ?? "1.0.0"}";
    public Uri RepositoryUrl => new(String("GIT_REPO_URL"));
    public IReadOnlyList<HomeLinkGroup> LinkGroups { get; private set; }
    public ICommand OpenLinkCommand { get; }
    public string Error
    {
        get => _error;
        private set
        {
            this.RaiseAndSetIfChanged(ref _error, value);
            this.RaisePropertyChanged(nameof(HasError));
        }
    }
    public bool HasError => !string.IsNullOrEmpty(Error);

    private async Task OpenLinkAsync(Uri uri)
    {
        Error = string.Empty;
        try { await _launcher.OpenUriAsync(uri); }
        catch (Exception ex) { Error = FormatString("Home.LinkError", ex.Message); }
    }

    protected override void OnLanguageChanged()
    {
        LinkGroups = CreateLinkGroups();
        base.OnLanguageChanged();
    }

    private IReadOnlyList<HomeLinkGroup> CreateLinkGroups() =>
    [
        new("Home.Links.Community", "Home.Links.Community.Description", PackIconCodiconsKind.CommentDiscussion,
        [
            Link("FeedbackGroup", "https://yhfx.jwznb.com/share?key=aF4Z7N53ru3c&ts=1757511926"),
            Link("QQGroup", "https://qm.qq.com/q/nREuTLYItq"),
            Link("CoolApk", "https://www.coolapk.com/u/37865590"),
            Link("UotanCommunity", "https://www.uotan.cn"),
        ]),
        new("Home.Links.Resources", "Home.Links.Resources.Description", PackIconCodiconsKind.CloudDownload,
        [
            Link("Firefly", "https://yhcres.top"),
            Link("DaxiaamuOnePlus", "https://yun.daxiaamu.com/OnePlus_Roms/"),
            Link("MIUIFirmware", "https://roms.miuier.com/zh-cn/devices"),
            Link("HyperOSFirmware", "https://hyperos.fans/zh/devices/"),
        ]),
        new("Home.Links.GeekSites", "Home.Links.GeekSites.Description", PackIconCodiconsKind.Tools,
        [
            Link("UotanWiki", "https://wiki.uotan.cn/"),
            Link("TWRPBuilder", "https://www.hovatek.com/twrpbuilder/"),
            Link("SPRDSign", "https://sprd-sign-web.pages.dev/"),
        ]),
    ];

    private HomeLink Link(string key, string url) => new("Home.Links." + key, new Uri(url));

    public override void Dispose()
    {
        (OpenLinkCommand as IDisposable)?.Dispose();
        base.Dispose();
    }
}
