namespace GeekFlashToolX.Core.Models;

public sealed class AppSettings
{
    public ThemeMode Theme { get; set; } = ThemeMode.System;

    public string LanguageCode { get; set; } = string.Empty;

    public string AccentColor { get; set; } = "#2F81F7";

    public bool AnimationsEnabled { get; set; } = true;

    public bool AutoCheckUpdates { get; set; } = true;

    public string? IgnoredUpdateVersion { get; set; }
}

public enum ThemeMode
{
    System,
    Light,
    Dark,
}

public sealed record LanguageOption(string Code, string NativeName);
