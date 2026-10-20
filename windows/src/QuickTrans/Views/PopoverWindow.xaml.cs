using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using QuickTrans.Interop;
using QuickTrans.Services;
using QuickTrans.ViewModels;
using Forms = System.Windows.Forms;

namespace QuickTrans.Views;

public partial class PopoverWindow : Window
{
    private System.Threading.Timer? _outsideClickTimer;
    private IntPtr _windowHandle;
    private int _mouseWasDown;
    private int _dismissPending;
    private int _isDismissed;

    public PopoverWindow(PopoverViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Deactivated += (_, _) => { if (IsVisible && IsActive == false) Dismiss(); };
        Closed += (_, _) => StopOutsideClickMonitor();
        SourceInitialized += (_, _) => ThemeService.ApplyWindowTheme(this);
    }

    public void ShowAtCursor()
    {
        NativeMethods.GetCursorPos(out var cursor);
        var screen = Forms.Screen.FromPoint(new System.Drawing.Point(cursor.X, cursor.Y));
        Opacity = 0;
        try
        {
            if (!IsVisible)
                Show();

            UpdateLayout();
            var handle = new WindowInteropHelper(this).Handle;
            _windowHandle = handle;

            // Move once while fully transparent so Windows applies the target monitor's
            // DPI before the final measurement. This avoids rendering at the previous
            // location and visibly sliding to the cursor one dispatcher frame later.
            PositionWindow(handle, cursor, screen);
            var dpi = NativeMethods.GetDpiForWindow(handle);
            var dpiScale = dpi > 0 ? dpi / 96.0 : 1.0;
            MaxHeight = Math.Max(220, screen.WorkingArea.Height / dpiScale * 0.40);
            UpdateLayout();
            PositionWindow(handle, cursor, screen);
        }
        finally
        {
            Opacity = 1;
        }

        StartOutsideClickMonitor();
    }

    private static void PositionWindow(IntPtr handle, NativeMethods.Point cursor, Forms.Screen screen)
    {
        if (handle == IntPtr.Zero || !NativeMethods.GetWindowRect(handle, out var current))
            return;

        var dpi = NativeMethods.GetDpiForWindow(handle);
        var scale = dpi > 0 ? dpi / 96.0 : 1.0;
        var horizontalOffset = (int)Math.Round(14 * scale);
        var verticalOffset = (int)Math.Round(18 * scale);
        var edgePadding = (int)Math.Round(6 * scale);
        var aboveOffset = (int)Math.Round(14 * scale);
        var width = current.Right - current.Left;
        var height = current.Bottom - current.Top;
        var work = screen.WorkingArea;
        var desiredLeft = cursor.X + horizontalOffset;
        var desiredTop = cursor.Y + verticalOffset;
        var left = Math.Clamp(desiredLeft, work.Left + edgePadding, Math.Max(work.Left + edgePadding, work.Right - width - edgePadding));
        var top = desiredTop + height <= work.Bottom - edgePadding
            ? desiredTop
            : Math.Max(work.Top + edgePadding, cursor.Y - height - aboveOffset);

        NativeMethods.SetWindowPos(
            handle,
            IntPtr.Zero,
            left,
            top,
            0,
            0,
            NativeMethods.SwpNosize | NativeMethods.SwpNozorder | NativeMethods.SwpNoactivate);
    }

    public void Dismiss()
    {
        if (Interlocked.Exchange(ref _isDismissed, 1) != 0)
            return;

        StopOutsideClickMonitor();
        if (DataContext is PopoverViewModel viewModel)
            viewModel.Cancel();
        Close();
    }

    private void StartOutsideClickMonitor()
    {
        Interlocked.Exchange(ref _dismissPending, 0);
        if (_outsideClickTimer is not null)
            return;

        Interlocked.Exchange(ref _mouseWasDown, IsAnyMouseButtonDown() ? 1 : 0);
        _outsideClickTimer = new System.Threading.Timer(CheckOutsideClick, null, 10, 10);
    }

    private void StopOutsideClickMonitor()
    {
        Interlocked.Exchange(ref _dismissPending, 1);
        Interlocked.Exchange(ref _outsideClickTimer, null)?.Dispose();
        _windowHandle = IntPtr.Zero;
    }

    private void CheckOutsideClick(object? state)
    {
        var isDown = IsAnyMouseButtonDown();
        var wasDown = Interlocked.Exchange(ref _mouseWasDown, isDown ? 1 : 0) != 0;
        if (!isDown || wasDown || Volatile.Read(ref _dismissPending) != 0)
            return;

        if (_windowHandle != IntPtr.Zero && NativeMethods.GetCursorPos(out var cursor) &&
            NativeMethods.GetWindowRect(_windowHandle, out var bounds) && !bounds.Contains(cursor) &&
            Interlocked.Exchange(ref _dismissPending, 1) == 0)
        {
            Dispatcher.BeginInvoke(Dismiss, DispatcherPriority.Send);
        }
    }

    private static bool IsAnyMouseButtonDown() =>
        IsKeyDown(NativeMethods.VkLbutton) || IsKeyDown(NativeMethods.VkRbutton) ||
        IsKeyDown(NativeMethods.VkMbutton) || IsKeyDown(NativeMethods.VkXbutton1) ||
        IsKeyDown(NativeMethods.VkXbutton2);

    private static bool IsKeyDown(int virtualKey) =>
        (NativeMethods.GetAsyncKeyState(virtualKey) & 0x8000) != 0;

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Dismiss();
        }
    }
}
