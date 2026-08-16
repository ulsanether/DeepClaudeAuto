using CommunityToolkit.Mvvm.ComponentModel;

namespace DeepClaudeAuto.UI.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    public WizardViewModel Wizard { get; }

    public MainWindowViewModel(WizardViewModel wizard)
    {
        Wizard = wizard;
    }
}
