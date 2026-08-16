using DeepClaudeAuto.Core.Models;
using DeepClaudeAuto.Core.Services;
using DeepClaudeAuto.Core.Services.Impl;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DeepClaudeAuto.Tests;

public class DependencyCheckerTests
{
    private static DependencyChecker CreateChecker(
        Func<string, string, (int, string, string)>? runnerFactory = null)
    {
        var mock = new Mock<IProcessRunner>();

        mock.Setup(r => r.RunAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string cmd, string args, CancellationToken _) =>
                runnerFactory?.Invoke(cmd, args) ?? (0, "1.0.0", string.Empty));

        return new DependencyChecker(mock.Object, NullLogger<DependencyChecker>.Instance);
    }

    [Fact]
    public async Task CheckAll_WhenAllToolsFound_AllResultsPassed()
    {
        var checker = CreateChecker((cmd, _) => (0, $"{cmd} 3.11.0", string.Empty));
        var results = await checker.CheckAllAsync();

        Assert.All(results.Where(r => r.IsRequired),
            r => Assert.Equal(CheckStatus.Passed, r.Status));
    }

    [Fact]
    public async Task CheckAll_WhenPythonMissing_RequiredItemFails()
    {
        var checker = CreateChecker((cmd, _) =>
            cmd == "python" ? (1, string.Empty, "not found") : (0, "version 1.0", string.Empty));

        var results = await checker.CheckAllAsync();
        var python = results.First(r => r.Name == "Python");

        Assert.Equal(CheckStatus.Failed, python.Status);
    }

    [Fact]
    public async Task CheckAll_WhenDockerMissing_NonRequiredGetsWarning()
    {
        var checker = CreateChecker((cmd, _) =>
            cmd == "docker" ? (1, string.Empty, "not found") : (0, "version 1.0", string.Empty));

        var results = await checker.CheckAllAsync();
        var docker = results.First(r => r.Name == "Docker");

        Assert.Equal(CheckStatus.Warning, docker.Status);
        Assert.False(docker.IsRequired);
    }

    [Fact]
    public void Items_ContainsExpectedDependencies()
    {
        var checker = CreateChecker();
        var names = checker.Items.Select(i => i.Name).ToList();

        Assert.Contains("Python", names);
        Assert.Contains("pip", names);
        Assert.Contains("Git", names);
        Assert.Contains("Docker", names);
    }
}
