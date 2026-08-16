using DeepClaudeAuto.Core.Models;

namespace DeepClaudeAuto.Core.Services;

public interface IBuilderService
{
    Task CloneRepositoryAsync(
        string repoUrl,
        string targetPath,
        Action<string> onLog,
        CancellationToken cancellationToken = default);

    Task InstallDependenciesAsync(
        string projectPath,
        string buildMode,
        Action<string> onLog,
        CancellationToken cancellationToken = default);

    Task WriteEnvFileAsync(AppConfig config, string projectPath);
}
