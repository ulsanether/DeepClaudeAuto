using DeepClaudeAuto.Core.Models;

namespace DeepClaudeAuto.Core.Services;

public interface IServerManager
{
    ServerInfo Info { get; }
    event EventHandler<string> LogReceived;
    event EventHandler<ServerInfo> StatusChanged;

    Task StartAsync(AppConfig config, CancellationToken cancellationToken = default);
    Task StopAsync();
    Task<bool> HealthCheckAsync(int port, CancellationToken cancellationToken = default);
}
