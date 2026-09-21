using System.ComponentModel;
using System.Windows;
using QuickTrans.Services;
using QuickTrans.ViewModels;

namespace QuickTrans.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;
    private readonly bool _firstRun;
    private bool _completed;

    public SettingsWindow(SettingsViewModel viewModel, bool firstRun)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _firstRun = firstRun;
        DataContext = viewModel;
        OpenAiApiKeyBox.Password = viewModel.OpenAiApiKey;
        BaiduSecretBox.Password = viewModel.BaiduSecretKey;
        AliyunSecretBox.Password = viewModel.AliyunAccessKeySecret;
        viewModel.RequestClose += OnRequestClose;
        Closing += OnClosing;
        SourceInitialized += (_, _) => ThemeService.ApplyWindowTheme(this);
    }

    private void OpenAiApiKeyBox_OnPasswordChanged(object sender, RoutedEventArgs e) => _viewModel.OpenAiApiKey = OpenAiApiKeyBox.Password;
    private void BaiduSecretBox_OnPasswordChanged(object sender, RoutedEventArgs e) => _viewModel.BaiduSecretKey = BaiduSecretBox.Password;
    private void AliyunSecretBox_OnPasswordChanged(object sender, RoutedEventArgs e) => _viewModel.AliyunAccessKeySecret = AliyunSecretBox.Password;

    private void OnRequestClose(object? sender, EventArgs e)
    {
        _completed = true;
        Close();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        _viewModel.RequestClose -= OnRequestClose;
        if (!_completed)
            _viewModel.RevertThemePreview();
        if (_firstRun && !_completed)
            Application.Current.Dispatcher.BeginInvoke(new Action(() => Application.Current.Shutdown()));
    }
}
