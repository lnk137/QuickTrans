using System.Net;
using System.Text.Json;
using System.Windows;
using QuickTrans.Models;
using QuickTrans.ViewModels;
using QuickTrans.Views;

namespace QuickTrans.Services;

public sealed class AppController : IDisposable
{
    private readonly ConfigStore _configStore = new();
    private readonly StartupService _startupService = new();
    private readonly TextSelectionService _selectionService = new();
    private readonly SessionTranslationCache _cache = new();
    private readonly HttpClient _openAiHttpClient;
    private readonly HttpClient _machineHttpClient;
    private readonly HttpClient _updateHttpClient;
    private readonly HotkeyService _hotkeyService;
    private readonly ThemeService _themeService;
    private readonly TrayService _trayService;
    private AppSettings _settings;
    private PopoverWindow? _popoverWindow;
    private SettingsWindow? _settingsWindow;
    private bool _paused;
    private bool _disposed;

    public AppController()
    {
        _settings = _configStore.Load();
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            ConnectTimeout = TimeSpan.FromSeconds(8),
            PooledConnectionLifetime = TimeSpan.FromMinutes(5)
        };
        _openAiHttpClient = new HttpClient(handler, disposeHandler: false) { Timeout = Timeout.InfiniteTimeSpan };
        _machineHttpClient = new HttpClient(handler, disposeHandler: false) { Timeout = TimeSpan.FromSeconds(8) };
        _updateHttpClient = new HttpClient(handler, disposeHandler: true) { Timeout = TimeSpan.FromSeconds(8) };
        _hotkeyService = new HotkeyService();
        _themeService = new ThemeService(() => _settings.Theme);
        _trayService = new TrayService();
    }

    public void Initialize()
    {
        _hotkeyService.Pressed += OnHotkeyPressed;
        _trayService.OpenSettingsRequested += (_, _) => OpenSettings(false);
        _trayService.PauseChanged += (_, paused) => _paused = paused;
        _trayService.ExitRequested += (_, _) => Application.Current.Shutdown();

        try
        {
            _hotkeyService.Register(_settings.Hotkey);
        }
        catch
        {
            _settings.Hotkey = "Alt+Q";
            try { _hotkeyService.Register(_settings.Hotkey); }
            catch { _trayService.ShowInfo("默认快捷键被占用，请打开设置更换快捷键。 "); }
        }

        if (!_settings.SetupCompleted)
            OpenSettings(true);
        else
            _ = Task.Run(async () =>
            {
                await Task.Delay(1500);
                await new VersionCheckService(_updateHttpClient).CheckAsync();
            });
    }

    private IEnumerable<ITranslationProvider> CreateProviders() =>
    [
        new OpenAiTranslationProvider(_openAiHttpClient, () => _settings.OpenAi),
        new BaiduTranslationProvider(_machineHttpClient, () => _settings.Baidu),
        new AliyunTranslationProvider(_machineHttpClient, () => _settings.Aliyun)
    ];

    private async void OnHotkeyPressed(object? sender, EventArgs e)
    {
        if (_paused)
        {
            _trayService.ShowInfo("取词已暂停，可从托盘菜单恢复。 ");
            return;
        }

        CloseCurrentPopover();

        var lifetime = new CancellationTokenSource();
        var viewModel = new PopoverViewModel(CreateProviders(), _cache, _settings.DefaultProvider);
        var window = new PopoverWindow(viewModel);
        _popoverWindow = window;
        window.Closed += (_, _) => ReleasePopover(window, viewModel, lifetime);

        viewModel.ShowLoading();
        window.ShowAtCursor();
        try
        {
            var text = await _selectionService.GetSelectedTextAsync(lifetime.Token);
            text = TranslationRequestValidator.NormalizeAndValidate(text);
            lifetime.Token.ThrowIfCancellationRequested();
            await viewModel.SetSourceAndTranslateAsync(text, _settings.DefaultProvider);
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
        {
        }
        catch (InvalidOperationException ex)
        {
            if (!lifetime.IsCancellationRequested)
                viewModel.ShowError(ex.Message);
        }
        catch (Exception)
        {
            if (!lifetime.IsCancellationRequested)
                viewModel.ShowError("读取选中文本失败，请重新选择后再试。 ");
        }
    }

    private void CloseCurrentPopover()
    {
        var window = _popoverWindow;
        _popoverWindow = null;
        window?.Dismiss();
    }

    private void ReleasePopover(PopoverWindow window, PopoverViewModel viewModel, CancellationTokenSource lifetime)
    {
        lifetime.Cancel();
        viewModel.Dispose();
        lifetime.Dispose();
        if (!ReferenceEquals(_popoverWindow, window))
            return;

        _popoverWindow = null;
    }

    private void OpenSettings(bool firstRun)
    {
        if (_settingsWindow is not null)
        {
            _settingsWindow.Activate();
            return;
        }

        var draft = Clone(_settings);
        var viewModel = new SettingsViewModel(draft, firstRun, SaveSettingsAsync, _themeService.Preview, _themeService.Apply);
        _settingsWindow = new SettingsWindow(viewModel, firstRun);
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    private Task SaveSettingsAsync(AppSettings newSettings)
    {
        var previousHotkey = _settings.Hotkey;
        try
        {
            _hotkeyService.Register(newSettings.Hotkey);
        }
        catch
        {
            try { _hotkeyService.Register(previousHotkey); } catch { }
            throw;
        }

        _startupService.SetEnabled(newSettings.RunAtStartup);
        _configStore.Save(newSettings);
        _settings = Clone(newSettings);
        _themeService.Apply();
        return Task.CompletedTask;
    }

    private static AppSettings Clone(AppSettings value) =>
        JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(value)) ?? new AppSettings();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        CloseCurrentPopover();
        _settingsWindow?.Close();
        _hotkeyService.Dispose();
        _trayService.Dispose();
        _themeService.Dispose();
        _openAiHttpClient.Dispose();
        _machineHttpClient.Dispose();
        _updateHttpClient.Dispose();
    }
}
