namespace DeepClaudeAuto.Core.Models;

public class AppConfig
{
    public string AnthropicApiKey { get; set; } = string.Empty;
    public string DeepSeekApiKey { get; set; } = string.Empty;
    public int ServerPort { get; set; } = 3000;
    public string BuildMode { get; set; } = "Source"; // Source | Docker
    public string InstallPath { get; set; } = string.Empty;
    public bool AutoStartServer { get; set; } = false;
    public string DeepClaudeRepoUrl { get; set; } = "https://github.com/getasterisk/deepclaude";
}
