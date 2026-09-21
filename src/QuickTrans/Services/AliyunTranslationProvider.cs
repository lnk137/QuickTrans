using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using QuickTrans.Models;

namespace QuickTrans.Services;

public sealed class AliyunTranslationProvider(HttpClient httpClient, Func<AliyunSettings> settingsAccessor) : ITranslationProvider
{
    private static readonly Uri Endpoint = new("https://mt.cn-hangzhou.aliyuncs.com/");
    private readonly HttpClient _httpClient = httpClient;
    private readonly Func<AliyunSettings> _settingsAccessor = settingsAccessor;

    public ProviderDescriptor Descriptor { get; } = new(ProviderIds.Aliyun, "阿里云翻译", false);
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
            throw new TranslationException("阿里云翻译尚未配置。 ");

        var parameters = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["AccessKeyId"] = settings.AccessKeyId,
            ["Action"] = "TranslateGeneral",
            ["Format"] = "JSON",
            ["FormatType"] = "text",
            ["Scene"] = "general",
            ["SignatureMethod"] = "HMAC-SHA1",
            ["SignatureNonce"] = Guid.NewGuid().ToString("N"),
            ["SignatureVersion"] = "1.0",
            ["SourceLanguage"] = request.SourceLanguage,
            ["SourceText"] = request.Text,
            ["TargetLanguage"] = request.TargetLanguage,
            ["Timestamp"] = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"),
            ["Version"] = "2018-10-12"
        };
        var canonicalized = string.Join("&", parameters.Select(pair => $"{PercentEncode(pair.Key)}={PercentEncode(pair.Value)}"));
        var stringToSign = "POST&%2F&" + PercentEncode(canonicalized);
        using var hmac = new HMACSHA1(Encoding.UTF8.GetBytes(settings.AccessKeySecret + "&"));
        parameters["Signature"] = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign)));

        using var message = new HttpRequestMessage(HttpMethod.Post, Endpoint)
        {
            Content = new FormUrlEncodedContent(parameters)
        };
        using var response = await _httpClient.SendAsync(message, cancellationToken);
        await ProviderHttp.EnsureSuccessAsync(response, cancellationToken);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));

        var root = json.RootElement;
        var code = root.TryGetProperty("Code", out var codeNode) ? codeNode.GetString() : null;
        if (!string.IsNullOrWhiteSpace(code) && code != "200")
            throw new TranslationException(MapError(code));

        string? translated = null;
        if (root.TryGetProperty("Data", out var data) && data.TryGetProperty("Translated", out var value))
            translated = value.GetString();
        if (string.IsNullOrWhiteSpace(translated))
            throw new TranslationException("阿里云翻译没有返回可用译文。 ");

        yield return new TranslationChunk(translated, true);
    }

    internal static string PercentEncode(string value) => Uri.EscapeDataString(value)
        .Replace("+", "%20", StringComparison.Ordinal)
        .Replace("*", "%2A", StringComparison.Ordinal)
        .Replace("%7E", "~", StringComparison.Ordinal);

    private static string MapError(string code) => code switch
    {
        "InvalidAccessKeyId.NotFound" or "SignatureDoesNotMatch" => "阿里云翻译鉴权失败，请检查 AccessKey。",
        "Throttling.User" or "Throttling.Api" => "阿里云翻译请求过于频繁，请稍后重试。",
        "Forbidden.RAM" => "当前 AccessKey 没有机器翻译权限。",
        "QuotaExceed" => "阿里云翻译额度不足，请检查账户状态。",
        _ => $"阿里云翻译返回错误（{code}）。"
    };
}
