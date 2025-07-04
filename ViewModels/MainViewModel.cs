using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using WindowsPCCleaner.Commands;
using WindowsPCCleaner.Models;
using WindowsPCCleaner.Services;

namespace WindowsPCCleaner.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly IThemeService _themeService;
        private readonly ILoggingService _loggingService;
        private object _currentViewModel;
        private NavigationItem _selectedNavigationItem;
        private bool _isDarkTheme;

        public MainViewModel(
            IThemeService themeService,
            ILoggingService loggingService,
            DashboardViewModel dashboardViewModel,
            CleaningViewModel cleaningViewModel,
            PrivacyViewModel privacyViewModel,
            ToolsViewModel toolsViewModel,
            SettingsViewModel settingsViewModel)
        {
            _themeService = themeService;
            _loggingService = loggingService;

            // Initialize ViewModels
            DashboardViewModel = dashboardViewModel;
            CleaningViewModel = cleaningViewModel;
            PrivacyViewModel = privacyViewModel;
            ToolsViewModel = toolsViewModel;
            SettingsViewModel = settingsViewModel;

            // Initialize Navigation
            NavigationItems = new ObservableCollection<NavigationItem>
            {
                new NavigationItem { Name = "Dashboard", Icon = "🏠", ViewModel = DashboardViewModel },
                new NavigationItem { Name = "System Cleaner", Icon = "🧹", ViewModel = CleaningViewModel },
                new NavigationItem { Name = "Privacy", Icon = "🔒", ViewModel = PrivacyViewModel },
                new NavigationItem { Name = "Tools", Icon = "🔧", ViewModel = ToolsViewModel },
                new NavigationItem { Name = "Settings", Icon = "⚙️", ViewModel = SettingsViewModel }
            };

            // Set default view
            SelectedNavigationItem = NavigationItems[0];
            CurrentViewModel = DashboardViewModel;

            // Initialize theme
            IsDarkTheme = _themeService.IsDarkTheme;

            // Commands
            NavigateCommand = new RelayCommand<NavigationItem>(Navigate);
            ToggleThemeCommand = new RelayCommand(ToggleTheme);
            MinimizeCommand = new RelayCommand(Minimize);
            CloseCommand = new RelayCommand(Close);
        }

        public ObservableCollection<NavigationItem> NavigationItems { get; }
        public DashboardViewModel DashboardViewModel { get; }
        public CleaningViewModel CleaningViewModel { get; }
        public PrivacyViewModel PrivacyViewModel { get; }
        public ToolsViewModel ToolsViewModel { get; }
        public SettingsViewModel SettingsViewModel { get; }

        public object CurrentViewModel
        {
            get => _currentViewModel;
            set => SetProperty(ref _currentViewModel, value);
        }

        public NavigationItem SelectedNavigationItem
        {
            get => _selectedNavigationItem;
            set => SetProperty(ref _selectedNavigationItem, value);
        }

        public bool IsDarkTheme
        {
            get => _isDarkTheme;
            set => SetProperty(ref _isDarkTheme, value);
        }

        public ICommand NavigateCommand { get; }
        public ICommand ToggleThemeCommand { get; }
        public ICommand MinimizeCommand { get; }
        public ICommand CloseCommand { get; }

        private void Navigate(NavigationItem item)
        {
            if (item != null)
            {
                SelectedNavigationItem = item;
                CurrentViewModel = item.ViewModel;
                _loggingService.LogInfo($"Navigated to {item.Name}");
            }
        }

        private void ToggleTheme()
        {
            IsDarkTheme = !IsDarkTheme;
            _themeService.SetTheme(IsDarkTheme);
        }

        private void Minimize()
        {
            // Minimize window logic
        }

        private void Close()
        {
            // Close application logic
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
