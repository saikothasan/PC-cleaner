using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using WindowsPCCleaner.Commands;
using WindowsPCCleaner.Models;
using WindowsPCCleaner.Services;

namespace WindowsPCCleaner.ViewModels
{
    public class DashboardViewModel : INotifyPropertyChanged
    {
        private readonly ICleaningService _cleaningService;
        private readonly IDiskAnalysisService _diskAnalysisService;
        private readonly ILoggingService _loggingService;

        private SystemHealthScore _healthScore;
        private ObservableCollection<QuickStat> _quickStats;
        private ObservableCollection<RecentActivity> _recentActivities;
        private bool _isLoading;

        public DashboardViewModel(
            ICleaningService cleaningService,
            IDiskAnalysisService diskAnalysisService,
            ILoggingService loggingService)
        {
            _cleaningService = cleaningService;
            _diskAnalysisService = diskAnalysisService;
            _loggingService = loggingService;

            QuickStats = new ObservableCollection<QuickStat>();
            RecentActivities = new ObservableCollection<RecentActivity>();

            // Commands
            QuickScanCommand = new AsyncRelayCommand(ExecuteQuickScanAsync);
            RefreshCommand = new AsyncRelayCommand(RefreshDashboardAsync);

            // Initialize dashboard
            _ = InitializeDashboardAsync();
        }

        public SystemHealthScore HealthScore
        {
            get => _healthScore;
            set => SetProperty(ref _healthScore, value);
        }

        public ObservableCollection<QuickStat> QuickStats
        {
            get => _quickStats;
            set => SetProperty(ref _quickStats, value);
        }

        public ObservableCollection<RecentActivity> RecentActivities
        {
            get => _recentActivities;
            set => SetProperty(ref _recentActivities, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public ICommand QuickScanCommand { get; }
        public ICommand RefreshCommand { get; }

        private async Task InitializeDashboardAsync()
        {
            IsLoading = true;
            try
            {
                await LoadSystemHealthAsync();
                await LoadQuickStatsAsync();
                await LoadRecentActivitiesAsync();
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Error initializing dashboard: {ex.Message}", ex);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadSystemHealthAsync()
        {
            try
            {
                var diskInfo = await _diskAnalysisService.GetDiskInfoAsync();
                var tempFileCount = await GetTempFileCountAsync();
                var registryIssues = await GetRegistryIssueCountAsync();

                var score = CalculateHealthScore(diskInfo, tempFileCount, registryIssues);
                
                HealthScore = new SystemHealthScore
                {
                    Score = score,
                    Status = GetHealthStatus(score),
                    LastUpdated = DateTime.Now,
                    Recommendations = GetRecommendations(score, tempFileCount, registryIssues)
                };
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Error loading system health: {ex.Message}", ex);
            }
        }

        private async Task LoadQuickStatsAsync()
        {
            try
            {
                var diskInfo = await _diskAnalysisService.GetDiskInfoAsync();
                var tempSize = await GetTempFileSizeAsync();
                var cacheSize = await GetCacheSizeAsync();

                QuickStats.Clear();
                QuickStats.Add(new QuickStat
                {
                    Title = "Disk Space Used",
                    Value = $"{FormatBytes(diskInfo.UsedSpace)} / {FormatBytes(diskInfo.TotalSpace)}",
                    Percentage = (double)diskInfo.UsedSpace / diskInfo.TotalSpace * 100,
                    Icon = "💾",
                    Color = GetDiskSpaceColor((double)diskInfo.UsedSpace / diskInfo.TotalSpace)
                });

                QuickStats.Add(new QuickStat
                {
                    Title = "Temporary Files",
                    Value = FormatBytes(tempSize),
                    Icon = "🗂️",
                    Color = "#FF6B6B"
                });

                QuickStats.Add(new QuickStat
                {
                    Title = "Cache Files",
                    Value = FormatBytes(cacheSize),
                    Icon = "📦",
                    Color = "#4ECDC4"
                });

                QuickStats.Add(new QuickStat
                {
                    Title = "System Health",
                    Value = $"{HealthScore?.Score ?? 0}%",
                    Icon = "❤️",
                    Color = GetHealthColor(HealthScore?.Score ?? 0)
                });
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Error loading quick stats: {ex.Message}", ex);
            }
        }

        private async Task LoadRecentActivitiesAsync()
        {
            try
            {
                // Load recent activities from logging service
                var activities = await _loggingService.GetRecentActivitiesAsync(10);
                
                RecentActivities.Clear();
                foreach (var activity in activities)
                {
                    RecentActivities.Add(activity);
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Error loading recent activities: {ex.Message}", ex);
            }
        }

        private async Task ExecuteQuickScanAsync()
        {
            try
            {
                IsLoading = true;
                
                var scanOptions = new ScanOptions
                {
                    ScanTemporaryFiles = true,
                    ScanSystemCache = true,
                    ScanLogFiles = false,
                    ScanRecycleBin = true,
                    ScanRegistry = false
                };

                var progress = new Progress<ScanProgress>(p => 
                {
                    // Update progress in UI
                });

                var result = await _cleaningService.ScanSystemAsync(scanOptions, progress, default);
                
                // Navigate to cleaning view with results
                // This would be handled by the main view model
                
                _loggingService.LogInfo($"Quick scan completed. Found {result.ItemCount} items.");
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Error during quick scan: {ex.Message}", ex);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task RefreshDashboardAsync()
        {
            await InitializeDashboardAsync();
        }

        private async Task<int> GetTempFileCountAsync()
        {
            // Implementation to count temp files
            return await Task.FromResult(0);
        }

        private async Task<int> GetRegistryIssueCountAsync()
        {
            // Implementation to count registry issues
            return await Task.FromResult(0);
        }

        private async Task<long> GetTempFileSizeAsync()
        {
            // Implementation to calculate temp file size
            return await Task.FromResult(0L);
        }

        private async Task<long> GetCacheSizeAsync()
        {
            // Implementation to calculate cache size
            return await Task.FromResult(0L);
        }

        private int CalculateHealthScore(DiskInfo diskInfo, int tempFileCount, int registryIssues)
        {
            var score = 100;
            
            // Reduce score based on disk usage
            var diskUsagePercent = (double)diskInfo.UsedSpace / diskInfo.TotalSpace;
            if (diskUsagePercent > 0.9) score -= 30;
            else if (diskUsagePercent > 0.8) score -= 20;
            else if (diskUsagePercent > 0.7) score -= 10;

            // Reduce score based on temp files
            if (tempFileCount > 1000) score -= 20;
            else if (tempFileCount > 500) score -= 10;

            // Reduce score based on registry issues
            if (registryIssues > 100) score -= 15;
            else if (registryIssues > 50) score -= 10;

            return Math.Max(0, score);
        }

        private string GetHealthStatus(int score)
        {
            return score switch
            {
                >= 90 => "Excellent",
                >= 70 => "Good",
                >= 50 => "Fair",
                >= 30 => "Poor",
                _ => "Critical"
            };
        }

        private string[] GetRecommendations(int score, int tempFileCount, int registryIssues)
        {
            var recommendations = new List<string>();

            if (score < 70)
                recommendations.Add("Run a full system scan to improve performance");
            
            if (tempFileCount > 500)
                recommendations.Add("Clean temporary files to free up disk space");
            
            if (registryIssues > 50)
                recommendations.Add("Clean registry entries to optimize system");

            return recommendations.ToArray();
        }

        private string GetDiskSpaceColor(double percentage)
        {
            return percentage switch
            {
                > 0.9 => "#FF4757",
                > 0.8 => "#FFA502",
                > 0.7 => "#FFD700",
                _ => "#2ED573"
            };
        }

        private string GetHealthColor(int score)
        {
            return score switch
            {
                >= 90 => "#2ED573",
                >= 70 => "#FFD700",
                >= 50 => "#FFA502",
                _ => "#FF4757"
            };
        }

        private string FormatBytes(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int counter = 0;
            decimal number = bytes;
            while (Math.Round(number / 1024) >= 1)
            {
                number /= 1024;
                counter++;
            }
            return $"{number:n1} {suffixes[counter]}";
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
}
