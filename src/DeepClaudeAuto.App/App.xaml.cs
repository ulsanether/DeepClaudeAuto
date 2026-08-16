using System.IO;
using System.Windows;
using DeepClaudeAuto.Core.Services;
using DeepClaudeAuto.Core.Services.Impl;
using DeepClaudeAuto.Services;
using DeepClaudeAuto.UI.ViewModels;
using DeepClaudeAuto.UI.ViewModels.Steps;
using DeepClaudeAuto.UI.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Application = System.Windows.Application;

namespace DeepClaudeAuto;

public partial class App : Application
{
    private IHost? _host;

    protected override async void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);

        Log.Logger = new LoggerConfiguration()
            .WriteTo.File(
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "DeepClaudeAuto", "logs", "app-.log"),
                rollingInterval: RollingInterval.Day)
            .CreateLogger();

        _host = Host.CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureServices(services =>
            {
                // Core Services
                services.AddSingleton<IProcessRunner, ProcessRunner>();
                services.AddSingleton<IConfigManager, ConfigManager>();
                services.AddSingleton<IDependencyChecker, DependencyChecker>();
                services.AddSingleton<IBuilderService, BuilderService>();
                services.AddSingleton<IServerManager, ServerManager>();
                services.AddSingleton<IFolderBrowserService, WinFormsFolderBrowserService>();

                // ViewModels
                services.AddSingleton<ValidationViewModel>();
                services.AddSingleton<InstallViewModel>();
                services.AddSingleton<ConfigViewModel>();
                services.AddSingleton<BuildModeViewModel>();
                services.AddSingleton<BuildViewModel>();
                services.AddSingleton<ServerViewModel>();
                services.AddSingleton<WizardViewModel>();
                services.AddSingleton<MainWindowViewModel>();

                // Main Window
                services.AddTransient<MainWindow>();
            })
            .Build();

        await _host.StartAsync();

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.DataContext = _host.Services.GetRequiredService<MainWindowViewModel>();
        mainWindow.Show();
    }

    protected override async void OnExit(System.Windows.ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
