using GeekFlashToolX.Core.Services;

namespace GeekFlashToolX.ViewModels;

public sealed class OtherViewModel(ILocalizationService localization) : ViewModelBase(localization)
{
    public string Eyebrow => Text("Other.Eyebrow");
    public string Title => Text("Other.Title");
    public string Subtitle => Text("Other.Subtitle");
    public string EmptyTitle => Text("Other.EmptyTitle");
    public string EmptyBody => Text("Other.EmptyBody");
}