using System.Windows;
using System.Windows.Automation;
using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;
using QuickTrans.Interop;

namespace QuickTrans.Services;

public sealed class TextSelectionService
{
    public async Task<string> GetSelectedTextAsync(CancellationToken cancellationToken)
    {
        var automationText = TryReadAutomationSelection();
        if (!string.IsNullOrWhiteSpace(automationText))
            return automationText;
        return await ReadViaClipboardAsync(cancellationToken);
    }

    private static string? TryReadAutomationSelection()
    {
        try
        {
            var focused = AutomationElement.FocusedElement;
            if (focused is null || !focused.TryGetCurrentPattern(TextPattern.Pattern, out var patternObject))
                return null;
            var ranges = ((TextPattern)patternObject).GetSelection();
            if (ranges.Length == 0)
                return null;
            return string.Join(Environment.NewLine, ranges.Select(range => range.GetText(TranslationRequestValidator.MaxCharacters + 1)));
        }
        catch (ElementNotAvailableException) { return null; }
        catch (InvalidOperationException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    private static async Task<string> ReadViaClipboardAsync(CancellationToken cancellationToken)
    {
        // WM_HOTKEY may be delivered before the user has physically released Alt+Q.
        // Copying while Alt is still down becomes Alt+Ctrl+C and is ignored by most apps.
        await WaitForHotkeyReleaseAsync(cancellationToken);

        System.Windows.IDataObject? original = null;
        var hadOriginal = false;
        try
        {
            original = SnapshotClipboard();
            hadOriginal = original is not null;
        }
        catch (ExternalException) { }

        var before = NativeMethods.GetClipboardSequenceNumber();
        NativeMethods.keybd_event(NativeMethods.VkControl, 0, 0, UIntPtr.Zero);
        NativeMethods.keybd_event(NativeMethods.VkC, 0, 0, UIntPtr.Zero);
        NativeMethods.keybd_event(NativeMethods.VkC, 0, NativeMethods.KeyeventfKeyup, UIntPtr.Zero);
        NativeMethods.keybd_event(NativeMethods.VkControl, 0, NativeMethods.KeyeventfKeyup, UIntPtr.Zero);

        string? selected = null;
        for (var attempt = 0; attempt < 10; attempt++)
        {
            await Task.Delay(30, cancellationToken);
            if (NativeMethods.GetClipboardSequenceNumber() == before)
                continue;
            try
            {
                if (Clipboard.ContainsText())
                {
                    selected = Clipboard.GetText();
                    break;
                }
            }
            catch (ExternalException) { }
        }

        try
        {
            if (hadOriginal && original is not null)
                Clipboard.SetDataObject(original, true);
            else
                Clipboard.Clear();
        }
        catch (ExternalException) { }

        return selected ?? string.Empty;
    }

    private static async Task WaitForHotkeyReleaseAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 30; attempt++)
        {
            if (!IsDown(NativeMethods.VkMenu) && !IsDown(NativeMethods.VkControl) &&
                !IsDown(NativeMethods.VkShift) && !IsDown(NativeMethods.VkLwin) && !IsDown(NativeMethods.VkRwin))
                return;
            await Task.Delay(15, cancellationToken);
        }
    }

    private static bool IsDown(int virtualKey) => (NativeMethods.GetAsyncKeyState(virtualKey) & 0x8000) != 0;

    private static System.Windows.IDataObject? SnapshotClipboard()
    {
        var source = System.Windows.Clipboard.GetDataObject();
        if (source is null)
            return null;

        var snapshot = new System.Windows.DataObject();
        foreach (var format in source.GetFormats(autoConvert: false))
        {
            try
            {
                var data = source.GetData(format, autoConvert: false);
                if (data is null) continue;
                snapshot.SetData(format, CloneClipboardValue(data), autoConvert: false);
            }
            catch (Exception ex) when (ex is ExternalException or COMException or IOException)
            {
                // Preserve every materializable format; skip delayed/owner-rendered formats
                // that cannot safely survive replacing the clipboard.
            }
        }
        return snapshot.GetFormats(false).Length > 0 ? snapshot : null;
    }

    private static object CloneClipboardValue(object value)
    {
        if (value is MemoryStream memory)
            return new MemoryStream(memory.ToArray(), writable: false);
        if (value is Stream stream)
        {
            var position = stream.CanSeek ? stream.Position : 0;
            var copy = new MemoryStream();
            stream.CopyTo(copy);
            copy.Position = 0;
            if (stream.CanSeek) stream.Position = position;
            return copy;
        }
        if (value is BitmapSource bitmap)
        {
            var clone = bitmap.Clone();
            clone.Freeze();
            return clone;
        }
        if (value is System.Drawing.Bitmap drawingBitmap)
            return drawingBitmap.Clone();
        return value;
    }
}
