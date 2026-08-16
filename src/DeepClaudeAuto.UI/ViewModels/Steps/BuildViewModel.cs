using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeepClaudeAuto.Core.Services;

namespace DeepClaudeAuto.UI.ViewModels.Steps;

public sealed partial class BuildViewModel : ObservableObject
{
    private readonly IBuilderService _builder;
    private readonly IConfigManager _configManager;
    private readonly BuildModeViewModel _buildModeVm;
    private readonly ConfigViewModel _configVm;

    public ObservableCollection<string> LogLines { get; } = [];

    [ObservableProperty]
    private bool _isBuilding;

    [ObservableProperty]
    private bool _buildCompleted;

    [ObservableProperty]
    private string _statusMessage = "빌드를 시작하려면 '빌드 시작' 버튼을 누르세요.";

    public BuildViewModel(
        IBuilderService builder,
        IConfigManager configManager,
        BuildModeViewModel buildModeVm,
        ConfigViewModel configVm)
    {
        _builder = builder;
        _configManager = configManager;
        _buildModeVm = buildModeVm;
        _configVm = configVm;
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task StartBuildAsync(CancellationToken ct)
    {
        IsBuilding = true;
        BuildCompleted = false;
        LogLines.Clear();
        StatusMessage = "빌드 진행 중...";

        var config = new Core.Models.AppConfig
        {
            AnthropicApiKey = _configVm.AnthropicApiKey,
            DeepSeekApiKey = _configVm.DeepSeekApiKey,
            ServerPort = _configVm.ServerPort,
            BuildMode = _buildModeVm.SelectedMode,
            InstallPath = _buildModeVm.InstallPath,
            DeepClaudeRepoUrl = _buildModeVm.RepoUrl
        };

        try
        {
            await _builder.CloneRepositoryAsync(
                config.DeepClaudeRepoUrl, config.InstallPath, Log, ct);

            await _builder.WriteEnvFileAsync(config, config.InstallPath);
            Log("[INFO] .env 파일 작성 완료");

            await _builder.InstallDependenciesAsync(
                config.InstallPath, config.BuildMode, Log, ct);

            _configManager.Save(config);
            BuildCompleted = true;
            StatusMessage = "✅ 빌드 완료!";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "빌드가 취소되었습니다.";
            Log("[WARN] 빌드 취소됨");
        }
        catch (Exception ex)
        {
            StatusMessage = "❌ 빌드 실패: " + ex.Message;
            Log("[ERROR] " + ex.Message);
        }

        IsBuilding = false;
    }

    private void Log(string line) =>
        System.Windows.Application.Current?.Dispatcher.Invoke(() => LogLines.Add(line));
}
