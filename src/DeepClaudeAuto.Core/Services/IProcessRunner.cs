namespace DeepClaudeAuto.Core.Services;

public interface IProcessRunner
{
    /// <summary>커맨드를 실행하고 stdout을 반환합니다.</summary>
    Task<(int ExitCode, string Output, string Error)> RunAsync(
        string fileName,
        string arguments,
        CancellationToken cancellationToken = default);

    /// <summary>실시간 출력 스트리밍이 필요한 장기 실행 프로세스용.</summary>
    Task<int> RunWithStreamingAsync(
        string fileName,
        string arguments,
        Action<string> onOutputLine,
        Action<string>? onErrorLine = null,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default);
}
