using System.Collections.ObjectModel;
using System.Net;
using QuickTrans.Models;
using QuickTrans.Services;

namespace QuickTrans.ViewModels;

public sealed class SettingsViewModel : ObservableObject
{
    private readonly Func<AppSettings, Task> _save;
    private readonly Action<ThemeSettings>? _previewTheme;
    private readonly Action? _revertThemePreview;
    private readonly HashSet<string> _validated = new(StringComparer.Ordinal);
    private string _openAiStatus = "";
    private string _baiduStatus = "";
    private string _aliyunStatus = "";
    private string _pageStatus = "";

    public SettingsViewModel(
        AppSettings settings,
        bool firstRun,
        Func<AppSettings, Task> save,
        Action<ThemeSettings>? previewTheme = null,
        Action? revertThemePreview = null)
    {
        Settings = settings;
        IsFirstRun = firstRun;
        _save = save;
        _previewTheme = previewTheme;
        _revertThemePreview = revertThemePreview;
        Providers = new ObservableCollection<ProviderDescriptor>
        {
            new(ProviderIds.OpenAi, "AI 翻译", true),
            new(ProviderIds.Baidu, "百度翻译", false),
            new(ProviderIds.Aliyun, "阿里云翻译", false)
        };
        ThemeModes = new ObservableCollection<ThemeOption>
        {
            new(ThemeModeIds.System, "跟随 Windows"),
            new(ThemeModeIds.Light, "浅色"),
            new(ThemeModeIds.Dark, "深色"),
            new(ThemeModeIds.Custom, "自定义")
        };
        TestOpenAiCommand = new AsyncRelayCommand(() => TestAsync(ProviderIds.OpenAi));
        TestBaiduCommand = new AsyncRelayCommand(() => TestAsync(ProviderIds.Baidu));
        TestAliyunCommand = new AsyncRelayCommand(() => TestAsync(ProviderIds.Aliyun));
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        UseLightPaletteCommand = new RelayCommand(() => UsePalette(ThemeService.CreateLightPalette()));
        UseDarkPaletteCommand = new RelayCommand(() => UsePalette(ThemeService.CreateDarkPalette()));
    }

    public AppSettings Settings { get; }
    public bool IsFirstRun { get; }
    public ObservableCollection<ProviderDescriptor> Providers { get; }
    public ObservableCollection<ThemeOption> ThemeModes { get; }
    public AsyncRelayCommand TestOpenAiCommand { get; }
    public AsyncRelayCommand TestBaiduCommand { get; }
    public AsyncRelayCommand TestAliyunCommand { get; }
    public AsyncRelayCommand SaveCommand { get; }
    public RelayCommand UseLightPaletteCommand { get; }
    public RelayCommand UseDarkPaletteCommand { get; }
    public event EventHandler? RequestClose;

    public string DefaultProvider { get => Settings.DefaultProvider; set { Settings.DefaultProvider = value; RaisePropertyChanged(); } }
    public string Hotkey { get => Settings.Hotkey; set { Settings.Hotkey = value; RaisePropertyChanged(); } }
    public bool RunAtStartup { get => Settings.RunAtStartup; set { Settings.RunAtStartup = value; RaisePropertyChanged(); } }
    public string ThemeMode
    {
        get => Settings.Theme.Mode;
        set
        {
            if (Settings.Theme.Mode == value) return;
            Settings.Theme.Mode = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(IsCustomTheme));
            PreviewTheme();
        }
    }
    public bool IsCustomTheme => ThemeMode == ThemeModeIds.Custom;

    public string ThemeWindow { get => Settings.Theme.CustomPalette.Window; set => SetThemeColor(color => Settings.Theme.CustomPalette.Window = color, value); }
    public string ThemeSurface { get => Settings.Theme.CustomPalette.Surface; set => SetThemeColor(color => Settings.Theme.CustomPalette.Surface = color, value); }
    public string ThemeSurfaceAlt { get => Settings.Theme.CustomPalette.SurfaceAlt; set => SetThemeColor(color => Settings.Theme.CustomPalette.SurfaceAlt = color, value); }
    public string ThemeInput { get => Settings.Theme.CustomPalette.Input; set => SetThemeColor(color => Settings.Theme.CustomPalette.Input = color, value); }
    public string ThemeText { get => Settings.Theme.CustomPalette.Text; set => SetThemeColor(color => Settings.Theme.CustomPalette.Text = color, value); }
    public string ThemeMutedText { get => Settings.Theme.CustomPalette.MutedText; set => SetThemeColor(color => Settings.Theme.CustomPalette.MutedText = color, value); }
    public string ThemeBorder { get => Settings.Theme.CustomPalette.Border; set => SetThemeColor(color => Settings.Theme.CustomPalette.Border = color, value); }
    public string ThemeAccent { get => Settings.Theme.CustomPalette.Accent; set => SetThemeColor(color => Settings.Theme.CustomPalette.Accent = color, value); }
    public string ThemeAccentHover { get => Settings.Theme.CustomPalette.AccentHover; set => SetThemeColor(color => Settings.Theme.CustomPalette.AccentHover = color, value); }
    public string ThemeAccentSubtle { get => Settings.Theme.CustomPalette.AccentSubtle; set => SetThemeColor(color => Settings.Theme.CustomPalette.AccentSubtle = color, value); }
    public string ThemeError { get => Settings.Theme.CustomPalette.Error; set => SetThemeColor(color => Settings.Theme.CustomPalette.Error = color, value); }
    public string ThemeSuccess { get => Settings.Theme.CustomPalette.Success; set => SetThemeColor(color => Settings.Theme.CustomPalette.Success = color, value); }

    public string OpenAiBaseUrl { get => Settings.OpenAi.BaseUrl; set { Settings.OpenAi.BaseUrl = value; Invalidate(ProviderIds.OpenAi); RaisePropertyChanged(); } }
    public string OpenAiApiKey { get => Settings.OpenAi.ApiKey; set { Settings.OpenAi.ApiKey = value; Invalidate(ProviderIds.OpenAi); RaisePropertyChanged(); } }
    public string OpenAiModel { get => Settings.OpenAi.Model; set { Settings.OpenAi.Model = value; Invalidate(ProviderIds.OpenAi); RaisePropertyChanged(); } }
    public string CustomPrompt { get => Settings.OpenAi.CustomPrompt; set { Settings.OpenAi.CustomPrompt = value; Invalidate(ProviderIds.OpenAi); RaisePropertyChanged(); } }
    public string BaiduAppId { get => Settings.Baidu.AppId; set { Settings.Baidu.AppId = value; Invalidate(ProviderIds.Baidu); RaisePropertyChanged(); } }
    public string BaiduSecretKey { get => Settings.Baidu.SecretKey; set { Settings.Baidu.SecretKey = value; Invalidate(ProviderIds.Baidu); RaisePropertyChanged(); } }
    public string AliyunAccessKeyId { get => Settings.Aliyun.AccessKeyId; set { Settings.Aliyun.AccessKeyId = value; Invalidate(ProviderIds.Aliyun); RaisePropertyChanged(); } }
    public string AliyunAccessKeySecret { get => Settings.Aliyun.AccessKeySecret; set { Settings.Aliyun.AccessKeySecret = value; Invalidate(ProviderIds.Aliyun); RaisePropertyChanged(); } }

    public string OpenAiStatus { get => _openAiStatus; private set => SetProperty(ref _openAiStatus, value); }
    public string BaiduStatus { get => _baiduStatus; private set => SetProperty(ref _baiduStatus, value); }
    public string AliyunStatus { get => _aliyunStatus; private set => SetProperty(ref _aliyunStatus, value); }
    public string PageStatus { get => _pageStatus; private set => SetProperty(ref _pageStatus, value); }

    private async Task TestAsync(string providerId)
    {
        SetProviderStatus(providerId, "正在测试…");
        using var httpClient = new HttpClient(new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            ConnectTimeout = TimeSpan.FromSeconds(8)
        }) { Timeout = TimeSpan.FromSeconds(providerId == ProviderIds.OpenAi ? 20 : 8) };

        ITranslationProvider provider = providerId switch
        {
            ProviderIds.OpenAi => new OpenAiTranslationProvider(httpClient, () => Settings.OpenAi),
            ProviderIds.Baidu => new BaiduTranslationProvider(httpClient, () => Settings.Baidu),
            ProviderIds.Aliyun => new AliyunTranslationProvider(httpClient, () => Settings.Aliyun),
            _ => throw new ArgumentOutOfRangeException(nameof(providerId))
        };

        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            await provider.ValidateConfigurationAsync(timeout.Token);
            _validated.Add(providerId);
            SetProviderStatus(providerId, "连接成功");
            PageStatus = "";
        }
        catch (OperationCanceledException)
        {
            _validated.Remove(providerId);
            SetProviderStatus(providerId, "连接超时");
        }
        catch (TranslationException ex)
        {
            _validated.Remove(providerId);
            SetProviderStatus(providerId, ex.UserMessage);
        }
        catch (Exception)
        {
            _validated.Remove(providerId);
            SetProviderStatus(providerId, "连接失败，请检查网络和配置。 ");
        }
    }

    private async Task SaveAsync()
    {
        if (!ThemeService.TryValidate(Settings.Theme, out var themeError))
        {
            PageStatus = themeError ?? "自定义配色无效。";
            return;
        }
        if (!HotkeyService.TryNormalize(Hotkey, out var normalized, out var error))
        {
            PageStatus = error ?? "快捷键格式不正确。 ";
            return;
        }
        Hotkey = normalized;

        if (!IsProviderConfigured(DefaultProvider))
        {
            PageStatus = "请完整配置默认翻译引擎。 ";
            return;
        }
        if (IsFirstRun && !_validated.Contains(DefaultProvider))
        {
            PageStatus = "首次使用前，请测试默认翻译引擎并确认连接成功。 ";
            return;
        }

        try
        {
            Settings.SetupCompleted = true;
            await _save(Settings);
            RequestClose?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            PageStatus = ex.Message;
        }
    }

    public void RevertThemePreview() => _revertThemePreview?.Invoke();

    private void SetThemeColor(Action<string> setter, string value, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        setter(value.Trim());
        RaisePropertyChanged(propertyName);
        PreviewTheme();
    }

    private void UsePalette(ThemePalette palette)
    {
        Settings.Theme.CustomPalette = palette;
        Settings.Theme.Mode = ThemeModeIds.Custom;
        RaisePropertyChanged(nameof(ThemeMode));
        RaisePropertyChanged(nameof(IsCustomTheme));
        RaiseAllThemeColors();
        PreviewTheme();
    }

    private void RaiseAllThemeColors()
    {
        RaisePropertyChanged(nameof(ThemeWindow));
        RaisePropertyChanged(nameof(ThemeSurface));
        RaisePropertyChanged(nameof(ThemeSurfaceAlt));
        RaisePropertyChanged(nameof(ThemeInput));
        RaisePropertyChanged(nameof(ThemeText));
        RaisePropertyChanged(nameof(ThemeMutedText));
        RaisePropertyChanged(nameof(ThemeBorder));
        RaisePropertyChanged(nameof(ThemeAccent));
        RaisePropertyChanged(nameof(ThemeAccentHover));
        RaisePropertyChanged(nameof(ThemeAccentSubtle));
        RaisePropertyChanged(nameof(ThemeError));
        RaisePropertyChanged(nameof(ThemeSuccess));
    }

    private void PreviewTheme()
    {
        if (ThemeService.TryValidate(Settings.Theme, out _))
            _previewTheme?.Invoke(Settings.Theme);
    }

    private bool IsProviderConfigured(string id) => id switch
    {
        ProviderIds.OpenAi => Settings.OpenAi.IsConfigured,
        ProviderIds.Baidu => Settings.Baidu.IsConfigured,
        ProviderIds.Aliyun => Settings.Aliyun.IsConfigured,
        _ => false
    };

    private void Invalidate(string id)
    {
        _validated.Remove(id);
        SetProviderStatus(id, string.Empty);
    }

    private void SetProviderStatus(string id, string value)
    {
        switch (id)
        {
            case ProviderIds.OpenAi: OpenAiStatus = value; break;
            case ProviderIds.Baidu: BaiduStatus = value; break;
            case ProviderIds.Aliyun: AliyunStatus = value; break;
        }
    }
}

public sealed record ThemeOption(string Id, string DisplayName);
