using System.Net.Sockets;
using System.Text.RegularExpressions;
using DeepClaudeAuto.Core.Models;
using DeepClaudeAuto.Core.Services;
using Microsoft.Extensions.Logging;

namespace DeepClaudeAuto.Core.Services.Impl;

public sealed class DependencyChecker : IDependencyChecker
{
    private readonly IProcessRunner _runner;
    private readonly ILogger<DependencyChecker> _logger;

    private readonly List<DependencyCheckResult> _items = new()
    {
        new DependencyCheckResult
        {
            Name = "Python",
            Description = "Python 런타임 (3.10 이상)",
            RequiredVersion = "3.10",
            IsRequired = true,
            InstallUrl = "https://www.python.org/downloads/",
            InstallCommand = "winget install Python.Python.3.12"
        },
        new DependencyCheckResult
        {
            Name = "pip",
            Description = "Python 패키지 관리자",
            RequiredVersion = "",
            IsRequired = true,
            InstallCommand = "python -m ensurepip --upgrade"
        },
        new DependencyCheckResult
        {
            Name = "Git",
            Description = "버전 관리 (소스 클론용)",
            RequiredVersion = "2.0",
            IsRequired = true,
            InstallUrl = "https://git-scm.com/download/win",
            InstallCommand = "winget install Git.Git"
        },
        new DependencyCheckResult
        {
            Name = "Rust / Cargo",
            Description = "Rust 빌드 도구 (소스 빌드 방식)",
            RequiredVersion = "1.70",
            IsRequired = false,
            InstallUrl = "https://rustup.rs/",
            InstallCommand = "winget install Rustlang.Rustup"
        },
        new DependencyCheckResult
        {
            Name = "Docker",
            Description = "컨테이너 런타임 (Docker 방식)",
            RequiredVersion = "20.0",
            IsRequired = false,
            InstallUrl = "https://www.docker.com/products/docker-desktop/",
            InstallCommand = "winget install Docker.DockerDesktop"
        }
    };

    public IReadOnlyList<DependencyCheckResult> Items => _items.AsReadOnly();

    public DependencyChecker(IProcessRunner runner, ILogger<DependencyChecker> logger)
    {
        _runner = runner;
        _logger = logger;
    }

    public async Task<IReadOnlyList<DependencyCheckResult>> CheckAllAsync(
        IProgress<DependencyCheckResult>? progress = null,
        CancellationToken cancellationToken = default)
    {
        foreach (var item in _items)
        {
            item.Status = CheckStatus.Checking;
            progress?.Report(item);

            await CheckItemCoreAsync(item, cancellationToken);
            progress?.Report(item);

            _logger.LogDebug("[{name}] {status} — {message}", item.Name, item.Status, item.Message);
        }
        return _items.AsReadOnly();
    }

    public async Task<DependencyCheckResult> CheckItemAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        var item = _items.FirstOrDefault(i => i.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                   ?? throw new ArgumentException($"Unknown dependency: {name}", nameof(name));

        item.Status = CheckStatus.Checking;
        await CheckItemCoreAsync(item, cancellationToken);
        return item;
    }

    private async Task CheckItemCoreAsync(DependencyCheckResult item, CancellationToken ct)
    {
        try
        {
            string cmd;
            string args;
            Func<string, (bool ok, bool warning, string version)> parser;

            switch (item.Name)
            {
                case "Python":       cmd = "python"; args = "--version"; parser = ParseVersion; break;
                case "pip":          cmd = "pip";    args = "--version"; parser = ParseVersion; break;
                case "Git":          cmd = "git";    args = "--version"; parser = ParseVersion; break;
                case "Rust / Cargo": cmd = "cargo";  args = "--version"; parser = ParseVersion; break;
                case "Docker":       cmd = "docker"; args = "--version"; parser = ParseVersion; break;
                default: throw new InvalidOperationException($"No checker for {item.Name}");
            }

            var (exit, output, error) = await _runner.RunAsync(cmd, args, ct);
            var text = string.IsNullOrWhiteSpace(output) ? error : output;

            if (exit != 0 || string.IsNullOrWhiteSpace(text))
            {
                item.Status = item.IsRequired ? CheckStatus.Failed : CheckStatus.Warning;
                item.Message = item.IsRequired ? "설치되지 않았습니다." : "설치되지 않음 (선택사항)";
                return;
            }

            var (ok, warn, version) = parser(text);
            item.DetectedVersion = version;

            if (!string.IsNullOrWhiteSpace(item.RequiredVersion) && !ok)
            {
                item.Status = warn ? CheckStatus.Warning : CheckStatus.Failed;
                item.Message = $"버전 {version} 감지됨. {item.RequiredVersion} 이상 필요.";
            }
            else
            {
                item.Status = CheckStatus.Passed;
                item.Message = $"버전 {version} ✔";
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Dependency check failed for {name}", item.Name);
            item.Status = item.IsRequired ? CheckStatus.Failed : CheckStatus.Warning;
            item.Message = "확인 중 오류 발생: " + ex.Message;
        }
    }

    private static (bool ok, bool warning, string version) ParseVersion(string output)
    {
        var match = Regex.Match(output, @"(\d+)\.(\d+)(?:\.(\d+))?");
        if (!match.Success) return (true, false, output.Trim());

        var version = match.Value;
        return (true, false, version);
    }
}
