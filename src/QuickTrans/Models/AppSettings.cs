namespace QuickTrans.Models;

public sealed class AppSettings
{
    public string DefaultProvider { get; set; } = ProviderIds.OpenAi;
    public string Hotkey { get; set; } = "Alt+Q";
    public bool RunAtStartup { get; set; }
    public bool SetupCompleted { get; set; }
    public ThemeSettings Theme { get; set; } = new();
    public OpenAiSettings OpenAi { get; set; } = new();
    public BaiduSettings Baidu { get; set; } = new();
    public AliyunSettings Aliyun { get; set; } = new();
}

public static class ThemeModeIds
{
    public const string System = "system";
    public const string Light = "light";
    public const string Dark = "dark";
    public const string Custom = "custom";
}

public sealed class ThemeSettings
{
    public string Mode { get; set; } = ThemeModeIds.System;
    public ThemePalette CustomPalette { get; set; } = new();
}

public sealed class ThemePalette
{
    public string Window { get; set; } = "#10151B";
    public string Surface { get; set; } = "#171E27";
    public string SurfaceAlt { get; set; } = "#202A35";
    public string Input { get; set; } = "#121820";
    public string Text { get; set; } = "#EEF3F8";
    public string MutedText { get; set; } = "#9BA8B7";
    public string Border { get; set; } = "#2B3745";
    public string Accent { get; set; } = "#69A7F5";
    public string AccentHover { get; set; } = "#87BAF8";
    public string AccentSubtle { get; set; } = "#1D3551";
    public string Error { get; set; } = "#FF8A8A";
    public string Success { get; set; } = "#67C394";
}

public sealed class OpenAiSettings
{
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "";
    public string CustomPrompt { get; set; } = "";
    public bool IsConfigured => Uri.TryCreate(BaseUrl, UriKind.Absolute, out _) && !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(Model);
}

public sealed class BaiduSettings
{
    public string AppId { get; set; } = "";
    public string SecretKey { get; set; } = "";
    public bool IsConfigured => !string.IsNullOrWhiteSpace(AppId) && !string.IsNullOrWhiteSpace(SecretKey);
}

public sealed class AliyunSettings
{
    public string AccessKeyId { get; set; } = "";
    public string AccessKeySecret { get; set; } = "";
    public bool IsConfigured => !string.IsNullOrWhiteSpace(AccessKeyId) && !string.IsNullOrWhiteSpace(AccessKeySecret);
}
