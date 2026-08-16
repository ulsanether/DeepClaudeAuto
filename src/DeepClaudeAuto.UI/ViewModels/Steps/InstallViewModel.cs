using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeepClaudeAuto.Core.Models;
using DeepClaudeAuto.Core.Services;

namespace DeepClaudeAuto.UI.ViewModels.Steps;

public sealed partial class InstallViewModel : ObservableObject
{
    private readonly IDependencyChecker _checker;
    private readonly IProcessRunner _runner;

    public ObservableCollection<DependencyCheckResult> FailedItems { get; } = [];
    public ObservableCollection<string> LogLines { get; } = [];

    [ObservableProperty]
    private bool _isInstalling;

    [ObservableProperty]
    private string _currentTask = string.Empty;

    public InstallViewModel(IDependencyChecker checker, IProcessRunner runner)
    {
        _checker = checker;
        _runner = runner;
        RefreshFailedItems();
    }

    public void RefreshFailedItems()
    {
        FailedItems.Clear();
        foreach (var item in _checker.Items.Where(i => i.Status is CheckStatus.Failed or CheckStatus.Warning))
            FailedItems.Add(item);
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task InstallAllAsync(CancellationToken ct)
    {
        IsInstalling = true;
        LogLines.Clear();

        foreach (var item in FailedItems.ToList())
        {
            if (string.IsNullOrWhiteSpace(item.InstallCommand)) continue;

            CurrentTask = $"{item.Name} 설치 중...";
            Log($"▶ {item.Name} 설치: {item.InstallCommand}");

            var parts = item.InstallCommand.Split(' ', 2);
            var (exit, _, err) = await _runner.RunAsync(parts[0], parts.Length > 1 ? parts[1] : "", ct);

            if (exit == 0)
                Log($"✅ {item.Name} 설치 완료");
            else
                Log($"❌ {item.Name} 설치 실패: {err}");
        }

        CurrentTask = "설치 완료";
        IsInstalling = false;
    }

    private void Log(string line) =>
        System.Windows.Application.Current?.Dispatcher.Invoke(() => LogLines.Add(line));
}
