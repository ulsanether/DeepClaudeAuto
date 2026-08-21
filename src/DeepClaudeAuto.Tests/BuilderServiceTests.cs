using DeepClaudeAuto.Core.Services;
using DeepClaudeAuto.Core.Services.Impl;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DeepClaudeAuto.Tests;

public class BuilderServiceTests
{
    private static Mock<IProcessRunner> CreateRunnerMock(int exitCode = 0)
    {
        var runner = new Mock<IProcessRunner>();
        runner.Setup(r => r.RunWithStreamingAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Action<string>>(),
                It.IsAny<Action<string>?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(exitCode);
        return runner;
    }

    private static BuilderService CreateService(Mock<IProcessRunner> runner) =>
        new(runner.Object, NullLogger<BuilderService>.Instance);

    [Fact]
    public async Task InstallDependenciesAsync_WhenBuildModeIsSource_UsesCargoBuild()
    {
        var runner = CreateRunnerMock();
        var service = CreateService(runner);

        await service.InstallDependenciesAsync("C:/repo", "Source", _ => { });

        runner.Verify(r => r.RunWithStreamingAsync(
            "cargo",
            "build --release",
            It.IsAny<Action<string>>(),
            It.IsAny<Action<string>?>(),
            "C:/repo",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InstallDependenciesAsync_WhenBuildModeIsDocker_UsesDockerBuild()
    {
        var runner = CreateRunnerMock();
        var service = CreateService(runner);

        await service.InstallDependenciesAsync("C:/repo", "Docker", _ => { });

        runner.Verify(r => r.RunWithStreamingAsync(
            "docker",
            "build -t deepclaude .",
            It.IsAny<Action<string>>(),
            It.IsAny<Action<string>?>(),
            "C:/repo",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InstallDependenciesAsync_WhenCargoFails_Throws()
    {
        var runner = CreateRunnerMock(exitCode: 1);
        var service = CreateService(runner);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.InstallDependenciesAsync("C:/repo", "Source", _ => { }));

        Assert.Contains("exit code 1", ex.Message);
    }

    [Fact]
    public async Task CloneRepositoryAsync_WhenGitPullFails_Throws()
    {
        var runner = CreateRunnerMock(exitCode: 128);
        var service = CreateService(runner);

        var dir = Path.Combine(Path.GetTempPath(), "dcauto-clone-" + Guid.NewGuid());
        try
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "existing.txt"), "x");

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.CloneRepositoryAsync("https://example.com/repo.git", dir, _ => { }));

            Assert.Contains("git pull", ex.Message);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task SyncConfigTomlAsync_WhenFileMissing_CreatesWithPort()
    {
        var service = CreateService(CreateRunnerMock());
        var dir = Path.Combine(Path.GetTempPath(), "dcauto-config-" + Guid.NewGuid());
        try
        {
            Directory.CreateDirectory(dir);
            await service.SyncConfigTomlAsync(dir, 4242);

            var content = await File.ReadAllTextAsync(Path.Combine(dir, "config.toml"));
            Assert.Contains("host = \"127.0.0.1\"", content);
            Assert.Contains("port = 4242", content);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task SyncConfigTomlAsync_WhenFileExists_ReplacesServerPort()
    {
        var service = CreateService(CreateRunnerMock());
        var dir = Path.Combine(Path.GetTempPath(), "dcauto-config-" + Guid.NewGuid());
        try
        {
            Directory.CreateDirectory(dir);
            await File.WriteAllTextAsync(Path.Combine(dir, "config.toml"), """
[server]
host = "127.0.0.1"
port = 1337

[pricing]
output_price = 2.19
""");

            await service.SyncConfigTomlAsync(dir, 4242);

            var content = await File.ReadAllTextAsync(Path.Combine(dir, "config.toml"));
            Assert.Contains("port = 4242", content);
            Assert.DoesNotContain("port = 1337", content);
            // pricing 키는 건드리지 않아야 함
            Assert.Contains("output_price = 2.19", content);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task PatchCargoTomlAsync_WhenVendoredOpensslExists_RemovesOnlyThatLine()
    {
        var service = CreateService(CreateRunnerMock());
        var dir = Path.Combine(Path.GetTempPath(), "dcauto-cargo-" + Guid.NewGuid());
        try
        {
            Directory.CreateDirectory(dir);
            await File.WriteAllTextAsync(Path.Combine(dir, "Cargo.toml"), """
[package]
name = "deepreasoning"

[dependencies]
reqwest = { version = "0.12", features = ["json", "stream"] }
openssl = { version = "0.10", features = ["vendored"] }
""");

            await service.PatchCargoTomlAsync(dir);

            var content = await File.ReadAllTextAsync(Path.Combine(dir, "Cargo.toml"));
            Assert.DoesNotContain("openssl", content);
            Assert.Contains("reqwest", content);
            Assert.Contains("[package]", content);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task PatchCargoTomlAsync_WhenNoOpensslLine_LeavesFileUnchanged()
    {
        var service = CreateService(CreateRunnerMock());
        var dir = Path.Combine(Path.GetTempPath(), "dcauto-cargo-" + Guid.NewGuid());
        try
        {
            Directory.CreateDirectory(dir);
            const string original = """
[dependencies]
reqwest = "0.12"
""";
            await File.WriteAllTextAsync(Path.Combine(dir, "Cargo.toml"), original);

            await service.PatchCargoTomlAsync(dir);

            var content = await File.ReadAllTextAsync(Path.Combine(dir, "Cargo.toml"));
            Assert.Equal(original, content);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
