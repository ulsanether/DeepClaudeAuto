namespace DeepClaudeAuto.Core.Models;

public enum ServerStatus
{
    Stopped,
    Starting,
    Running,
    Failed
}

public class ServerInfo
{
    public ServerStatus Status { get; set; } = ServerStatus.Stopped;
    public string Address { get; set; } = string.Empty;
    public int Port { get; set; }
    public int? ProcessId { get; set; }
}
