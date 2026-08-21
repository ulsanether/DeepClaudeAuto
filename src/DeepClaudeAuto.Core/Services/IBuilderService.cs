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

    /// <summary>config.toml의 [server] port를 지정한 포트로 동기화합니다.</summary>
    Task SyncConfigTomlAsync(string projectPath, int port);

    /// <summary>Windows 빌드를 막는 vendored openssl 의존성 라인을 Cargo.toml에서 제거합니다.</summary>
    Task PatchCargoTomlAsync(string projectPath);
}
