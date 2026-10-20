using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using QuickTrans.Models;

namespace QuickTrans.Services;

public sealed class BaiduTranslationProvider(HttpClient httpClient, Func<BaiduSettings> settingsAccessor) : ITranslationProvider
{
    private static readonly Uri Endpoint = new("https://fanyi-api.baidu.com/api/trans/vip/translate");
    private readonly HttpClient _httpClient = httpClient;
    private readonly Func<BaiduSettings> _settingsAccessor = settingsAccessor;

    public ProviderDescriptor Descriptor { get; } = new(ProviderIds.Baidu, "百度翻译", false);
    public bool IsConfigured => _settingsAccessor().IsConfigured;

    public async Task ValidateConfigurationAsync(CancellationToken cancellationToken)
    {
        await foreach (var _ in TranslateAsync(new TranslationRequest("test", "en", "zh", TranslationMode.Translate, true), cancellationToken))
        {
        }
    }

    public async IAsyncEnumerable<TranslationChunk> TranslateAsync(
        TranslationRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var settings = _settingsAccessor();
        if (!settings.IsConfigured)
            throw new TranslationException("百度翻译尚未配置。 ");

        var salt = Guid.NewGuid().ToString("N");
        var signature = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(settings.AppId + request.Text + salt + settings.SecretKey))).ToLowerInvariant();
        var fields = new Dictionary<string, string>
        {
            ["q"] = request.Text,
            ["from"] = request.SourceLanguage,
            ["to"] = request.TargetLanguage,
            ["appid"] = settings.AppId,
            ["salt"] = salt,
            ["sign"] = signature
        };

        using var message = new HttpRequestMessage(HttpMethod.Post, Endpoint) { Content = new FormUrlEncodedContent(fields) };
        using var response = await _httpClient.SendAsync(message, cancellationToken);
        await ProviderHttp.EnsureSuccessAsync(response, cancellationToken);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));

        if (json.RootElement.TryGetProperty("error_code", out var errorCode))
            throw new TranslationException(MapError(errorCode.GetString()));
        if (!json.RootElement.TryGetProperty("trans_result", out var results) || results.ValueKind != JsonValueKind.Array)
            throw new TranslationException("百度翻译没有返回可用译文。 ");

        var translated = string.Join("\n", results.EnumerateArray()
            .Select(item => item.TryGetProperty("dst", out var value) ? value.GetString() : null)
            .Where(value => !string.IsNullOrWhiteSpace(value)));
        if (translated.Length == 0)
            throw new TranslationException("百度翻译没有返回可用译文。 ");

        yield return new TranslationChunk(translated, true);
    }

    private static string MapError(string? code) => code switch
    {
        "52001" => "百度翻译请求超时，请稍后重试。",
        "52002" => "百度翻译系统错误，请稍后重试。",
        "52003" or "54001" => "百度翻译鉴权失败，请检查 App ID 和密钥。",
        "54003" => "百度翻译请求过于频繁，请稍后重试。",
        "54004" => "百度翻译额度不足，请检查账户状态。",
        "58001" => "百度翻译不支持当前语言方向。",
        _ => $"百度翻译返回错误（{code ?? "未知"}）。"
    };
}
