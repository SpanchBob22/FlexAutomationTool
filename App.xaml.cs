using System;
using System.Diagnostics;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using FlexAutomator.Services;
using FlexAutomator.Services.API;
using FlexAutomator.ViewModels;
using FlexAutomator.Views;

using Application = System.Windows.Application;

namespace FlexAutomator;

public partial class App : Application
{
    public IHost? Host { get; private set; }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
            .ConfigureServices(ConfigureServices)
            .Build();

        var bootService = Host.Services.GetRequiredService<BootConfigService>();
        bool needsSetup = false;
        bool restartRequired = false;

        if (bootService.Load() == null)
        {
            needsSetup = true;
        }
        else
        {

            try
            {
                Host.Services.GetRequiredService<DatabaseService>();
            }
            catch
            {
                needsSetup = true;
                restartRequired = true;
            }
        }

        if (needsSetup)
        {

            var setupWindow = Host.Services.GetRequiredService<SetupWindow>();
            var result = setupWindow.ShowDialog();

            if (result != true)
            {
                Shutdown();
                return;
            }


            if (restartRequired)
            {
                var currentProcess = Environment.ProcessPath;
                if (currentProcess != null)
                {
                    Process.Start(currentProcess);
                }
                Shutdown();
                return;
            }
        }

        await Host.StartAsync();

        var mainWindow = Host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<BootConfigService>();

        services.AddSingleton<DatabaseService>();

        services.AddSingleton<LogService>();
        services.AddSingleton<ScenarioExecutor>();
        services.AddSingleton<YouTubeService>();
        services.AddSingleton<TMDbService>();
        services.AddSingleton<TelegramBotService>();
        services.AddSingleton<GlobalHotkeyService>();

        services.AddSingleton<SchedulerService>();
        services.AddHostedService(sp => sp.GetRequiredService<SchedulerService>());

        services.AddTransient<SetupViewModel>();
        services.AddTransient<MainViewModel>();
        services.AddTransient<ScenarioEditorViewModel>();
        services.AddTransient<SettingsViewModel>();

        services.AddTransient<SetupWindow>();
        services.AddTransient<MainWindow>();
        services.AddTransient<ScenarioEditorWindow>();
        services.AddTransient<SettingsWindow>();
        services.AddTransient<LogWindow>();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (Host != null)
        {
            await Host.StopAsync(TimeSpan.FromSeconds(5));
            Host.Dispose();
        }
        base.OnExit(e);
    }
}
