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
    public class SystemCleaningViewModel : INotifyPropertyChanged
    {
        private readonly ISystemCleaningService _systemCleaningService;
        private readonly ILoggingService _loggingService;

        private ObservableCollection<CleanableItem> _cleanableItems;
        private ObservableCollection<CleaningCategory> _categories;
        private bool _isScanning;
        private bool _selectAll = true;
        private long _totalSelectedSize;
        private int _totalSelectedCount;

        public SystemCleaningViewModel(ISystemCleaningService systemCleaningService, ILoggingService loggingService)
        {
            _systemCleaningService = systemCleaningService;
            _loggingService = loggingService;

            CleanableItems = new ObservableCollection<CleanableItem>();
            Categories = new ObservableCollection<CleaningCategory>();

            InitializeCommands();
            InitializeCategories();
        }

        public ObservableCollection<CleanableItem> CleanableItems
        {
            get => _cleanableItems;
            set => SetProperty(ref _cleanableItems, value);
        }

        public ObservableCollection<CleaningCategory> Categories
        {
            get => _categories;
            set => SetProperty(ref _categories, value);
        }

        public bool IsScanning
        {
            get => _isScanning;
            set => SetProperty(ref _isScanning, value);
        }

        public bool SelectAll
        {
            get => _selectAll;
            set
            {
                if (SetProperty(ref _selectAll, value))
                {
                    UpdateAllItemsSelection(value);
                }
            }
        }

        public long TotalSelectedSize
        {
            get => _totalSelectedSize;
            set => SetProperty(ref _totalSelectedSize, value);
        }

        public int TotalSelectedCount
        {
            get => _totalSelectedCount;
            set => SetProperty(ref _totalSelectedCount, value);
        }

        public ICommand ScanCommand { get; private set; }
        public ICommand CleanCommand { get; private set; }
        public ICommand SelectCategoryCommand { get; private set; }

        private void InitializeCommands()
        {
            ScanCommand = new AsyncRelayCommand(ScanSystemAsync, () => !IsScanning);
            CleanCommand = new AsyncRelayCommand(CleanSelectedAsync, () => !IsScanning && CleanableItems.Any(i => i.IsSelected));
            SelectCategoryCommand = new RelayCommand<CleaningCategory>(SelectCategory);
        }

        private void InitializeCategories()
        {
            Categories.Add(new CleaningCategory
            {
                Name = "Temporary Files",
                Description = "Windows and application temporary files",
                IsSelected = true,
                Risk = CleaningRisk.Safe
            });

            Categories.Add(new CleaningCategory
            {
                Name = "System Cache",
                Description = "System cache and thumbnail files",
                IsSelected = true,
                Risk = CleaningRisk.Safe
            });

            Categories.Add(new CleaningCategory
            {
                Name = "Log Files",
                Description = "System and application log files",
                IsSelected = true,
                Risk = CleaningRisk.Low
            });

            Categories.Add(new CleaningCategory
            {
                Name = "Recycle Bin",
                Description = "Files in the Recycle Bin",
                IsSelected = false,
                Risk = CleaningRisk.Medium
            });

            Categories.Add(new CleaningCategory
            {
                Name = "Memory Dumps",
                Description = "System crash dump files",
                IsSelected = true,
                Risk = CleaningRisk.Low
            });
        }

        private async Task ScanSystemAsync()
        {
            try
            {
                IsScanning = true;
                CleanableItems.Clear();

                var progress = new Progress<ScanProgress>(UpdateScanProgress);
                var options = new SystemScanOptions
                {
                    ScanTempFiles = Categories.First(c => c.Name == "Temporary Files").IsSelected,
                    ScanSystemCache = Categories.First(c => c.Name == "System Cache").IsSelected,
                    ScanLogFiles = Categories.First(c => c.Name == "Log Files").IsSelected,
                    ScanRecycleBin = Categories.First(c => c.Name == "Recycle Bin").IsSelected,
                    ScanMemoryDumps = Categories.First(c => c.Name == "Memory Dumps").IsSelected
                };

                var items = await _systemCleaningService.ScanSystemAsync(options, progress, CancellationToken.None);

                foreach (var item in items)
                {
                    CleanableItems.Add(item);
                }

                UpdateSelectionTotals();
                _loggingService.LogInfo($"System scan completed. Found {items.Count} items.");
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Error during system scan: {ex.Message}", ex);
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
                if (!selectedItems.Any())
                {
                    _loggingService.LogWarning("No items selected for cleaning.");
                    return;
                }

                var progress = new Progress<CleaningProgress>(UpdateCleaningProgress);
                var result = await _systemCleaningService.CleanItemsAsync(selectedItems, progress, CancellationToken.None);

                // Remove cleaned items from the list
                foreach (var cleanedItem in result.CleanedItems)
                {
                    var item = CleanableItems.FirstOrDefault(i => i.Path == cleanedItem.Path);
                    if (item != null)
                    {
                        CleanableItems.Remove(item);
                    }
                }

                UpdateSelectionTotals();
                _loggingService.LogInfo($"Cleaning completed. Freed {FormatBytes(result.TotalSizeFreed)}.");
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Error during cleaning: {ex.Message}", ex);
            }
        }

        private void SelectCategory(CleaningCategory category)
        {
            category.IsSelected = !category.IsSelected;
            
            // Update items in this category
            foreach (var item in CleanableItems.Where(i => i.Category == category.Name))
            {
                item.IsSelected = category.IsSelected;
            }

            UpdateSelectionTotals();
        }

        private void UpdateAllItemsSelection(bool isSelected)
        {
            foreach (var item in CleanableItems)
            {
                item.IsSelected = isSelected;
            }

            foreach (var category in Categories)
            {
                category.IsSelected = isSelected;
            }

            UpdateSelectionTotals();
        }

        private void UpdateSelectionTotals()
        {
            var selectedItems = CleanableItems.Where(i => i.IsSelected);
            TotalSelectedCount = selectedItems.Count();
            TotalSelectedSize = selectedItems.Sum(i => i.Size);
        }

        private void UpdateScanProgress(ScanProgress progress)
        {
            // Update UI with scan progress
        }

        private void UpdateCleaningProgress(CleaningProgress progress)
        {
            // Update UI with cleaning progress
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

    public class CleaningCategory : INotifyPropertyChanged
    {
        private bool _isSelected;

        public string Name { get; set; }
        public string Description { get; set; }
        public CleaningRisk Risk { get; set; }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
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
