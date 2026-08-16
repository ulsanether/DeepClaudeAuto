using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeepClaudeAuto.Core.Models;
using DeepClaudeAuto.Core.Services;

namespace DeepClaudeAuto.UI.ViewModels.Steps;

public sealed partial class ServerViewModel : ObservableObject
{
    private readonly IServerManager _server;
    private readonly IConfigManager _configManager;

    public ObservableCollection<string> LogLines { get; } = [];

    [ObservableProperty]
    private ServerStatus _serverStatus = ServerStatus.Stopped;

    [ObservableProperty]
    private string _serverAddress = string.Empty;

    public bool IsRunning => ServerStatus == ServerStatus.Running;
    public bool IsStopped => ServerStatus == ServerStatus.Stopped;

    public ServerViewModel(IServerManager server, IConfigManager configManager)
    {
        _server = server;
        _configManager = configManager;

        _server.LogReceived += (_, line) =>
            System.Windows.Application.Current?.Dispatcher.Invoke(() => LogLines.Add(line));

        _server.StatusChanged += (_, info) =>
        {
            ServerStatus = info.Status;
            ServerAddress = info.Address;
            OnPropertyChanged(nameof(IsRunning));
            OnPropertyChanged(nameof(IsStopped));
        };
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task StartServerAsync(CancellationToken ct)
    {
        LogLines.Clear();
        await _server.StartAsync(_configManager.Current, ct);
    }

    [RelayCommand]
    private async Task StopServerAsync()
    {
        await _server.StopAsync();
    }

    [RelayCommand]
    private void OpenBrowser()
    {
        if (string.IsNullOrEmpty(ServerAddress)) return;
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = ServerAddress,
            UseShellExecute = true
        });
    }
}
