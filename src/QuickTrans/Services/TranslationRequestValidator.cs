namespace QuickTrans.Services;

public static class TranslationRequestValidator
{
    public const int MaxCharacters = 2000;

    public static string NormalizeAndValidate(string? text)
    {
        var normalized = (text ?? string.Empty).Replace("\r\n", "\n").Trim();
        if (normalized.Length == 0)
            throw new InvalidOperationException("没有读取到选中的文本。请重新选择后再试。");
        if (normalized.Length > MaxCharacters)
            throw new InvalidOperationException($"选中文本超过 {MaxCharacters:N0} 字符，请缩短选区后重试。");
        return normalized;
    }
}
