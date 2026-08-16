using DeepClaudeAuto.Core.Models;
using DeepClaudeAuto.Core.Services;
using Microsoft.Extensions.Logging;

namespace DeepClaudeAuto.Core.Services.Impl;

public sealed class BuilderService : IBuilderService
{
    private readonly IProcessRunner _runner;
    private readonly ILogger<BuilderService> _logger;

    public BuilderService(IProcessRunner runner, ILogger<BuilderService> logger)
    {
        _runner = runner;
        _logger = logger;
    }

    public async Task CloneRepositoryAsync(
        string repoUrl,
        string targetPath,
        Action<string> onLog,
        CancellationToken cancellationToken = default)
    {
        if (Directory.Exists(targetPath) && Directory.GetFiles(targetPath).Length > 0)
        {
            onLog($"[INFO] 이미 존재하는 경로입니다: {targetPath}. git pull 실행 중...");
            await _runner.RunWithStreamingAsync("git", "pull", onLog, onLog, targetPath, cancellationToken);
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        onLog($"[INFO] 저장소 클론 중: {repoUrl} → {targetPath}");
        await _runner.RunWithStreamingAsync(
            "git", $"clone {repoUrl} \"{targetPath}\"",
            onLog, onLog, null, cancellationToken);
    }

    public async Task InstallDependenciesAsync(
        string projectPath,
        string buildMode,
        Action<string> onLog,
        CancellationToken cancellationToken = default)
    {
        if (buildMode == "Docker")
        {
            onLog("[INFO] Docker 이미지 빌드 중...");
            await _runner.RunWithStreamingAsync(
                "docker", "build -t deepclaude .",
                onLog, onLog, projectPath, cancellationToken);
        }
        else
        {
            onLog("[INFO] Python 의존성 설치 중...");
            await _runner.RunWithStreamingAsync(
                "pip", "install -r requirements.txt",
                onLog, onLog, projectPath, cancellationToken);
        }
    }

    public async Task WriteEnvFileAsync(AppConfig config, string projectPath)
    {
        var envPath = Path.Combine(projectPath, ".env");
        var content = $"""
ANTHROPIC_API_KEY={config.AnthropicApiKey}
DEEPSEEK_API_KEY={config.DeepSeekApiKey}
PORT={config.ServerPort}
""";
        await File.WriteAllTextAsync(envPath, content);
        _logger.LogInformation(".env 파일 작성 완료: {path}", envPath);
    }
}
