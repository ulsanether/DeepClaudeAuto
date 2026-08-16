using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeepClaudeAuto.Core.Models;
using DeepClaudeAuto.Core.Services;
using DeepClaudeAuto.UI.ViewModels.Steps;

namespace DeepClaudeAuto.UI.ViewModels;

public sealed partial class WizardViewModel : ObservableObject
{
    private readonly IConfigManager _configManager;

    public ValidationViewModel ValidationVm { get; }
    public InstallViewModel InstallVm { get; }
    public ConfigViewModel ConfigVm { get; }
    public BuildModeViewModel BuildModeVm { get; }
    public BuildViewModel BuildVm { get; }
    public ServerViewModel ServerVm { get; }

    [ObservableProperty]
    private ObservableObject _currentStep = null!;

    [ObservableProperty]
    private int _currentStepIndex;

    [ObservableProperty]
    private bool _canGoNext;

    [ObservableProperty]
    private bool _canGoPrev;

    private readonly List<ObservableObject> _steps;

    public WizardViewModel(
        IConfigManager configManager,
        ValidationViewModel validationVm,
        InstallViewModel installVm,
        ConfigViewModel configVm,
        BuildModeViewModel buildModeVm,
        BuildViewModel buildVm,
        ServerViewModel serverVm)
    {
        _configManager = configManager;
        ValidationVm = validationVm;
        InstallVm = installVm;
        ConfigVm = configVm;
        BuildModeVm = buildModeVm;
        BuildVm = buildVm;
        ServerVm = serverVm;

        _steps = [validationVm, installVm, configVm, buildModeVm, buildVm, serverVm];
        CurrentStep = _steps[0];
        CurrentStepIndex = 0;
        UpdateNavigation();
    }

    [RelayCommand]
    private void Next()
    {
        if (CurrentStepIndex < _steps.Count - 1)
        {
            CurrentStepIndex++;
            CurrentStep = _steps[CurrentStepIndex];
            UpdateNavigation();
        }
    }

    [RelayCommand]
    private void Prev()
    {
        if (CurrentStepIndex > 0)
        {
            CurrentStepIndex--;
            CurrentStep = _steps[CurrentStepIndex];
            UpdateNavigation();
        }
    }

    private void UpdateNavigation()
    {
        CanGoPrev = CurrentStepIndex > 0;
        CanGoNext = CurrentStepIndex < _steps.Count - 1;
    }

    public AppConfig BuildConfig() => new()
    {
        AnthropicApiKey = ConfigVm.AnthropicApiKey,
        DeepSeekApiKey = ConfigVm.DeepSeekApiKey,
        ServerPort = ConfigVm.ServerPort,
        BuildMode = BuildModeVm.SelectedMode,
        InstallPath = BuildModeVm.InstallPath,
        AutoStartServer = ConfigVm.AutoStartServer
    };
}
