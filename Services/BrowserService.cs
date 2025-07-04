using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using WindowsPCCleaner.Models;

namespace WindowsPCCleaner.Services
{
    public interface IBrowserService
    {
        Task<List<CleanableItem>> ScanBrowserDataAsync(BrowserScanOptions options, IProgress<ScanProgress> progress, CancellationToken cancellationToken);
        Task<List<BrowserInfo>> GetInstalledBrowsersAsync();
        Task<BrowserCleaningResult> CleanBrowserDataAsync(List<CleanableItem> items, IProgress<CleaningProgress> progress, CancellationToken cancellationToken);
    }

    public class BrowserService : IBrowserService
    {
        private readonly ILoggingService _loggingService;
        private readonly ISecurityService _securityService;

        private readonly Dictionary<BrowserType, BrowserConfig> _browserConfigs = new()
        {
            [BrowserType.Chrome] = new BrowserConfig
            {
                Name = "Google Chrome",
                ExecutableName = "chrome.exe",
                UserDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Google", "Chrome", "User Data"),
                CachePaths = new[] { "Default\\Cache", "Default\\Code Cache", "Default\\GPUCache" },
                CookiesPath = "Default\\Cookies",
                HistoryPath = "Default\\History",
                DownloadsPath = "Default\\History",
                PasswordsPath = "Default\\Login Data",
                BookmarksPath = "Default\\Bookmarks"
            },
            [BrowserType.Firefox] = new BrowserConfig
            {
                Name = "Mozilla Firefox",
                ExecutableName = "firefox.exe",
                UserDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mozilla", "Firefox", "Profiles"),
                CachePaths = new[] { "cache2", "startupCache", "OfflineCache" },
                CookiesPath = "cookies.sqlite",
                HistoryPath = "places.sqlite",
                DownloadsPath = "places.sqlite",
                PasswordsPath = "logins.json",
                BookmarksPath = "places.sqlite"
            },
            [BrowserType.Edge] = new BrowserConfig
            {
                Name = "Microsoft Edge",
                ExecutableName = "msedge.exe",
                UserDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Edge", "User Data"),
                CachePaths = new[] { "Default\\Cache", "Default\\Code Cache", "Default\\GPUCache" },
                CookiesPath = "Default\\Cookies",
                HistoryPath = "Default\\History",
                DownloadsPath = "Default\\History",
                PasswordsPath = "Default\\Login Data",
                BookmarksPath = "Default\\Bookmarks"
            },
            [BrowserType.Opera] = new BrowserConfig
            {
                Name = "Opera",
                ExecutableName = "opera.exe",
                UserDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Opera Software", "Opera Stable"),
                CachePaths = new[] { "Cache", "Code Cache", "GPUCache" },
                CookiesPath = "Cookies",
                HistoryPath = "History",
                DownloadsPath = "History",
                PasswordsPath = "Login Data",
                BookmarksPath = "Bookmarks"
            }
        };

        public BrowserService(ILoggingService loggingService, ISecurityService securityService)
        {
            _loggingService = loggingService;
            _securityService = securityService;
        }

        public async Task<List<CleanableItem>> ScanBrowserDataAsync(BrowserScanOptions options, IProgress<ScanProgress> progress, CancellationToken cancellationToken)
        {
            var items = new List<CleanableItem>();
            var installedBrowsers = await GetInstalledBrowsersAsync();
            var totalBrowsers = installedBrowsers.Count;
            var currentBrowser = 0;

            foreach (var browser in installedBrowsers)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                progress?.Report(new ScanProgress
                {
                    CurrentStep = currentBrowser + 1,
                    TotalSteps = totalBrowsers,
                    CurrentOperation = $"Scanning {browser.Name}..."
                });

                try
                {
                    var browserItems = await ScanSingleBrowserAsync(browser, options, cancellationToken);
                    items.AddRange(browserItems);
                }
                catch (Exception ex)
                {
                    _loggingService.LogError($"Error scanning {browser.Name}: {ex.Message}", ex);
                }

                currentBrowser++;
            }

            return items;
        }

        public async Task<List<BrowserInfo>> GetInstalledBrowsersAsync()
        {
            var browsers = new List<BrowserInfo>();

            await Task.Run(() =>
            {
                foreach (var kvp in _browserConfigs)
                {
                    var config = kvp.Value;
                    if (IsBrowserInstalled(config))
                    {
                        browsers.Add(new BrowserInfo
                        {
                            Type = kvp.Key,
                            Name = config.Name,
                            Version = GetBrowserVersion(config),
                            UserDataPath = config.UserDataPath,
                            IsRunning = IsBrowserRunning(config.ExecutableName),
                            ProfileCount = GetProfileCount(config)
                        });
                    }
                }
            });

            return browsers;
        }

        public async Task<BrowserCleaningResult> CleanBrowserDataAsync(List<CleanableItem> items, IProgress<CleaningProgress> progress, CancellationToken cancellationToken)
        {
            var result = new BrowserCleaningResult();
            var totalItems = items.Count;
            var processedItems = 0;

            // Group items by browser to handle browser-specific cleaning
            var browserGroups = items.GroupBy(item => item.BrowserType);

            foreach (var browserGroup in browserGroups)
            {
                var browserType = browserGroup.Key;
                var browserItems = browserGroup.ToList();

                // Check if browser is running and warn user
                var config = _browserConfigs[browserType];
                if (IsBrowserRunning(config.ExecutableName))
                {
                    result.Warnings.Add($"{config.Name} is currently running. Some files may not be cleaned.");
                }

                foreach (var item in browserItems)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    progress?.Report(new CleaningProgress
                    {
                        ProcessedItems = processedItems,
                        TotalItems = totalItems,
                        CurrentItem = item.Name,
                        CurrentOperation = $"Cleaning {config.Name} data..."
                    });

                    try
                    {
                        await CleanBrowserItemAsync(item, cancellationToken);
                        result.CleanedItems.Add(item);
                        result.TotalSizeFreed += item.Size;
                    }
                    catch (Exception ex)
                    {
                        _loggingService.LogError($"Error cleaning {item.Name}: {ex.Message}", ex);
                        result.FailedItems.Add(new FailedCleaningItem { Item = item, Error = ex.Message });
                    }

                    processedItems++;
                }
            }

            result.Success = result.FailedItems.Count == 0;
            return result;
        }

        private async Task<List<CleanableItem>> ScanSingleBrowserAsync(BrowserInfo browser, BrowserScanOptions options, CancellationToken cancellationToken)
        {
            var items = new List<CleanableItem>();
            var config = _browserConfigs[browser.Type];

            // Scan Cache
            if (options.ScanCache)
            {
                var cacheItems = await ScanBrowserCacheAsync(browser, config, cancellationToken);
                items.AddRange(cacheItems);
            }

            // Scan Cookies
            if (options.ScanCookies)
            {
                var cookieItems = await ScanBrowserCookiesAsync(browser, config, cancellationToken);
                items.AddRange(cookieItems);
            }

            // Scan History
            if (options.ScanHistory)
            {
                var historyItems = await ScanBrowserHistoryAsync(browser, config, cancellationToken);
                items.AddRange(historyItems);
            }

            // Scan Downloads
            if (options.ScanDownloads)
            {
                var downloadItems = await ScanBrowserDownloadsAsync(browser, config, cancellationToken);
                items.AddRange(downloadItems);
            }

            // Scan Passwords (with high risk warning)
            if (options.ScanPasswords)
            {
                var passwordItems = await ScanBrowserPasswordsAsync(browser, config, cancellationToken);
                items.AddRange(passwordItems);
            }

            return items;
        }

        private async Task<List<CleanableItem>> ScanBrowserCacheAsync(BrowserInfo browser, BrowserConfig config, CancellationToken cancellationToken)
        {
            var items = new List<CleanableItem>();

            await Task.Run(() =>
            {
                foreach (var cachePath in config.CachePaths)
                {
                    var fullCachePath = Path.Combine(config.UserDataPath, cachePath);
                    if (Directory.Exists(fullCachePath))
                    {
                        try
                        {
                            var cacheSize = CalculateDirectorySize(fullCachePath);
                            if (cacheSize > 0)
                            {
                                items.Add(new CleanableItem
                                {
                                    Name = $"{browser.Name} Cache",
                                    Path = fullCachePath,
                                    Size = cacheSize,
                                    Category = "Browser Cache",
                                    BrowserType = browser.Type,
                                    Risk = CleaningRisk.Safe,
                                    Description = $"Temporary cache files from {browser.Name}",
                                    Icon = "🌐",
                                    IsSelected = true
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            _loggingService.LogWarning($"Could not scan cache path {fullCachePath}: {ex.Message}");
                        }
                    }
                }
            }, cancellationToken);

            return items;
        }

        private async Task<List<CleanableItem>> ScanBrowserCookiesAsync(BrowserInfo browser, BrowserConfig config, CancellationToken cancellationToken)
        {
            var items = new List<CleanableItem>();

            await Task.Run(() =>
            {
                var cookiesPath = Path.Combine(config.UserDataPath, config.CookiesPath);
                if (File.Exists(cookiesPath))
                {
                    try
                    {
                        var fileInfo = new FileInfo(cookiesPath);
                        items.Add(new CleanableItem
                        {
                            Name = $"{browser.Name} Cookies",
                            Path = cookiesPath,
                            Size = fileInfo.Length,
                            Category = "Browser Cookies",
                            BrowserType = browser.Type,
                            Risk = CleaningRisk.Medium,
                            Description = $"Stored cookies from {browser.Name}. You may need to log in to websites again.",
                            Icon = "🍪",
                            IsSelected = false // Default to not selected due to medium risk
                        });
                    }
                    catch (Exception ex)
                    {
                        _loggingService.LogWarning($"Could not scan cookies file {cookiesPath}: {ex.Message}");
                    }
                }
            }, cancellationToken);

            return items;
        }

        private async Task<List<CleanableItem>> ScanBrowserHistoryAsync(BrowserInfo browser, BrowserConfig config, CancellationToken cancellationToken)
        {
            var items = new List<CleanableItem>();

            await Task.Run(() =>
            {
                var historyPath = Path.Combine(config.UserDataPath, config.HistoryPath);
                if (File.Exists(historyPath))
                {
                    try
                    {
                        var fileInfo = new FileInfo(historyPath);
                        items.Add(new CleanableItem
                        {
                            Name = $"{browser.Name} History",
                            Path = historyPath,
                            Size = fileInfo.Length,
                            Category = "Browser History",
                            BrowserType = browser.Type,
                            Risk = CleaningRisk.Low,
                            Description = $"Browsing history from {browser.Name}",
                            Icon = "📚",
                            IsSelected = true
                        });
                    }
                    catch (Exception ex)
                    {
                        _loggingService.LogWarning($"Could not scan history file {historyPath}: {ex.Message}");
                    }
                }
            }, cancellationToken);

            return items;
        }

        private async Task<List<CleanableItem>> ScanBrowserDownloadsAsync(BrowserInfo browser, BrowserConfig config, CancellationToken cancellationToken)
        {
            var items = new List<CleanableItem>();

            await Task.Run(() =>
            {
                var downloadsPath = Path.Combine(config.UserDataPath, config.DownloadsPath);
                if (File.Exists(downloadsPath))
                {
                    try
                    {
                        // For browsers that store download history in the same file as browsing history,
                        // we create a separate entry for clarity
                        var fileInfo = new FileInfo(downloadsPath);
                        items.Add(new CleanableItem
                        {
                            Name = $"{browser.Name} Download History",
                            Path = downloadsPath,
                            Size = fileInfo.Length / 2, // Approximate size for download history portion
                            Category = "Browser Downloads",
                            BrowserType = browser.Type,
                            Risk = CleaningRisk.Low,
                            Description = $"Download history from {browser.Name}",
                            Icon = "📥",
                            IsSelected = true
                        });
                    }
                    catch (Exception ex)
                    {
                        _loggingService.LogWarning($"Could not scan downloads file {downloadsPath}: {ex.Message}");
                    }
                }
            }, cancellationToken);

            return items;
        }

        private async Task<List<CleanableItem>> ScanBrowserPasswordsAsync(BrowserInfo browser, BrowserConfig config, CancellationToken cancellationToken)
        {
            var items = new List<CleanableItem>();

            await Task.Run(() =>
            {
                var passwordsPath = Path.Combine(config.UserDataPath, config.PasswordsPath);
                if (File.Exists(passwordsPath))
                {
                    try
                    {
                        var fileInfo = new FileInfo(passwordsPath);
                        items.Add(new CleanableItem
                        {
                            Name = $"{browser.Name} Saved Passwords",
                            Path = passwordsPath,
                            Size = fileInfo.Length,
                            Category = "Browser Passwords",
                            BrowserType = browser.Type,
                            Risk = CleaningRisk.High,
                            Description = $"Saved passwords from {browser.Name}. WARNING: You will lose all saved passwords!",
                            Icon = "🔐",
                            IsSelected = false // Default to not selected due to high risk
                        });
                    }
                    catch (Exception ex)
                    {
                        _loggingService.LogWarning($"Could not scan passwords file {passwordsPath}: {ex.Message}");
                    }
                }
            }, cancellationToken);

            return items;
        }

        private async Task CleanBrowserItemAsync(CleanableItem item, CancellationToken cancellationToken)
        {
            await Task.Run(() =>
            {
                try
                {
                    if (Directory.Exists(item.Path))
                    {
                        // For cache directories, delete contents but keep the directory
                        if (item.Category == "Browser Cache")
                        {
                            var files = Directory.GetFiles(item.Path, "*", SearchOption.AllDirectories);
                            foreach (var file in files)
                            {
                                try
                                {
                                    File.Delete(file);
                                }
                                catch
                                {
                                    // Some cache files may be locked, continue with others
                                }
                            }

                            var directories = Directory.GetDirectories(item.Path, "*", SearchOption.AllDirectories);
                            foreach (var dir in directories.OrderByDescending(d => d.Length)) // Delete deepest first
                            {
                                try
                                {
                                    Directory.Delete(dir, false);
                                }
                                catch
                                {
                                    // Directory may not be empty, continue
                                }
                            }
                        }
                        else
                        {
                            Directory.Delete(item.Path, true);
                        }
                    }
                    else if (File.Exists(item.Path))
                    {
                        // For sensitive data like passwords, use secure deletion
                        if (item.Risk == CleaningRisk.High)
                        {
                            _securityService.SecureDeleteFile(item.Path);
                        }
                        else
                        {
                            File.Delete(item.Path);
                        }
                    }
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Failed to clean {item.Name}: {ex.Message}", ex);
                }
            }, cancellationToken);
        }

        private bool IsBrowserInstalled(BrowserConfig config)
        {
            return Directory.Exists(config.UserDataPath) || IsBrowserInRegistry(config.Name);
        }

        private bool IsBrowserInRegistry(string browserName)
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
                if (key != null)
                {
                    foreach (var subKeyName in key.GetSubKeyNames())
                    {
                        using var subKey = key.OpenSubKey(subKeyName);
                        var displayName = subKey?.GetValue("DisplayName")?.ToString();
                        if (!string.IsNullOrEmpty(displayName) && displayName.Contains(browserName, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogWarning($"Could not check registry for {browserName}: {ex.Message}");
            }

            return false;
        }

        private string GetBrowserVersion(BrowserConfig config)
        {
            try
            {
                // Try to get version from registry first
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
                if (key != null)
                {
                    foreach (var subKeyName in key.GetSubKeyNames())
                    {
                        using var subKey = key.OpenSubKey(subKeyName);
                        var displayName = subKey?.GetValue("DisplayName")?.ToString();
                        if (!string.IsNullOrEmpty(displayName) && displayName.Contains(config.Name, StringComparison.OrdinalIgnoreCase))
                        {
                            return subKey?.GetValue("DisplayVersion")?.ToString() ?? "Unknown";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogWarning($"Could not get version for {config.Name}: {ex.Message}");
            }

            return "Unknown";
        }

        private bool IsBrowserRunning(string executableName)
        {
            try
            {
                var processes = System.Diagnostics.Process.GetProcessesByName(Path.GetFileNameWithoutExtension(executableName));
                return processes.Length > 0;
            }
            catch
            {
                return false;
            }
        }

        private int GetProfileCount(BrowserConfig config)
        {
            try
            {
                if (!Directory.Exists(config.UserDataPath))
                    return 0;

                // Count profile directories (Default, Profile 1, Profile 2, etc.)
                var profileDirs = Directory.GetDirectories(config.UserDataPath)
                    .Where(dir => Path.GetFileName(dir).StartsWith("Default") || Path.GetFileName(dir).StartsWith("Profile"))
                    .Count();

                return Math.Max(1, profileDirs);
            }
            catch
            {
                return 1;
            }
        }

        private long CalculateDirectorySize(string directoryPath)
        {
            try
            {
                return Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories)
                    .Sum(file => new FileInfo(file).Length);
            }
            catch
            {
                return 0;
            }
        }
    }
}
