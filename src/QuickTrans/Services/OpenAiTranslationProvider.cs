using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using QuickTrans.Models;

namespace QuickTrans.Services;

public sealed class OpenAiTranslationProvider(HttpClient httpClient, Func<OpenAiSettings> settingsAccessor) : ITranslationProvider
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly Func<OpenAiSettings> _settingsAccessor = settingsAccessor;

    public ProviderDescriptor Descriptor { get; } = new(ProviderIds.OpenAi, "AI 翻译", true);
    public bool IsConfigured => _settingsAccessor().IsConfigured;
    public string Model => _settingsAccessor().Model;

    public async Task ValidateConfigurationAsync(CancellationToken cancellationToken)
    {
        var settings = RequireSettings();
        using var message = CreateRequest(settings, new TranslationRequest("test", "en", "zh", TranslationMode.Translate, true), false, 32);
        using var response = await _httpClient.SendAsync(message, HttpCompletionOption.ResponseContentRead, cancellationToken);
        await ProviderHttp.EnsureSuccessAsync(response, cancellationToken);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        // Array navigation is handled explicitly because JSON object path helpers intentionally stay strict.
        if (json.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0 &&
            choices[0].TryGetProperty("message", out var resultMessage) &&
            resultMessage.TryGetProperty("content", out var resultContent) &&
            !string.IsNullOrWhiteSpace(resultContent.GetString()))
            return;
        throw new TranslationException("接口连接成功，但没有返回可用内容。请检查模型名称。 ");
    }

    public async IAsyncEnumerable<TranslationChunk> TranslateAsync(
        TranslationRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var settings = RequireSettings();
        using var message = CreateRequest(settings, request, true, request.Mode == TranslationMode.Explain ? 500 : 1000);
        using var response = await _httpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        await ProviderHttp.EnsureSuccessAsync(response, cancellationToken);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);
        var firstChunk = true;
        var emitted = false;

        while (true)
        {
            var timeout = firstChunk ? TimeSpan.FromSeconds(15) : TimeSpan.FromSeconds(30);
            string? line;
            try
            {
                line = await reader.ReadLineAsync(cancellationToken).AsTask().WaitAsync(timeout, cancellationToken);
            }
            catch (TimeoutException ex)
            {
                throw new TranslationException(firstChunk ? "AI 服务首包等待超时。" : "AI 服务输出中断超时。", ex);
            }

            if (line is null)
                break;
            if (!line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                continue;

            var payload = line[5..].Trim();
            if (payload == "[DONE]")
                break;
            if (payload.Length == 0)
                continue;

            string? content = null;
            try
            {
                using var json = JsonDocument.Parse(payload);
                if (!json.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
                    continue;
                var choice = choices[0];
                if (!choice.TryGetProperty("delta", out var delta) ||
                    !delta.TryGetProperty("content", out var contentNode))
                    continue;
                content = contentNode.GetString();
            }
            catch (JsonException)
            {
                // Ignore malformed keepalive/event frames. If every frame is malformed,
                // the user receives the safe empty-result error below.
            }

            if (string.IsNullOrEmpty(content))
                continue;
            firstChunk = false;
            emitted = true;
            yield return new TranslationChunk(content);
        }

        if (!emitted)
            throw new TranslationException("AI 服务没有返回可用译文。 ");
        yield return new TranslationChunk(string.Empty, true);
    }

    private OpenAiSettings RequireSettings()
    {
        var settings = _settingsAccessor();
        if (!settings.IsConfigured)
            throw new TranslationException("AI 引擎尚未配置。 ");
        return settings;
    }

    private static HttpRequestMessage CreateRequest(OpenAiSettings settings, TranslationRequest request, bool stream, int maxTokens)
    {
        var endpoint = new Uri(settings.BaseUrl.TrimEnd('/') + "/chat/completions");
        var requestBody = new Dictionary<string, object?>
        {
            ["model"] = settings.Model,
            ["stream"] = stream,
            ["temperature"] = 0.1,
            ["max_tokens"] = maxTokens,
            ["messages"] = new object[]
            {
                new { role = "system", content = PromptBuilder.BuildSystemPrompt(request, settings.CustomPrompt) },
                new { role = "user", content = request.Text }
            }
        };

        // DeepSeek V4 enables high-effort thinking by default. Translation benefits from
        // lower latency, and short validation responses can otherwise spend their entire
        // token budget on reasoning_content without ever producing message.content.
        if (IsDeepSeekEndpoint(settings.BaseUrl))
            requestBody["thinking"] = new { type = "disabled" };

        var body = JsonSerializer.Serialize(requestBody);
        var message = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
        return message;
    }

    private static bool IsDeepSeekEndpoint(string baseUrl) =>
        Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) &&
        uri.Host.Equals("api.deepseek.com", StringComparison.OrdinalIgnoreCase);
}
