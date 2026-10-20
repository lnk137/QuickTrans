using System.Drawing;
using System.Windows;
using Forms = System.Windows.Forms;

namespace QuickTrans.Services;

public sealed class TrayService : IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private readonly Forms.ToolStripMenuItem _pauseItem;
    private readonly Icon _trayIcon;

    public event EventHandler? OpenSettingsRequested;
    public event EventHandler<bool>? PauseChanged;
    public event EventHandler? ExitRequested;

    public TrayService()
    {
        _pauseItem = new Forms.ToolStripMenuItem("暂停取词") { CheckOnClick = true };
        _pauseItem.CheckedChanged += (_, _) => Dispatch(() => PauseChanged?.Invoke(this, _pauseItem.Checked));

        var settingsItem = new Forms.ToolStripMenuItem("设置");
        settingsItem.Click += (_, _) => Dispatch(() => OpenSettingsRequested?.Invoke(this, EventArgs.Empty));
        var exitItem = new Forms.ToolStripMenuItem("退出");
        exitItem.Click += (_, _) => Dispatch(() => ExitRequested?.Invoke(this, EventArgs.Empty));

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(settingsItem);
        menu.Items.Add(_pauseItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(exitItem);

        var resource = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/QuickTrans.ico"));
        if (resource is null)
            throw new InvalidOperationException("QuickTrans 图标资源缺失。 ");
        using (resource.Stream)
        using (var sourceIcon = new Icon(resource.Stream))
            _trayIcon = (Icon)sourceIcon.Clone();

        _icon = new Forms.NotifyIcon
        {
            Text = "QuickTrans",
            Icon = _trayIcon,
            ContextMenuStrip = menu,
            Visible = true
        };
        _icon.DoubleClick += (_, _) => Dispatch(() => OpenSettingsRequested?.Invoke(this, EventArgs.Empty));
    }

    public void ShowInfo(string message)
    {
        _icon.BalloonTipTitle = "QuickTrans";
        _icon.BalloonTipText = message;
        _icon.BalloonTipIcon = Forms.ToolTipIcon.Info;
        _icon.ShowBalloonTip(2500);
    }

    private static void Dispatch(Action action) => Application.Current.Dispatcher.BeginInvoke(action);

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _trayIcon.Dispose();
    }
}
