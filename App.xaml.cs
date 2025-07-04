using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Windows;
using WindowsPCCleaner.Services;
using WindowsPCCleaner.ViewModels;
using WindowsPCCleaner.Views;

namespace WindowsPCCleaner
{
    public partial class App : Application
    {
        private IHost _host;

        protected override void OnStartup(StartupEventArgs e)
        {
            _host = Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    // Register Services
                    services.AddSingleton<ICleaningService, CleaningService>();
                    services.AddSingleton<IRegistryService, RegistryService>();
                    services.AddSingleton<IBrowserService, BrowserService>();
                    services.AddSingleton<IDiskAnalysisService, DiskAnalysisService>();
                    services.AddSingleton<IStartupManagerService, StartupManagerService>();
                    services.AddSingleton<IThemeService, ThemeService>();
                    services.AddSingleton<ILoggingService, LoggingService>();
                    services.AddSingleton<ISecurityService, SecurityService>();

                    // Register ViewModels
                    services.AddTransient<MainViewModel>();
                    services.AddTransient<DashboardViewModel>();
                    services.AddTransient<CleaningViewModel>();
                    services.AddTransient<PrivacyViewModel>();
                    services.AddTransient<ToolsViewModel>();
                    services.AddTransient<SettingsViewModel>();

                    // Register Views
                    services.AddTransient<MainWindow>();
                })
                .Build();

            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            mainWindow.Show();

            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _host?.Dispose();
            base.OnExit(e);
        }
    }
}
