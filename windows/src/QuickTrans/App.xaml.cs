using System.Threading;
using System.Windows;
using QuickTrans.Services;

namespace QuickTrans;

public partial class App : System.Windows.Application
{
    private Mutex? _singleInstance;
    private AppController? _controller;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstance = new Mutex(true, @"Local\QuickTrans.App", out var createdNew);
        if (!createdNew)
        {
            MessageBox.Show("QuickTrans 已在运行。", "QuickTrans", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        _controller = new AppController();
        _controller.Initialize();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _controller?.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
