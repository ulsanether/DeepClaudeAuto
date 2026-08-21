using System.Diagnostics;
using System.Net.Sockets;
using DeepClaudeAuto.Core.Models;
using DeepClaudeAuto.Core.Services;
using Microsoft.Extensions.Logging;

namespace DeepClaudeAuto.Core.Services.Impl;

public sealed class ServerManager : IServerManager, IDisposable
{
    private readonly ILogger<ServerManager> _logger;
    private Process? _process;

    public ServerInfo Info { get; private set; } = new();
    public event EventHandler<string>? LogReceived;
    public event EventHandler<ServerInfo>? StatusChanged;

    public ServerManager(ILogger<ServerManager> logger)
    {
        _logger = logger;
    }

    public async Task StartAsync(AppConfig config, CancellationToken cancellationToken = default)
    {
        if (_process is not null && !_process.HasExited)
        {
            _logger.LogWarning("서버가 이미 실행 중입니다.");
            return;
        }

        SetStatus(ServerStatus.Starting, config.ServerPort);

        var (cmd, args) = config.BuildMode == "Docker"
            ? ("docker", $"run --rm -p {config.ServerPort}:1337 deepclaude")
            : (Path.Combine(config.InstallPath, "target", "release", "deepreasoning.exe"), string.Empty);

        if (config.BuildMode != "Docker" && !File.Exists(cmd))
        {
            _logger.LogError("실행 파일을 찾을 수 없습니다: {path}. 5단계(빌드)를 먼저 실행하세요.", cmd);
            SetStatus(ServerStatus.Failed, config.ServerPort);
            return;
        }

        _process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = cmd,
                Arguments = args,
                WorkingDirectory = config.InstallPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            },
            EnableRaisingEvents = true
        };

        _process.OutputDataReceived += (_, e) => { if (e.Data != null) RaiseLog(e.Data); };
        _process.ErrorDataReceived  += (_, e) => { if (e.Data != null) RaiseLog("[ERR] " + e.Data); };
        _process.Exited += (_, _) => SetStatus(ServerStatus.Stopped, config.ServerPort);

        _process.Start();
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();

        // 최대 10초 헬스체크 대기
        for (int i = 0; i < 10; i++)
        {
            await Task.Delay(1000, cancellationToken);
            if (await HealthCheckAsync(config.ServerPort, cancellationToken))
            {
                SetStatus(ServerStatus.Running, config.ServerPort, _process.Id);
                return;
            }
        }

        SetStatus(ServerStatus.Failed, config.ServerPort);
    }

    public Task StopAsync()
    {
        if (_process is not null && !_process.HasExited)
        {
            _process.Kill(entireProcessTree: true);
            _process.Dispose();
            _process = null;
        }
        SetStatus(ServerStatus.Stopped, Info.Port);
        return Task.CompletedTask;
    }

    public async Task<bool> HealthCheckAsync(int port, CancellationToken cancellationToken = default)
    {
        // deepreasoning에는 /health 라우트가 없으므로 TCP 연결 성공 여부로 리슨 상태를 판정합니다.
        try
        {
            using var client = new TcpClient();
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(2));
            await client.ConnectAsync("127.0.0.1", port, timeoutCts.Token);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void SetStatus(ServerStatus status, int port, int? pid = null)
    {
        Info = new ServerInfo
        {
            Status = status,
            Port = port,
            Address = $"http://localhost:{port}",
            ProcessId = pid
        };
        StatusChanged?.Invoke(this, Info);
    }

    private void RaiseLog(string line)
    {
        _logger.LogDebug(line);
        LogReceived?.Invoke(this, line);
    }

    public void Dispose()
    {
        _process?.Dispose();
    }
}
