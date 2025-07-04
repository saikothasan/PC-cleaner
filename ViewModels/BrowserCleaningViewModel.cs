using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using WindowsPCCleaner.Models;
using WindowsPCCleaner.Services;

namespace WindowsPCCleaner.ViewModels
{
    public class BrowserCleaningViewModel : INotifyPropertyChanged
    {
        private readonly IBrowserService _browserService;
        private readonly ILoggingService _loggingService;

        private ObservableCollection<CleanableItem> _cleanableItems;
        private ObservableCollection<BrowserInfo> _detectedBrowsers;
        private BrowserScanOptions _scanOptions;
        private bool _isScanning;

        public BrowserCleaningViewModel(IBrowserService browserService, ILoggingService loggingService)
        {
            _browserService = browserService;
            _loggingService = loggingService;

            CleanableItems = new ObservableCollection<CleanableItem>();
            DetectedBrowsers = new ObservableCollection<BrowserInfo>();
            ScanOptions = new BrowserScanOptions();

            InitializeCommands();
            DetectBrowsersAsync();
        }

        public ObservableCollection<CleanableItem> CleanableItems
        {
            get => _cleanableItems;
            set => SetProperty(ref _cleanableItems, value);
        }

        public ObservableCollection<BrowserInfo> DetectedBrowsers
        {
            get => _detectedBrowsers;
            set => SetProperty(ref _detectedBrowsers, value);
        }

        public BrowserScanOptions ScanOptions
        {
            get => _scanOptions;
            set => SetProperty(ref _scanOptions, value);
        }

        public bool IsScanning
        {
            get => _isScanning;
            set => SetProperty(ref _isScanning, value);
        }

        public ICommand ScanBrowsersCommand { get; private set; }
        public ICommand CleanSelectedCommand { get; private set; }
        public ICommand RefreshBrowsersCommand { get; private set; }

        private void InitializeCommands()
        {
            ScanBrowsersCommand = new AsyncRelayCommand(ScanBrowsersAsync, () => !IsScanning);
            CleanSelectedCommand = new AsyncRelayCommand(CleanSelectedAsync, () => !IsScanning);
            RefreshBrowsersCommand = new AsyncRelayCommand(DetectBrowsersAsync, () => !IsScanning);
        }

        private async Task DetectBrowsersAsync()
        {
            try
            {
                var browsers = await _browserService.DetectInstalledBrowsersAsync();
                DetectedBrowsers.Clear();
                foreach (var browser in browsers)
                {
                    DetectedBrowsers.Add(browser);
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Error detecting browsers: {ex.Message}", ex);
            }
        }

        private async Task ScanBrowsersAsync()
        {
            try
            {
                IsScanning = true;
                CleanableItems.Clear();

                var progress = new Progress<ScanProgress>();
                var items = await _browserService.ScanBrowsersAsync(ScanOptions, progress, CancellationToken.None);

                foreach (var item in items)
                {
                    CleanableItems.Add(item);
                }

                _loggingService.LogInfo($"Browser scan completed. Found {items.Count} items.");
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Error during browser scan: {ex.Message}", ex);
            }
            finally
            {
                IsScanning = false;
            }
        }

        private async Task CleanSelectedAsync()
        {
            try
            {
                var selectedItems = CleanableItems.Where(i => i.IsSelected).ToList();
                if (!selectedItems.Any()) return;

                var progress = new Progress<CleaningProgress>();
                var result = await _browserService.CleanBrowserDataAsync(selectedItems, progress, CancellationToken.None);

                foreach (var cleanedItem in result.CleanedItems)
                {
                    var item = CleanableItems.FirstOrDefault(i => i.Path == cleanedItem.Path);
                    if (item != null)
                    {
                        CleanableItems.Remove(item);
                    }
                }

                _loggingService.LogInfo($"Browser cleaning completed. Freed {FormatBytes(result.TotalSizeFreed)}.");
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Error during browser cleaning: {ex.Message}", ex);
            }
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
