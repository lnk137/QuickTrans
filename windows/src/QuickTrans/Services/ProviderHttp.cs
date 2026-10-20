using System.Net;
using System.Text.Json;
using QuickTrans.Models;

namespace QuickTrans.Services;

internal static class ProviderHttp
{
    public static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        var message = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "鉴权失败，请检查 API 凭据。",
            HttpStatusCode.TooManyRequests => "请求过于频繁或额度已用完，请稍后重试或切换引擎。",
            HttpStatusCode.BadRequest => "服务拒绝了请求，请检查模型和接口配置。",
            _ when (int)response.StatusCode >= 500 => "翻译服务暂时不可用，请稍后重试。",
            _ => $"翻译服务返回错误（{(int)response.StatusCode}）。"
        };

        // Consume the body so the connection can be reused, but never expose it: providers
        // sometimes echo credentials or source text in diagnostic responses.
        _ = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new TranslationException(message);
    }

    public static string? ReadString(JsonElement element, params string[] path)
    {
        foreach (var segment in path)
        {
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(segment, out element))
                return null;
        }
        return element.ValueKind == JsonValueKind.String ? element.GetString() : element.ToString();
    }
}
