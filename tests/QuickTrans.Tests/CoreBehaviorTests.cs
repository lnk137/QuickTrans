using QuickTrans.Models;
using QuickTrans.Services;

namespace QuickTrans.Tests;

public sealed class CoreBehaviorTests
{
    [Theory]
    [InlineData("hello world", "en", "zh")]
    [InlineData("你好世界", "zh", "en")]
    [InlineData("你好 QuickTrans", "zh", "en")]
    public void DetectDirection_UsesChineseMajority(string text, string source, string target)
    {
        Assert.Equal((source, target), LanguageService.DetectDirection(text));
    }

    [Theory]
    [InlineData("hello", "en", true)]
    [InlineData("state of the art", "en", true)]
    [InlineData("This is a complete sentence.", "en", false)]
    [InlineData("常用表达", "zh", true)]
    [InlineData("这是一个完整的长句子没有标点但是超过十二个字符", "zh", false)]
    public void IsTermLike_AppliesDeterministicLimits(string text, string source, bool expected)
    {
        Assert.Equal(expected, LanguageService.IsTermLike(text, source));
    }

    [Fact]
    public void Validator_NormalizesLineEndingsAndTrims()
    {
        Assert.Equal("one\ntwo", TranslationRequestValidator.NormalizeAndValidate("  one\r\ntwo  "));
    }

    [Fact]
    public void Validator_RejectsOverLimit()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            TranslationRequestValidator.NormalizeAndValidate(new string('a', 2001)));
        Assert.Contains("2,000", error.Message);
    }

    [Fact]
    public void PromptBuilder_TermPromptDoesNotRequestExtraDetail()
    {
        var request = new TranslationRequest("hello", "en", "zh", TranslationMode.Translate, true);
        var prompt = PromptBuilder.BuildSystemPrompt(request, null);
        Assert.Contains("1到3个", prompt);
        Assert.Contains("不要音标", prompt);
    }

    [Fact]
    public void SessionCache_SeparatesProvidersAndModes()
    {
        var cache = new SessionTranslationCache();
        var translate = new TranslationRequest("hello", "en", "zh", TranslationMode.Translate, true);
        var explain = translate with { Mode = TranslationMode.Explain };
        cache.Set(ProviderIds.OpenAi, translate, "model", "你好");
        Assert.True(cache.TryGet(ProviderIds.OpenAi, translate, "model", out var result));
        Assert.Equal("你好", result);
        Assert.False(cache.TryGet(ProviderIds.Baidu, translate, "", out _));
        Assert.False(cache.TryGet(ProviderIds.OpenAi, explain, "model", out _));
    }

    [Fact]
    public void ThemeValidation_ChecksEveryCustomColor()
    {
        var settings = new ThemeSettings { Mode = ThemeModeIds.Custom, CustomPalette = ThemeService.CreateDarkPalette() };
        Assert.True(ThemeService.TryValidate(settings, out _));

        settings.CustomPalette.Border = "blue";
        Assert.False(ThemeService.TryValidate(settings, out var error));
        Assert.Contains("边框", error);
    }
}
