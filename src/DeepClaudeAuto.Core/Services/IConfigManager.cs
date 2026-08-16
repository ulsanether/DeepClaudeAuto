using DeepClaudeAuto.Core.Models;

namespace DeepClaudeAuto.Core.Services;

public interface IConfigManager
{
    AppConfig Current { get; }
    void Save(AppConfig config);
    AppConfig Load();
    string ConfigFilePath { get; }
}
