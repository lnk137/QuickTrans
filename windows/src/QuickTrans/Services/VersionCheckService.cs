using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Windows;

namespace QuickTrans.Services;

public sealed class VersionCheckService(HttpClient httpClient)
{
    private readonly HttpClient _httpClient = httpClient;

    public async Task CheckAsync(CancellationToken cancellationToken = default)
    {
        var manifestUrl = Assembly.GetExecutingAssembly()
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == "UpdateManifestUrl")?.Value;
        if (!Uri.TryCreate(manifestUrl, UriKind.Absolute, out var uri))
            return;

        try
        {
            using var response = await _httpClient.GetAsync(uri, cancellationToken);
            if (!response.IsSuccessStatusCode) return;
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var latestText = json.RootElement.GetProperty("version").GetString();
            var downloadUrl = json.RootElement.GetProperty("downloadUrl").GetString();
            var current = Assembly.GetExecutingAssembly().GetName().Version;
            if (!Version.TryParse(latestText, out var latest) || current is null || latest <= current ||
                !Uri.TryCreate(downloadUrl, UriKind.Absolute, out var downloadUri))
                return;

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (MessageBox.Show($"QuickTrans {latest} 已发布，是否打开下载页面？", "发现新版本",
                        MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                    Process.Start(new ProcessStartInfo(downloadUri.AbsoluteUri) { UseShellExecute = true });
            });
        }
        catch
        {
            // A private or temporarily unavailable manifest must never interrupt translation.
        }
    }
}
