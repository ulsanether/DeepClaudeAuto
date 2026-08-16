using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeepClaudeAuto.Core.Models;
using DeepClaudeAuto.Core.Services;

namespace DeepClaudeAuto.UI.ViewModels.Steps;

public sealed partial class ValidationViewModel : ObservableObject
{
    private readonly IDependencyChecker _checker;

    public ObservableCollection<DependencyCheckResult> Results { get; } = [];

    [ObservableProperty]
    private bool _isChecking;

    [ObservableProperty]
    private bool _allPassed;

    [ObservableProperty]
    private string _statusMessage = "검증을 시작하려면 '검사 시작' 버튼을 누르세요.";

    public ValidationViewModel(IDependencyChecker checker)
    {
        _checker = checker;

        foreach (var item in checker.Items)
            Results.Add(item);
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task RunCheckAsync(CancellationToken ct)
    {
        IsChecking = true;
        AllPassed = false;
        StatusMessage = "의존성 검사 중...";

        // 상태 초기화
        foreach (var r in Results)
            r.Status = CheckStatus.Pending;

        var progress = new Progress<DependencyCheckResult>(updated =>
        {
            var idx = Results.IndexOf(updated);
            if (idx >= 0)
            {
                // ObservableCollection에 변경 알림 트리거
                Results[idx] = updated;
            }
        });

        await _checker.CheckAllAsync(progress, ct);

        var failed = Results.Count(r => r.IsRequired && r.Status == CheckStatus.Failed);
        AllPassed = failed == 0;
        StatusMessage = AllPassed
            ? "✅ 모든 필수 항목이 통과되었습니다."
            : $"❌ {failed}개의 필수 항목이 누락되었습니다. 다음 단계에서 설치하세요.";

        IsChecking = false;
    }
}
