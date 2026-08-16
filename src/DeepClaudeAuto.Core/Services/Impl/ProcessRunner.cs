using System.Diagnostics;
using DeepClaudeAuto.Core.Services;

namespace DeepClaudeAuto.Core.Services.Impl;

public sealed class ProcessRunner : IProcessRunner
{
    public async Task<(int ExitCode, string Output, string Error)> RunAsync(
        string fileName,
        string arguments,
        CancellationToken cancellationToken = default)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        process.Start();
        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);
        var output = await outputTask;
        var error = await errorTask;

        return (process.ExitCode, output.Trim(), error.Trim());
    }

    public async Task<int> RunWithStreamingAsync(
        string fileName,
        string arguments,
        Action<string> onOutputLine,
        Action<string>? onErrorLine = null,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory ?? string.Empty
        };

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null) onOutputLine(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null) (onErrorLine ?? onOutputLine)(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken);
        return process.ExitCode;
    }
}
