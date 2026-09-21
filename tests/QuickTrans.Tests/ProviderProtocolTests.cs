using System.Net;
using System.Text;
using System.Text.Json;
using QuickTrans.Models;
using QuickTrans.Services;

namespace QuickTrans.Tests;

public sealed class ProviderProtocolTests
{
    [Fact]
    public async Task OpenAi_ConsumesStreamingChunks()
    {
        const string sse = "data: {\"choices\":[{\"delta\":{\"content\":\"你\"}}]}\n\n" +
                           "data: {\"choices\":[{\"delta\":{\"content\":\"好\"}}]}\n\n" +
                           "data: [DONE]\n\n";
        using var client = new HttpClient(new StubHandler(_ => Response(sse, "text/event-stream")));
        var settings = new OpenAiSettings { BaseUrl = "https://example.test/v1", ApiKey = "secret", Model = "test" };
        var provider = new OpenAiTranslationProvider(client, () => settings);
        var result = await CollectAsync(provider.TranslateAsync(Request(), CancellationToken.None));
        Assert.Equal("你好", result);
    }

    [Fact]
    public async Task DeepSeek_DisablesThinkingForLowLatencyTranslation()
    {
        string? capturedBody = null;
        using var client = new HttpClient(new StubHandler(request =>
        {
            capturedBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return Response("{\"choices\":[{\"message\":{\"content\":\"测试\"}}]}");
        }));
        var settings = new OpenAiSettings
        {
            BaseUrl = "https://api.deepseek.com", ApiKey = "secret", Model = "deepseek-v4-flash"
        };
        var provider = new OpenAiTranslationProvider(client, () => settings);

        await provider.ValidateConfigurationAsync(CancellationToken.None);

        using var json = JsonDocument.Parse(capturedBody!);
        Assert.Equal("disabled", json.RootElement.GetProperty("thinking").GetProperty("type").GetString());
        Assert.Equal(32, json.RootElement.GetProperty("max_tokens").GetInt32());
    }

    [Fact]
    public async Task GenericOpenAi_DoesNotReceiveVendorSpecificThinkingParameter()
    {
        string? capturedBody = null;
        using var client = new HttpClient(new StubHandler(request =>
        {
            capturedBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return Response("{\"choices\":[{\"message\":{\"content\":\"测试\"}}]}");
        }));
        var provider = new OpenAiTranslationProvider(client, () => new OpenAiSettings
        {
            BaseUrl = "https://example.test/v1", ApiKey = "secret", Model = "test"
        });

        await provider.ValidateConfigurationAsync(CancellationToken.None);

        using var json = JsonDocument.Parse(capturedBody!);
        Assert.False(json.RootElement.TryGetProperty("thinking", out _));
    }

    [Fact]
    public async Task Baidu_ParsesTranslationAndPostsSignature()
    {
        HttpRequestMessage? captured = null;
        using var client = new HttpClient(new StubHandler(request =>
        {
            captured = request;
            return Response("{\"from\":\"en\",\"to\":\"zh\",\"trans_result\":[{\"src\":\"hello\",\"dst\":\"你好\"}]}");
        }));
        var settings = new BaiduSettings { AppId = "app", SecretKey = "secret" };
        var provider = new BaiduTranslationProvider(client, () => settings);
        var result = await CollectAsync(provider.TranslateAsync(Request(), CancellationToken.None));
        Assert.Equal("你好", result);
        Assert.Equal(HttpMethod.Post, captured!.Method);
    }

    [Fact]
    public async Task Aliyun_ParsesTranslation()
    {
        using var client = new HttpClient(new StubHandler(_ => Response("{\"Code\":\"200\",\"Data\":{\"Translated\":\"你好\"}}")));
        var settings = new AliyunSettings { AccessKeyId = "id", AccessKeySecret = "secret" };
        var provider = new AliyunTranslationProvider(client, () => settings);
        Assert.Equal("你好", await CollectAsync(provider.TranslateAsync(Request(), CancellationToken.None)));
    }

    [Fact]
    public async Task ProviderError_DoesNotExposeResponseBody()
    {
        using var client = new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("echoed secret and source text")
        }));
        var provider = new OpenAiTranslationProvider(client, () => new OpenAiSettings
        {
            BaseUrl = "https://example.test/v1", ApiKey = "secret", Model = "test"
        });
        var error = await Assert.ThrowsAsync<TranslationException>(async () =>
            await CollectAsync(provider.TranslateAsync(Request(), CancellationToken.None)));
        Assert.DoesNotContain("secret", error.Message);
        Assert.DoesNotContain("source", error.Message);
    }

    private static TranslationRequest Request() => new("hello", "en", "zh", TranslationMode.Translate, true);

    private static async Task<string> CollectAsync(IAsyncEnumerable<TranslationChunk> chunks)
    {
        var result = new StringBuilder();
        await foreach (var chunk in chunks)
            result.Append(chunk.Text);
        return result.ToString();
    }

    private static HttpResponseMessage Response(string body, string mediaType = "application/json") => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, Encoding.UTF8, mediaType)
    };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(response(request));
    }
}
