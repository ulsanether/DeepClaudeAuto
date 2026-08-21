using System.Text.RegularExpressions;
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
            await RunCheckedAsync("git", "pull", onLog, targetPath, cancellationToken);
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        onLog($"[INFO] 저장소 클론 중: {repoUrl} → {targetPath}");
        await RunCheckedAsync(
            "git", $"clone {repoUrl} \"{targetPath}\"",
            onLog, null, cancellationToken);
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
            await RunCheckedAsync(
                "docker", "build -t deepclaude .",
                onLog, projectPath, cancellationToken);
        }
        else
        {
            onLog("[INFO] Rust 릴리즈 빌드 중... (cargo build --release)");
            await RunCheckedAsync(
                "cargo", "build --release",
                onLog, projectPath, cancellationToken);
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

    /// <summary>
    /// deepreasoning 서버가 읽는 config.toml의 [server] port를 사용자가 선택한 포트와 동기화합니다.
    /// 파일이 없으면 최소 구성으로 생성합니다.
    /// </summary>
    public async Task SyncConfigTomlAsync(string projectPath, int port)
    {
        var configPath = Path.Combine(projectPath, "config.toml");

        if (!File.Exists(configPath))
        {
            await File.WriteAllTextAsync(configPath, $"""
[server]
host = "127.0.0.1"
port = {port}
""");
            _logger.LogInformation("config.toml 생성 완료: {path} (port={port})", configPath, port);
            return;
        }

        var content = await File.ReadAllTextAsync(configPath);
        var updated = Regex.Replace(content, @"(?m)^port\s*=\s*\d+", $"port = {port}");

        if (updated == content)
        {
            _logger.LogWarning("config.toml에서 [server] port 항목을 찾지 못했습니다: {path}", configPath);
            return;
        }

        await File.WriteAllTextAsync(configPath, updated);
        _logger.LogInformation("config.toml 포트 동기화 완료: {path} (port={port})", configPath, port);
    }

    /// <summary>
    /// 상류 저장소의 Cargo.toml에는 코드에서 사용되지 않는 vendored openssl 의존성이 있어
    /// Windows에서 perl/NASM 부재로 빌드가 실패합니다. 해당 라인을 제거해 빌드를 가능하게 합니다.
    /// </summary>
    public async Task PatchCargoTomlAsync(string projectPath)
    {
        var cargoPath = Path.Combine(projectPath, "Cargo.toml");
        if (!File.Exists(cargoPath))
        {
            _logger.LogWarning("Cargo.toml을 찾을 수 없습니다: {path}", cargoPath);
            return;
        }

        var content = await File.ReadAllTextAsync(cargoPath);
        var updated = Regex.Replace(content, @"(?m)^\s*openssl\s*=\s*\{[^}]*vendored[^}]*\}\s*(\r?\n)?", "");

        if (updated == content)
        {
            _logger.LogInformation("Cargo.toml 패치 불필요 (vendored openssl 없음): {path}", cargoPath);
            return;
        }

        await File.WriteAllTextAsync(cargoPath, updated);
        _logger.LogInformation("Cargo.toml 패치 완료 (vendored openssl 제거): {path}", cargoPath);
    }

    /// <summary>스트리밍 명령을 실행하고 exit code가 0이 아니면 예외를 던집니다.</summary>
    private async Task RunCheckedAsync(
        string fileName,
        string arguments,
        Action<string> onLog,
        string? workingDirectory,
        CancellationToken cancellationToken)
    {
        var exit = await _runner.RunWithStreamingAsync(
            fileName, arguments, onLog, onLog, workingDirectory, cancellationToken);

        if (exit != 0)
        {
            throw new InvalidOperationException(
                $"'{fileName} {arguments}' 실행 실패 (exit code {exit})");
        }
    }
}
