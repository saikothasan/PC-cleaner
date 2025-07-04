using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using WindowsPCCleaner.Models;
using WindowsPCCleaner.Services;

namespace WindowsPCCleaner.ViewModels
{
    public class DashboardViewModel : INotifyPropertyChanged
    {
        private readonly ISystemCleaningService _systemCleaningService;
        private readonly ILoggingService _loggingService;

        private int _systemHealthScore = 85;
        private long _totalJunkSize;
        private int _totalJunkFiles;
        private string _lastScanTime = "Never";
        private ObservableCollection<QuickActionItem> _quickActions;
        private ObservableCollection<SystemAlert> _systemAlerts;

        public DashboardViewModel(ISystemCleaningService systemCleaningService, ILoggingService loggingService)
        {
            _systemCleaningService = systemCleaningService;
            _loggingService = loggingService;

            InitializeQuickActions();
            InitializeSystemAlerts();
            LoadDashboardDataAsync();
        }

        public int SystemHealthScore
        {
            get => _systemHealthScore;
            set => SetProperty(ref _systemHealthScore, value);
        }

        public long TotalJunkSize
        {
            get => _totalJunkSize;
            set => SetProperty(ref _totalJunkSize, value);
        }

        public int TotalJunkFiles
        {
            get => _totalJunkFiles;
            set => SetProperty(ref _totalJunkFiles, value);
        }

        public string LastScanTime
        {
            get => _lastScanTime;
            set => SetProperty(ref _lastScanTime, value);
        }

        public ObservableCollection<QuickActionItem> QuickActions
        {
            get => _quickActions;
            set => SetProperty(ref _quickActions, value);
        }

        public ObservableCollection<SystemAlert> SystemAlerts
        {
            get => _systemAlerts;
            set => SetProperty(ref _systemAlerts, value);
        }

        private void InitializeQuickActions()
        {
            QuickActions = new ObservableCollection<QuickActionItem>
            {
                new QuickActionItem
                {
                    Title = "Quick Clean",
                    Description = "Clean temporary files and cache",
                    Icon = "🧹",
                    Command = new AsyncRelayCommand(PerformQuickCleanAsync)
                },
                new QuickActionItem
                {
                    Title = "Registry Scan",
                    Description = "Scan for registry issues",
                    Icon = "🔧",
                    Command = new AsyncRelayCommand(PerformRegistryScanAsync)
                },
                new QuickActionItem
                {
                    Title = "Disk Analysis",
                    Description = "Analyze disk usage",
                    Icon = "💾",
                    Command = new AsyncRelayCommand(PerformDiskAnalysisAsync)
                },
                new QuickActionItem
                {
                    Title = "Privacy Scan",
                    Description = "Scan for privacy risks",
                    Icon = "🔒",
                    Command = new AsyncRelayCommand(PerformPrivacyScanAsync)
                }
            };
        }

        private void InitializeSystemAlerts()
        {
            SystemAlerts = new ObservableCollection<SystemAlert>();
        }

        private async void LoadDashboardDataAsync()
        {
            try
            {
                await Task.Run(() =>
                {
                    // Simulate loading dashboard data
                    TotalJunkSize = 1024 * 1024 * 150; // 150 MB
                    TotalJunkFiles = 1250;
                    LastScanTime = "2 hours ago";

                    // Add some sample alerts
                    SystemAlerts.Add(new SystemAlert
                    {
                        Type = AlertType.Warning,
                        Title = "Low Disk Space",
                        Message = "Drive C: is running low on space (15% remaining)",
                        Timestamp = DateTime.Now.AddHours(-1)
                    });

                    SystemAlerts.Add(new SystemAlert
                    {
                        Type = AlertType.Info,
                        Title = "Startup Programs",
                        Message = "12 programs are set to start with Windows",
                        Timestamp = DateTime.Now.AddHours(-2)
                    });
                });
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Error loading dashboard data: {ex.Message}", ex);
            }
        }

        private async Task PerformQuickCleanAsync()
        {
            try
            {
                _loggingService.LogInfo("Starting quick clean from dashboard");
                // This would trigger the main quick clean functionality
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Error during quick clean: {ex.Message}", ex);
            }
        }

        private async Task PerformRegistryScanAsync()
        {
            try
            {
                _loggingService.LogInfo("Starting registry scan from dashboard");
                // This would trigger the registry scan functionality
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Error during registry scan: {ex.Message}", ex);
            }
        }

        private async Task PerformDiskAnalysisAsync()
        {
            try
            {
                _loggingService.LogInfo("Starting disk analysis from dashboard");
                // This would trigger the disk analysis functionality
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Error during disk analysis: {ex.Message}", ex);
            }
        }

        private async Task PerformPrivacyScanAsync()
        {
            try
            {
                _loggingService.LogInfo("Starting privacy scan from dashboard");
                // This would trigger the privacy scan functionality
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Error during privacy scan: {ex.Message}", ex);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }

    public class QuickActionItem
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string Icon { get; set; }
        public ICommand Command { get; set; }
    }

    public class SystemAlert
    {
        public AlertType Type { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public enum AlertType
    {
        Info,
        Warning,
        Error
    }
}
