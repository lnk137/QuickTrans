using System.Text.Json;
using QuickTrans.Models;

namespace QuickTrans.Services;

public sealed class ConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public ConfigStore(string? configDirectory = null)
    {
        var environmentOverride = Environment.GetEnvironmentVariable("QUICKTRANS_CONFIG_DIR");
        ConfigDirectory = configDirectory
            ?? (!string.IsNullOrWhiteSpace(environmentOverride) ? Path.GetFullPath(environmentOverride) : null)
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuickTrans");
    }

    public string ConfigDirectory { get; }
    public string ConfigPath => Path.Combine(ConfigDirectory, "config.json");

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(ConfigPath))
                return new AppSettings();
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(ConfigPath), JsonOptions);
            if (settings is null)
                return new AppSettings();
            settings.Theme ??= new ThemeSettings();
            settings.Theme.CustomPalette ??= new ThemePalette();
            return settings;
        }
        catch (JsonException)
        {
            return new AppSettings();
        }
        catch (IOException)
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(ConfigDirectory);
        var temporaryPath = ConfigPath + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(settings, JsonOptions));
        File.Move(temporaryPath, ConfigPath, true);
    }
}
