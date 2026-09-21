using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using QuickTrans.Models;
using QuickTrans.Services;

namespace QuickTrans.ViewModels;

public sealed class PopoverViewModel : ObservableObject, IDisposable
{
    private readonly IReadOnlyDictionary<string, ITranslationProvider> _providers;
    private readonly SessionTranslationCache _cache;
    private CancellationTokenSource? _requestCancellation;
    private string _sourceText = "";
    private string _resultText = "";
    private string _statusText = "正在读取所选文本…";
    private string _selectedProviderId;
    private bool _isBusy;
    private bool _hasError;
    private bool _isTerm;
    private bool _suppressProviderChange;

    public PopoverViewModel(IEnumerable<ITranslationProvider> providers, SessionTranslationCache cache, string defaultProvider)
    {
        _providers = providers.ToDictionary(provider => provider.Descriptor.Id);
        _cache = cache;
        Providers = new ObservableCollection<ProviderDescriptor>(_providers.Values.Select(item => item.Descriptor));
        _selectedProviderId = _providers.ContainsKey(defaultProvider) ? defaultProvider : Providers.First().Id;
        CopyCommand = new RelayCommand(CopyResult, () => !string.IsNullOrWhiteSpace(ResultText));
        ExplainCommand = new AsyncRelayCommand(ExplainAsync, () => CanExplain);
    }

    public ObservableCollection<ProviderDescriptor> Providers { get; }
    public RelayCommand CopyCommand { get; }
    public AsyncRelayCommand ExplainCommand { get; }

    public string SourceText { get => _sourceText; private set => SetProperty(ref _sourceText, value); }
    public string ResultText
    {
        get => _resultText;
        private set
        {
            if (SetProperty(ref _resultText, value))
            {
                CopyCommand.RaiseCanExecuteChanged();
                RaisePropertyChanged(nameof(CanExplain));
                ExplainCommand.RaiseCanExecuteChanged();
            }
        }
    }
    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (!SetProperty(ref _isBusy, value)) return;
            RaisePropertyChanged(nameof(CanExplain));
            ExplainCommand.RaiseCanExecuteChanged();
        }
    }
    public bool HasError
    {
        get => _hasError;
        private set
        {
            if (!SetProperty(ref _hasError, value)) return;
            RaisePropertyChanged(nameof(CanExplain));
            ExplainCommand.RaiseCanExecuteChanged();
        }
    }
    public bool HasStatus => !string.IsNullOrWhiteSpace(StatusText);
    public bool CanExplain => !_isTerm && !IsBusy && !HasError && !string.IsNullOrWhiteSpace(ResultText) && SelectedProviderId == ProviderIds.OpenAi;

    public string SelectedProviderId
    {
        get => _selectedProviderId;
        set
        {
            if (!SetProperty(ref _selectedProviderId, value)) return;
            RaisePropertyChanged(nameof(CanExplain));
            ExplainCommand.RaiseCanExecuteChanged();
            if (!_suppressProviderChange && SourceText.Length > 0)
                _ = TranslateAsync(TranslationMode.Translate);
        }
    }

    public void ShowLoading()
    {
        Cancel();
        SourceText = "";
        ResultText = "";
        HasError = false;
        IsBusy = true;
        SetStatus("正在读取所选文本…");
    }

    public async Task SetSourceAndTranslateAsync(string text, string preferredProvider)
    {
        SourceText = text;
        var direction = LanguageService.DetectDirection(text);
        _isTerm = LanguageService.IsTermLike(text, direction.Source);
        _suppressProviderChange = true;
        SelectedProviderId = _providers.ContainsKey(preferredProvider) ? preferredProvider : Providers.First().Id;
        _suppressProviderChange = false;
        await TranslateAsync(TranslationMode.Translate);
    }

    public void ShowError(string message)
    {
        Cancel();
        IsBusy = false;
        HasError = true;
        ResultText = "";
        SetStatus(message);
    }

    private async Task ExplainAsync() => await TranslateAsync(TranslationMode.Explain);

    private async Task TranslateAsync(TranslationMode mode)
    {
        Cancel();
        if (!_providers.TryGetValue(SelectedProviderId, out var provider))
        {
            ShowError("所选翻译引擎不可用。 ");
            return;
        }
        if (!provider.IsConfigured)
        {
            ShowError($"{provider.Descriptor.DisplayName}尚未配置，请从托盘打开设置。 ");
            return;
        }
        if (mode == TranslationMode.Explain && !provider.Descriptor.SupportsExplanation)
            return;

        _requestCancellation = new CancellationTokenSource();
        var cancellationToken = _requestCancellation.Token;
        var direction = LanguageService.DetectDirection(SourceText);
        var request = new TranslationRequest(SourceText, direction.Source, direction.Target, mode, _isTerm, PromptBuilder.PromptVersion);
        var model = provider is OpenAiTranslationProvider openAi ? openAi.Model : string.Empty;

        HasError = false;
        IsBusy = true;
        ResultText = "";
        SetStatus(mode == TranslationMode.Explain ? "正在解释…" : "正在翻译…");

        if (_cache.TryGet(provider.Descriptor.Id, request, model, out var cached))
        {
            ResultText = cached;
            IsBusy = false;
            SetStatus("已从本次会话缓存读取");
            return;
        }

        var builder = new StringBuilder();
        var uiRefresh = Stopwatch.StartNew();
        var statusCleared = false;
        try
        {
            await foreach (var chunk in provider.TranslateAsync(request, cancellationToken).ConfigureAwait(false))
            {
                if (!string.IsNullOrEmpty(chunk.Text))
                {
                    builder.Append(chunk.Text);
                    if (!statusCleared)
                    {
                        statusCleared = true;
                        await OnUiAsync(() => SetStatus(string.Empty)).ConfigureAwait(false);
                    }
                    if (uiRefresh.ElapsedMilliseconds >= 50)
                    {
                        var snapshot = builder.ToString();
                        await OnUiAsync(() => ResultText = snapshot).ConfigureAwait(false);
                        uiRefresh.Restart();
                    }
                }
            }
            if (builder.Length == 0)
                throw new TranslationException("翻译服务没有返回可用内容。 ");
            var finalResult = builder.ToString();
            _cache.Set(provider.Descriptor.Id, request, model, finalResult);
            await OnUiAsync(() =>
            {
                ResultText = finalResult;
                IsBusy = false;
                SetStatus(string.Empty);
            }).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (TranslationException ex)
        {
            await OnUiAsync(() => ShowError(ex.UserMessage)).ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
            await OnUiAsync(() => ShowError("无法连接翻译服务，请检查网络和接口地址。 ")).ConfigureAwait(false);
        }
        catch (Exception)
        {
            await OnUiAsync(() => ShowError("翻译失败，请稍后重试或切换引擎。 ")).ConfigureAwait(false);
        }
    }

    private static Task OnUiAsync(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
            return Task.CompletedTask;
        }

        return dispatcher.InvokeAsync(action, DispatcherPriority.Background).Task;
    }

    private void CopyResult()
    {
        try
        {
            Clipboard.SetText(ResultText);
            SetStatus("已复制");
        }
        catch (Exception)
        {
            SetStatus("剪贴板正被占用，请稍后重试");
        }
    }

    private void SetStatus(string value)
    {
        StatusText = value;
        RaisePropertyChanged(nameof(HasStatus));
    }

    public void Cancel()
    {
        _requestCancellation?.Cancel();
        _requestCancellation?.Dispose();
        _requestCancellation = null;
    }

    public void Dispose() => Cancel();
}
