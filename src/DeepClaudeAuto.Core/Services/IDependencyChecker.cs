using DeepClaudeAuto.Core.Models;

namespace DeepClaudeAuto.Core.Services;

public interface IDependencyChecker
{
    IReadOnlyList<DependencyCheckResult> Items { get; }

    Task<IReadOnlyList<DependencyCheckResult>> CheckAllAsync(
        IProgress<DependencyCheckResult>? progress = null,
        CancellationToken cancellationToken = default);

    Task<DependencyCheckResult> CheckItemAsync(
        string name,
        CancellationToken cancellationToken = default);
}
