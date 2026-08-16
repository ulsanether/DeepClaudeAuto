using CommunityToolkit.Mvvm.ComponentModel;
using DeepClaudeAuto.Core.Models;
using DeepClaudeAuto.Core.Services;

namespace DeepClaudeAuto.UI.ViewModels.Steps;

public sealed partial class ConfigViewModel : ObservableObject
{
    [ObservableProperty]
    private string _anthropicApiKey = string.Empty;

    [ObservableProperty]
    private string _deepSeekApiKey = string.Empty;

    [ObservableProperty]
    private int _serverPort = 3000;

    [ObservableProperty]
    private bool _autoStartServer;

    public ConfigViewModel(IConfigManager configManager)
    {
        var cfg = configManager.Current;
        AnthropicApiKey = cfg.AnthropicApiKey;
        DeepSeekApiKey  = cfg.DeepSeekApiKey;
        ServerPort      = cfg.ServerPort;
        AutoStartServer = cfg.AutoStartServer;
    }
}
