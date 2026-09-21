using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using QuickTrans.Models;

namespace QuickTrans.Services;

public sealed class ThemeService : IDisposable
{
    private const int DwmUseImmersiveDarkMode = 20;
    private const int DwmUseImmersiveDarkModeBefore20H1 = 19;
    private const int DwmBorderColor = 34;
    private const int DwmCaptionColor = 35;
    private const int DwmTextColor = 36;
    private readonly Func<ThemeSettings> _settingsAccessor;
    private static ThemePalette _activePalette = CreateLightPalette();

    public ThemeService(Func<ThemeSettings>? settingsAccessor = null)
    {
        _settingsAccessor = settingsAccessor ?? (() => new ThemeSettings());
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        Apply();
    }

    public void Apply() => Apply(_settingsAccessor());

    public void Preview(ThemeSettings settings) => Apply(settings);

    public static ThemePalette CreateLightPalette() => new()
    {
        Window = "#F3F5F7",
        Surface = "#FFFFFF",
        SurfaceAlt = "#EEF2F6",
        Input = "#F7F9FB",
        Text = "#16202A",
        MutedText = "#6D7988",
        Border = "#D8DEE6",
        Accent = "#2374D8",
        AccentHover = "#185FB6",
        AccentSubtle = "#E8F1FC",
        Error = "#B33A3A",
        Success = "#2B7A55"
    };

    public static ThemePalette CreateDarkPalette() => new();

    public static bool TryValidate(ThemeSettings settings, out string? error)
    {
        if (settings.Mode is not (ThemeModeIds.System or ThemeModeIds.Light or ThemeModeIds.Dark or ThemeModeIds.Custom))
        {
            error = "主题模式无效。";
            return false;
        }
        if (settings.Mode != ThemeModeIds.Custom)
        {
            error = null;
            return true;
        }

        foreach (var (name, value) in Enumerate(settings.CustomPalette))
        {
            if (TryParseColor(value, out _))
                continue;
            error = $"{name}不是有效颜色，请使用 #RRGGBB。";
            return false;
        }
        error = null;
        return true;
    }

    public static void ApplyWindowTheme(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
            return;

        var windowColor = ParseColor(_activePalette.Window, Colors.White);
        var textColor = ParseColor(_activePalette.Text, Colors.Black);
        var borderColor = ParseColor(_activePalette.Border, windowColor);
        var dark = IsDark(windowColor) ? 1 : 0;
        DwmSetWindowAttribute(handle, DwmUseImmersiveDarkMode, ref dark, sizeof(int));
        DwmSetWindowAttribute(handle, DwmUseImmersiveDarkModeBefore20H1, ref dark, sizeof(int));

        var caption = ToColorRef(windowColor);
        var text = ToColorRef(textColor);
        var border = ToColorRef(borderColor);
        DwmSetWindowAttribute(handle, DwmCaptionColor, ref caption, sizeof(int));
        DwmSetWindowAttribute(handle, DwmTextColor, ref text, sizeof(int));
        DwmSetWindowAttribute(handle, DwmBorderColor, ref border, sizeof(int));
    }

    private static void Apply(ThemeSettings settings)
    {
        var palette = ResolvePalette(settings);
        if (!TryValidate(new ThemeSettings { Mode = ThemeModeIds.Custom, CustomPalette = palette }, out _))
            palette = CreateDarkPalette();
        _activePalette = Clone(palette);

        var resources = Application.Current.Resources;
        Set(resources, "WindowBrush", palette.Window);
        Set(resources, "SurfaceBrush", palette.Surface);
        Set(resources, "SurfaceAltBrush", palette.SurfaceAlt);
        Set(resources, "InputBrush", palette.Input);
        Set(resources, "TextBrush", palette.Text);
        Set(resources, "MutedTextBrush", palette.MutedText);
        Set(resources, "BorderBrush", palette.Border);
        Set(resources, "AccentBrush", palette.Accent);
        Set(resources, "AccentHoverBrush", palette.AccentHover);
        Set(resources, "AccentSubtleBrush", palette.AccentSubtle);
        Set(resources, "ErrorBrush", palette.Error);
        Set(resources, "SuccessBrush", palette.Success);

        resources[SystemColors.ControlTextBrushKey] = resources["TextBrush"];
        resources[SystemColors.WindowTextBrushKey] = resources["TextBrush"];
        resources[SystemColors.ControlBrushKey] = resources["SurfaceBrush"];
        resources[SystemColors.WindowBrushKey] = resources["SurfaceBrush"];

        foreach (Window window in Application.Current.Windows)
            ApplyWindowTheme(window);
    }

    private static ThemePalette ResolvePalette(ThemeSettings settings) => settings.Mode switch
    {
        ThemeModeIds.Light => CreateLightPalette(),
        ThemeModeIds.Dark => CreateDarkPalette(),
        ThemeModeIds.Custom => settings.CustomPalette,
        _ => IsSystemLightTheme() ? CreateLightPalette() : CreateDarkPalette()
    };

    private static bool IsSystemLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return Convert.ToInt32(key?.GetValue("AppsUseLightTheme", 1)) != 0;
        }
        catch
        {
            return true;
        }
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e) =>
        Application.Current.Dispatcher.BeginInvoke(new Action(Apply));

    private static IEnumerable<(string Name, string Value)> Enumerate(ThemePalette palette)
    {
        yield return ("窗口背景", palette.Window);
        yield return ("内容背景", palette.Surface);
        yield return ("次级背景", palette.SurfaceAlt);
        yield return ("输入框背景", palette.Input);
        yield return ("主要文字", palette.Text);
        yield return ("次要文字", palette.MutedText);
        yield return ("边框", palette.Border);
        yield return ("强调色", palette.Accent);
        yield return ("悬停强调色", palette.AccentHover);
        yield return ("弱强调背景", palette.AccentSubtle);
        yield return ("错误色", palette.Error);
        yield return ("成功色", palette.Success);
    }

    private static ThemePalette Clone(ThemePalette value) => new()
    {
        Window = value.Window,
        Surface = value.Surface,
        SurfaceAlt = value.SurfaceAlt,
        Input = value.Input,
        Text = value.Text,
        MutedText = value.MutedText,
        Border = value.Border,
        Accent = value.Accent,
        AccentHover = value.AccentHover,
        AccentSubtle = value.AccentSubtle,
        Error = value.Error,
        Success = value.Success
    };

    private static void Set(ResourceDictionary resources, string key, string color) =>
        resources[key] = new SolidColorBrush(ParseColor(color, Colors.Magenta));

    private static bool TryParseColor(string value, out Color color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(value) || value.Length != 7 || value[0] != '#')
            return false;
        try
        {
            color = (Color)ColorConverter.ConvertFromString(value);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static Color ParseColor(string value, Color fallback) =>
        TryParseColor(value, out var color) ? color : fallback;

    private static bool IsDark(Color color) =>
        (0.2126 * color.R + 0.7152 * color.G + 0.0722 * color.B) < 128;

    private static int ToColorRef(Color color) => color.R | (color.G << 8) | (color.B << 16);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int valueSize);

    public void Dispose() => SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
}
