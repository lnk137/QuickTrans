using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using QuickTrans.Models;

namespace QuickTrans.Services;

public sealed class SessionTranslationCache
{
    private readonly ConcurrentDictionary<string, string> _items = new();

    public bool TryGet(string providerId, TranslationRequest request, string model, out string value) =>
        _items.TryGetValue(CreateKey(providerId, request, model), out value!);

    public void Set(string providerId, TranslationRequest request, string model, string value) =>
        _items[CreateKey(providerId, request, model)] = value;

    public static string CreateKey(string providerId, TranslationRequest request, string model)
    {
        var raw = string.Join("\u001f", providerId, model, request.SourceLanguage, request.TargetLanguage,
            request.Mode, request.IsTerm, request.PromptVersion, request.Text);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
    }
}
