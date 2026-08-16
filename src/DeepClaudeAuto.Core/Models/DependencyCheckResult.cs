namespace DeepClaudeAuto.Core.Models;

public enum CheckStatus
{
    Pending,
    Checking,
    Passed,
    Warning,
    Failed
}

public class DependencyCheckResult
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public CheckStatus Status { get; set; } = CheckStatus.Pending;
    public string DetectedVersion { get; set; } = string.Empty;
    public string RequiredVersion { get; init; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsRequired { get; init; } = true;
    public string InstallCommand { get; init; } = string.Empty;
    public string InstallUrl { get; init; } = string.Empty;
}
