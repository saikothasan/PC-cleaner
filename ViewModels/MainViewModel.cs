using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using WindowsPCCleaner.Models;
using WindowsPCCleaner.Services;

namespace WindowsPCCleaner.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly ISystemCleaningService _systemCleaningService;
        private readonly IBrowserService _browserService;
        private readonly IRegistryService _registryService;
        private readonly IDiskAnalysisService _diskAnalysisService;
        private readonly IStartupManagerService _startupManagerService;
        private readonly ISecurityService _securityService;
        private readonly ILoggingService _loggingService;

        private string _currentView = "Dashboard";
        private bool _isScanning;
        private bool _isCleaning;
        private ScanProgress _scanProgress;
        private CleaningProgress _cleaningProgress;
        private SystemInfo _systemInfo;
        private CleaningSession _currentSession;

        public MainViewModel(
            ISystemCleaningService systemCleaningService,
            IBrowserService browserService,
            IRegistryService registryService,
            IDiskAnalysisService diskAnalysisService,
            IStartupManagerService startupManagerService,
            ISecurityService securityService,
            ILoggingService loggingService)
        {
            _systemCleaningService = systemCleaningService;
            _browserService = browserService;
            _registryService = registryService;
            _diskAnalysisService = diskAnalysisService;
            _startupManagerService = startupManagerService;
            _securityService = securityService;
            _loggingService = loggingService;

            InitializeCommands();
            InitializeViewModels();
            LoadSystemInfoAsync();
        }

        // Properties
        public string CurrentView
        {
            get => _currentView;
            set => SetProperty(ref _currentView, value);
        }

        public bool IsScanning
        {
            get => _isScanning;
            set => SetProperty(ref _isScanning, value);
        }

        public bool IsCleaning
        {
            get => _isCleaning;
            set => SetProperty(ref _isCleaning, value);
        }

        public ScanProgress ScanProgress
        {
            get => _scanProgress;
            set => SetProperty(ref _scanProgress, value);
        }

        public CleaningProgress CleaningProgress
        {
            get => _cleaningProgress;
            set => SetProperty(ref _cleaningProgress, value);
        }

        public SystemInfo SystemInfo
        {
            get => _systemInfo;
            set => SetProperty(ref _systemInfo, value);
        }

        public CleaningSession CurrentSession
        {
            get => _currentSession;
            set => SetProperty(ref _currentSession, value);
        }

        // Child ViewModels
        public DashboardViewModel DashboardViewModel { get; private set; }
        public SystemCleaningViewModel SystemCleaningViewModel { get; private set; }
        public BrowserCleaningViewModel BrowserCleaningViewModel { get; private set; }
        public RegistryCleaningViewModel RegistryCleaningViewModel { get; private set; }
        public DiskAnalysisViewModel DiskAnalysisViewModel { get; private set; }
        public StartupManagerViewModel StartupManagerViewModel { get; private set; }
        public SecurityViewModel SecurityViewModel { get; private set; }

        // Commands
        public ICommand NavigateCommand { get; private set; }
        public ICommand QuickScanCommand { get; private set; }
        public ICommand FullScanCommand { get; private set; }
        public ICommand CleanSelectedCommand { get; private set; }
        public ICommand CancelOperationCommand { get; private set; }

        private CancellationTokenSource _cancellationTokenSource;

        private void InitializeCommands()
        {
            NavigateCommand = new RelayCommand<string>(Navigate);
            QuickScanCommand = new AsyncRelayCommand(PerformQuickScanAsync, () => !IsScanning && !IsCleaning);
            FullScanCommand = new AsyncRelayCommand(PerformFullScanAsync, () => !IsScanning && !IsCleaning);
            CleanSelectedCommand = new AsyncRelayCommand(CleanSelectedItemsAsync, () => !IsScanning && !IsCleaning);
            CancelOperationCommand = new RelayCommand(CancelOperation, () => IsScanning || IsCleaning);
        }

        private void InitializeViewModels()
        {
            DashboardViewModel = new DashboardViewModel(_systemCleaningService, _loggingService);
            SystemCleaningViewModel = new SystemCleaningViewModel(_systemCleaningService, _loggingService);
            BrowserCleaningViewModel = new BrowserCleaningViewModel(_browserService, _loggingService);
            RegistryCleaningViewModel = new RegistryCleaningViewModel(_registryService, _loggingService);
            DiskAnalysisViewModel = new DiskAnalysisViewModel(_diskAnalysisService, _loggingService);
            StartupManagerViewModel = new StartupManagerViewModel(_startupManagerService, _loggingService);
            SecurityViewModel = new SecurityViewModel(_securityService, _loggingService);
        }

        private void Navigate(string viewName)
        {
            CurrentView = viewName;
            _loggingService.LogInfo($"Navigated to {viewName}");
        }

        private async Task PerformQuickScanAsync()
        {
            try
            {
                IsScanning = true;
                _cancellationTokenSource = new CancellationTokenSource();
                CurrentSession = new CleaningSession();

                var progress = new Progress<ScanProgress>(p => ScanProgress = p);

                // Quick scan - system cleaning only
                var items = await _systemCleaningService.ScanSystemAsync(
                    new SystemScanOptions { QuickScan = true },
                    progress,
                    _cancellationTokenSource.Token);

                CurrentSession.ItemsToClean.AddRange(items);
                SystemCleaningViewModel.CleanableItems.Clear();
                foreach (var item in items)
                {
                    SystemCleaningViewModel.CleanableItems.Add(item);
                }

                _loggingService.LogInfo($"Quick scan completed. Found {items.Count} items.");
            }
            catch (OperationCanceledException)
            {
                _loggingService.LogInfo("Quick scan was cancelled.");
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Error during quick scan: {ex.Message}", ex);
            }
            finally
            {
                IsScanning = false;
                _cancellationTokenSource?.Dispose();
            }
        }

        private async Task PerformFullScanAsync()
        {
            try
            {
                IsScanning = true;
                _cancellationTokenSource = new CancellationTokenSource();
                CurrentSession = new CleaningSession();

                var progress = new Progress<ScanProgress>(p => ScanProgress = p);

                // Full scan - all modules
                var allItems = new ObservableCollection<CleanableItem>();

                // System cleaning
                var systemItems = await _systemCleaningService.ScanSystemAsync(
                    new SystemScanOptions(),
                    progress,
                    _cancellationTokenSource.Token);
                foreach (var item in systemItems) allItems.Add(item);

                // Browser cleaning
                var browserItems = await _browserService.ScanBrowsersAsync(
                    new BrowserScanOptions(),
                    progress,
                    _cancellationTokenSource.Token);
                foreach (var item in browserItems) allItems.Add(item);

                // Registry cleaning
                var registryItems = await _registryService.ScanRegistryAsync(
                    new RegistryScanOptions(),
                    progress,
                    _cancellationTokenSource.Token);
                foreach (var item in registryItems) allItems.Add(item);

                CurrentSession.ItemsToClean.AddRange(allItems);

                // Update individual ViewModels
                SystemCleaningViewModel.CleanableItems.Clear();
                BrowserCleaningViewModel.CleanableItems.Clear();
                RegistryCleaningViewModel.CleanableItems.Clear();

                foreach (var item in allItems)
                {
                    switch (item.Category)
                    {
                        case var cat when cat.StartsWith("System"):
                            SystemCleaningViewModel.CleanableItems.Add(item);
                            break;
                        case var cat when cat.StartsWith("Browser"):
                            BrowserCleaningViewModel.CleanableItems.Add(item);
                            break;
                        case var cat when cat.StartsWith("Registry"):
                            RegistryCleaningViewModel.CleanableItems.Add(item);
                            break;
                    }
                }

                _loggingService.LogInfo($"Full scan completed. Found {allItems.Count} items.");
            }
            catch (OperationCanceledException)
            {
                _loggingService.LogInfo("Full scan was cancelled.");
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Error during full scan: {ex.Message}", ex);
            }
            finally
            {
                IsScanning = false;
                _cancellationTokenSource?.Dispose();
            }
        }

        private async Task CleanSelectedItemsAsync()
        {
            try
            {
                IsCleaning = true;
                _cancellationTokenSource = new CancellationTokenSource();

                var progress = new Progress<CleaningProgress>(p => CleaningProgress = p);
                var selectedItems = new ObservableCollection<CleanableItem>();

                // Collect selected items from all ViewModels
                foreach (var item in SystemCleaningViewModel.CleanableItems)
                    if (item.IsSelected) selectedItems.Add(item);

                foreach (var item in BrowserCleaningViewModel.CleanableItems)
                    if (item.IsSelected) selectedItems.Add(item);

                foreach (var item in RegistryCleaningViewModel.CleanableItems)
                    if (item.IsSelected) selectedItems.Add(item);

                if (selectedItems.Count == 0)
                {
                    _loggingService.LogWarning("No items selected for cleaning.");
                    return;
                }

                // Clean items by category
                await CleanSystemItemsAsync(selectedItems, progress);
                await CleanBrowserItemsAsync(selectedItems, progress);
                await CleanRegistryItemsAsync(selectedItems, progress);

                CurrentSession.EndTime = DateTime.Now;
                _loggingService.LogInfo($"Cleaning completed. Freed {FormatBytes(CurrentSession.TotalSizeFreed)}.");
            }
            catch (OperationCanceledException)
            {
                _loggingService.LogInfo("Cleaning was cancelled.");
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Error during cleaning: {ex.Message}", ex);
            }
            finally
            {
                IsCleaning = false;
                _cancellationTokenSource?.Dispose();
            }
        }

        private async Task CleanSystemItemsAsync(ObservableCollection<CleanableItem> selectedItems, IProgress<CleaningProgress> progress)
        {
            var systemItems = selectedItems.Where(i => i.Category.StartsWith("System")).ToList();
            if (systemItems.Any())
            {
                var result = await _systemCleaningService.CleanItemsAsync(systemItems, progress, _cancellationTokenSource.Token);
                CurrentSession.CleanedItems.AddRange(result.CleanedItems);
                CurrentSession.FailedItems.AddRange(result.FailedItems);
                CurrentSession.TotalSizeFreed += result.TotalSizeFreed;
            }
        }

        private async Task CleanBrowserItemsAsync(ObservableCollection<CleanableItem> selectedItems, IProgress<CleaningProgress> progress)
        {
            var browserItems = selectedItems.Where(i => i.Category.StartsWith("Browser")).ToList();
            if (browserItems.Any())
            {
                var result = await _browserService.CleanBrowserDataAsync(browserItems, progress, _cancellationTokenSource.Token);
                CurrentSession.CleanedItems.AddRange(result.CleanedItems);
                CurrentSession.FailedItems.AddRange(result.FailedItems);
                CurrentSession.TotalSizeFreed += result.TotalSizeFreed;
            }
        }

        private async Task CleanRegistryItemsAsync(ObservableCollection<CleanableItem> selectedItems, IProgress<CleaningProgress> progress)
        {
            var registryItems = selectedItems.Where(i => i.Category.StartsWith("Registry")).ToList();
            if (registryItems.Any())
            {
                var result = await _registryService.CleanRegistryItemsAsync(registryItems, progress, _cancellationTokenSource.Token);
                CurrentSession.CleanedItems.AddRange(result.CleanedItems);
                CurrentSession.FailedItems.AddRange(result.FailedItems);
            }
        }

        private void CancelOperation()
        {
            _cancellationTokenSource?.Cancel();
            _loggingService.LogInfo("Operation cancelled by user.");
        }

        private async void LoadSystemInfoAsync()
        {
            try
            {
                SystemInfo = await Task.Run(() => GetSystemInfo());
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Error loading system info: {ex.Message}", ex);
            }
        }

        private SystemInfo GetSystemInfo()
        {
            var systemInfo = new SystemInfo
            {
                OperatingSystem = Environment.OSVersion.ToString(),
                Version = Environment.OSVersion.Version.ToString(),
                Architecture = Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit",
                ProcessorName = Environment.ProcessorCount.ToString() + " cores"
            };

            // Get memory info
            var memoryInfo = GC.GetTotalMemory(false);
            systemInfo.TotalMemory = memoryInfo;

            // Get drive info
            var drives = DriveInfo.GetDrives();
            foreach (var drive in drives.Where(d => d.IsReady))
            {
                systemInfo.Drives.Add(new DiskInfo
                {
                    DriveLetter = drive.Name,
                    Label = drive.VolumeLabel,
                    FileSystem = drive.DriveFormat,
                    TotalSpace = drive.TotalSize,
                    FreeSpace = drive.AvailableFreeSpace,
                    UsedSpace = drive.TotalSize - drive.AvailableFreeSpace,
                    HealthStatus = DriveHealthStatus.Healthy
                });
            }

            return systemInfo;
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
