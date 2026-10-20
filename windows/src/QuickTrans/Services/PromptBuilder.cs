using QuickTrans.Models;

namespace QuickTrans.Services;

public static class PromptBuilder
{
    public const string PromptVersion = "v1";

    public static string BuildSystemPrompt(TranslationRequest request, string? customPrompt)
    {
        if (!string.IsNullOrWhiteSpace(customPrompt) && request.Mode == TranslationMode.Translate)
            return customPrompt.Trim();

        var target = request.TargetLanguage == "zh" ? "简体中文" : "英文";
        if (request.Mode == TranslationMode.Explain)
            return $"你是简洁的双语语言助手。用{target}解释给定文本的语义、关键表达和可能的语境。只输出解释，不复述任务，不使用标题，控制在三句话内。";

        if (request.IsTerm)
            return $"你是精确的词语翻译器。把输入翻译为{target}，只给出1到3个最常用释义，用中文分号分隔。不要音标、词性、例句、编号、引号或说明。";

        return $"你是精确、自然的翻译器。把输入翻译为{target}，保留原有换行和语气。只输出译文，不要解释、标题、引号或其他内容。";
    }
}
