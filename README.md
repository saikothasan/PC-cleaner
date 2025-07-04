# Windows PC Cleaner

A comprehensive, professional-grade Windows PC cleaner application built with C# WPF, featuring modern UI design, robust functionality, and enterprise-level code quality.

## 🚀 Features

### Core Cleaning Capabilities
- **System Cleaning**: Temporary files, system cache, log files, recycle bin, memory dumps
- **Browser Cleaning**: Multi-browser support (Chrome, Firefox, Edge, Opera, Safari)
- **Registry Cleaning**: Safe registry cleanup with automatic backup and restore
- **Privacy Protection**: Comprehensive privacy file scanning and secure deletion
- **Disk Analysis**: Visual disk usage analysis and large file detection
- **Startup Management**: Optimize system startup with impact analysis

### Advanced Features
- **Security Scanning**: Detect suspicious files and assess security risks
- **Secure Deletion**: DoD 5220.22-M compliant file wiping
- **Duplicate File Finder**: Find and remove duplicate files with preview
- **Empty Folder Detection**: Identify and clean empty directories
- **System Health Monitoring**: Real-time system performance metrics
- **Scheduled Cleaning**: Automated cleaning with custom schedules

### User Experience
- **Modern UI**: Fluent Design with dark/light theme support
- **Real-time Progress**: Live progress updates for all operations
- **Smart Recommendations**: AI-powered cleaning suggestions
- **Detailed Reports**: Comprehensive cleaning reports with statistics
- **Undo Functionality**: Restore deleted files from secure backup
- **Multi-language Support**: Localized interface

## 🏗️ Architecture

### Technology Stack
- **.NET 6.0+**: Modern C# with latest language features
- **WPF**: Windows Presentation Foundation for rich desktop UI
- **MVVM Pattern**: Model-View-ViewModel architecture
- **Dependency Injection**: Microsoft.Extensions.DependencyInjection
- **Async/Await**: Asynchronous operations for responsive UI

### Project Structure
```
WindowsPCCleaner/
├── Models/                     # Data models and entities
│   ├── CommonModels.cs        # Shared data structures
│   ├── SecurityModels.cs      # Security-related models
│   ├── BrowserModels.cs       # Browser cleaning models
│   ├── RegistryModels.cs      # Registry operation models
│   ├── DiskAnalysisModels.cs  # Disk analysis models
│   └── StartupModels.cs       # Startup management models
├── Services/                   # Business logic and data access
│   ├── ISystemCleaningService.cs
│   ├── SystemCleaningService.cs
│   ├── IBrowserService.cs
│   ├── BrowserService.cs
│   ├── IRegistryService.cs
│   ├── RegistryService.cs
│   ├── IDiskAnalysisService.cs
│   ├── DiskAnalysisService.cs
│   ├── IStartupManagerService.cs
│   ├── StartupManagerService.cs
│   ├── ISecurityService.cs
│   ├── SecurityService.cs
│   ├── ILoggingService.cs
│   └── LoggingService.cs
├── ViewModels/                 # MVVM ViewModels
│   ├── MainViewModel.cs       # Main application coordinator
│   ├── DashboardViewModel.cs  # Dashboard overview
│   ├── SystemCleaningViewModel.cs
│   ├── BrowserCleaningViewModel.cs
│   ├── RegistryCleaningViewModel.cs
│   ├── DiskAnalysisViewModel.cs
│   ├── StartupManagerViewModel.cs
│   └── SecurityViewModel.cs
├── Views/                      # WPF User Interface
│   ├── MainWindow.xaml
│   ├── DashboardView.xaml
│   ├── SystemCleaningView.xaml
│   ├── BrowserCleaningView.xaml
│   ├── RegistryCleaningView.xaml
│   ├── DiskAnalysisView.xaml
│   ├── StartupManagerView.xaml
│   └── SecurityView.xaml
├── Utilities/                  # Helper classes and extensions
│   ├── RelayCommand.cs        # Command implementations
│   ├── Converters/            # Value converters
│   └── Extensions/            # Extension methods
├── Themes/                     # UI themes and styles
│   ├── DarkTheme.xaml
│   ├── LightTheme.xaml
│   └── CommonStyles.xaml
└── Resources/                  # Images, icons, and localization
    ├── Images/
    ├── Icons/
    └── Localization/
```

## 🛠️ Installation

### Prerequisites
- Windows 10/11 (64-bit recommended)
- .NET 6.0 Runtime or later
- Administrator privileges (for system-level operations)
- Minimum 4GB RAM, 100MB disk space

### Installation Steps
1. Download the latest release from https://github.com/saikothasan/PC-cleaner
2. Run the installer as Administrator
3. Follow the installation wizard
4. Launch the application from Start Menu or Desktop shortcut

### Building from Source
```bash
# Clone the repository
git clone https://github.com/yourrepo/windows-pc-cleaner.git
cd windows-pc-cleaner

# Restore NuGet packages
dotnet restore

# Build the solution
dotnet build --configuration Release

# Run the application
dotnet run --project WindowsPCCleaner
