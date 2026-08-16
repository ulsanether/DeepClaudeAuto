using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeepClaudeAuto.Core.Services;

namespace DeepClaudeAuto.UI.ViewModels.Steps;

public sealed partial class BuildModeViewModel : ObservableObject
{
    private readonly IFolderBrowserService _folderBrowser;

    [ObservableProperty]
    private string _selectedMode = "Source"; // Source | Docker

    [ObservableProperty]
    private string _installPath = string.Empty;

    [ObservableProperty]
    private string _repoUrl = "https://github.com/getasterisk/deepclaude";

    public BuildModeViewModel(IFolderBrowserService folderBrowser)
    {
        _folderBrowser = folderBrowser;
        InstallPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "deepclaude");
    }

    [RelayCommand]
    private void BrowseInstallPath()
    {
        var selected = _folderBrowser.BrowseForFolder("설치 경로를 선택하세요", InstallPath);
        if (selected is not null)
            InstallPath = selected;
    }
}
