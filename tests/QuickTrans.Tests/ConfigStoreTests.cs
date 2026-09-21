using QuickTrans.Models;
using QuickTrans.Services;

namespace QuickTrans.Tests;

public sealed class ConfigStoreTests
{
    [Fact]
    public void RoundTrip_PreservesSettings()
    {
        var directory = Path.Combine(Path.GetTempPath(), "QuickTrans.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            var store = new ConfigStore(directory);
            var settings = new AppSettings
            {
                DefaultProvider = ProviderIds.Baidu,
                Hotkey = "Ctrl+Alt+D",
                SetupCompleted = true,
                Theme = new ThemeSettings
                {
                    Mode = ThemeModeIds.Custom,
                    CustomPalette = new ThemePalette { Accent = "#B87333", Window = "#15120F" }
                },
                Baidu = new BaiduSettings { AppId = "id", SecretKey = "plain-secret" }
            };
            store.Save(settings);
            var loaded = store.Load();
            Assert.Equal(settings.DefaultProvider, loaded.DefaultProvider);
            Assert.Equal("plain-secret", loaded.Baidu.SecretKey);
            Assert.Equal(ThemeModeIds.Custom, loaded.Theme.Mode);
            Assert.Equal("#B87333", loaded.Theme.CustomPalette.Accent);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void Load_OldConfigurationCreatesDefaultTheme()
    {
        var directory = Path.Combine(Path.GetTempPath(), "QuickTrans.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "config.json"), "{\"DefaultProvider\":\"openai\"}");
            var loaded = new ConfigStore(directory).Load();
            Assert.NotNull(loaded.Theme);
            Assert.Equal(ThemeModeIds.System, loaded.Theme.Mode);
            Assert.NotNull(loaded.Theme.CustomPalette);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
