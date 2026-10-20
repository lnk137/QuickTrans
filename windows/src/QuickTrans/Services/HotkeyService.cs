using System.ComponentModel;
using System.Globalization;
using System.Windows.Input;
using System.Windows.Interop;
using QuickTrans.Interop;

namespace QuickTrans.Services;

public sealed class HotkeyService : IDisposable
{
    private const int HotkeyId = 0x5154;
    private readonly HwndSource _source;
    private bool _registered;

    public event EventHandler? Pressed;
    public string CurrentGesture { get; private set; } = "Alt+Q";

    public HotkeyService()
    {
        var parameters = new HwndSourceParameters("QuickTrans.HotkeySink")
        {
            Width = 0,
            Height = 0,
            WindowStyle = unchecked((int)0x80000000)
        };
        _source = new HwndSource(parameters);
        _source.AddHook(WindowProc);
    }

    public void Register(string gestureText)
    {
        var (modifiers, key) = Parse(gestureText);
        if (_registered)
        {
            NativeMethods.UnregisterHotKey(_source.Handle, HotkeyId);
            _registered = false;
        }

        var virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);
        if (!NativeMethods.RegisterHotKey(_source.Handle, HotkeyId, modifiers | NativeMethods.ModNoRepeat, virtualKey))
            throw new Win32Exception("快捷键已被其他应用占用，请换一个组合。 ");

        _registered = true;
        CurrentGesture = Format(modifiers, key);
    }

    public static bool TryNormalize(string value, out string normalized, out string? error)
    {
        try
        {
            var (modifiers, key) = Parse(value);
            normalized = Format(modifiers, key);
            error = null;
            return true;
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException)
        {
            normalized = string.Empty;
            error = ex.Message;
            return false;
        }
    }

    private static (uint Modifiers, Key Key) Parse(string text)
    {
        var parts = text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
            throw new FormatException("快捷键必须包含 Alt、Ctrl、Shift 或 Win 修饰键。 ");

        uint modifiers = 0;
        Key key = Key.None;
        foreach (var part in parts)
        {
            switch (part.ToLowerInvariant())
            {
                case "alt": modifiers |= NativeMethods.ModAlt; break;
                case "ctrl":
                case "control": modifiers |= NativeMethods.ModControl; break;
                case "shift": modifiers |= NativeMethods.ModShift; break;
                case "win":
                case "windows": modifiers |= NativeMethods.ModWin; break;
                default:
                    if (key != Key.None || !Enum.TryParse(part, true, out key) || key == Key.None)
                        throw new FormatException("无法识别快捷键，请使用例如 Alt+Q 的格式。 ");
                    break;
            }
        }

        if (modifiers == 0 || key == Key.None || key is Key.LeftAlt or Key.RightAlt or Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift)
            throw new FormatException("快捷键必须包含修饰键和一个普通按键。 ");
        return (modifiers, key);
    }

    private static string Format(uint modifiers, Key key)
    {
        var parts = new List<string>();
        if ((modifiers & NativeMethods.ModControl) != 0) parts.Add("Ctrl");
        if ((modifiers & NativeMethods.ModAlt) != 0) parts.Add("Alt");
        if ((modifiers & NativeMethods.ModShift) != 0) parts.Add("Shift");
        if ((modifiers & NativeMethods.ModWin) != 0) parts.Add("Win");
        parts.Add(key.ToString().ToUpper(CultureInfo.InvariantCulture));
        return string.Join('+', parts);
    }

    private IntPtr WindowProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == NativeMethods.WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            handled = true;
            Pressed?.Invoke(this, EventArgs.Empty);
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_registered)
            NativeMethods.UnregisterHotKey(_source.Handle, HotkeyId);
        _source.RemoveHook(WindowProc);
        _source.Dispose();
    }
}
