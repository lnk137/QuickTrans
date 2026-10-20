namespace QuickTrans.Models;

public static class ProviderIds
{
    public const string OpenAi = "openai";
    public const string Baidu = "baidu";
    public const string Aliyun = "aliyun";
}

public enum TranslationMode
{
    Translate,
    Explain
}

public sealed record TranslationRequest(
    string Text,
    string SourceLanguage,
    string TargetLanguage,
    TranslationMode Mode,
    bool IsTerm,
    string PromptVersion = "v1");

public sealed record TranslationChunk(string Text, bool IsFinal = false);

public sealed record ProviderDescriptor(string Id, string DisplayName, bool SupportsExplanation);

public sealed class TranslationException(string userMessage, Exception? inner = null) : Exception(userMessage, inner)
{
    public string UserMessage { get; } = userMessage;
}
