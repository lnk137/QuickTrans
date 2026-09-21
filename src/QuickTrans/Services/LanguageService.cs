using System.Text.RegularExpressions;

namespace QuickTrans.Services;

public static partial class LanguageService
{
    [GeneratedRegex("[\\u3400-\\u4DBF\\u4E00-\\u9FFF]", RegexOptions.Compiled)]
    private static partial Regex ChineseRegex();

    [GeneratedRegex("[。！？!?;；.\\r\\n]", RegexOptions.Compiled)]
    private static partial Regex SentenceEndingRegex();

    [GeneratedRegex("[A-Za-z]+(?:['’-][A-Za-z]+)?", RegexOptions.Compiled)]
    private static partial Regex EnglishWordRegex();

    public static (string Source, string Target) DetectDirection(string text)
    {
        var chinese = ChineseRegex().Matches(text).Count;
        var englishWords = EnglishWordRegex().Matches(text).Count;
        return chinese > 0 && chinese >= Math.Max(1, englishWords)
            ? ("zh", "en")
            : ("en", "zh");
    }

    public static bool IsTermLike(string text, string sourceLanguage)
    {
        var value = text.Trim();
        if (value.Length == 0 || SentenceEndingRegex().IsMatch(value))
            return false;

        if (sourceLanguage == "zh")
            return value.EnumerateRunes().Count() <= 12;

        return EnglishWordRegex().Matches(value).Count is > 0 and <= 5;
    }
}
