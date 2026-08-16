using System.Text.Json;
using DeepClaudeAuto.Core.Models;
using DeepClaudeAuto.Core.Services;

namespace DeepClaudeAuto.Core.Services.Impl;

public sealed class ConfigManager : IConfigManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public string ConfigFilePath { get; }

    public AppConfig Current { get; private set; }

    public ConfigManager()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "DeepClaudeAuto");
        Directory.CreateDirectory(dir);
        ConfigFilePath = Path.Combine(dir, "config.json");
        Current = Load();
    }

    public AppConfig Load()
    {
        if (!File.Exists(ConfigFilePath))
        {
            Current = new AppConfig();
            return Current;
        }

        try
        {
            var json = File.ReadAllText(ConfigFilePath);
            Current = JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
        }
        catch
        {
            Current = new AppConfig();
        }
        return Current;
    }

    public void Save(AppConfig config)
    {
        Current = config;
        var json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(ConfigFilePath, json);
    }
}
