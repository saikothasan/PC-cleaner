using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WindowsPCCleaner.Models;

namespace WindowsPCCleaner.Services
{
    public interface ICleaningService
    {
        Task<ScanResult> ScanSystemAsync(ScanOptions options, IProgress<ScanProgress> progress, CancellationToken cancellationToken);
        Task<CleaningResult> CleanSelectedItemsAsync(IEnumerable<CleanableItem> items, IProgress<CleaningProgress> progress, CancellationToken cancellationToken);
        Task<long> CalculateSizeAsync(IEnumerable<string> paths);
    }

    public class CleaningService : ICleaningService
    {
        private readonly ILoggingService _loggingService;
        private readonly ISecurityService _securityService;

        public CleaningService(ILoggingService loggingService, ISecurityService securityService)
        {
            _loggingService = loggingService;
            _securityService = securityService;
        }

        public async Task<ScanResult> ScanSystemAsync(ScanOptions options, IProgress<ScanProgress> progress, CancellationToken cancellationToken)
        {
            var result = new ScanResult();
            var totalSteps = GetTotalScanSteps(options);
            var currentStep = 0;

            try
            {
                // Scan Temporary Files
                if (options.ScanTemporaryFiles)
                {
                    progress?.Report(new ScanProgress { CurrentStep = ++currentStep, TotalSteps = totalSteps, CurrentOperation = "Scanning temporary files..." });
                    var tempItems = await ScanTemporaryFilesAsync(cancellationToken);
                    result.CleanableItems.AddRange(tempItems);
                }

                // Scan System Cache
                if (options.ScanSystemCache)
                {
                    progress?.Report(new ScanProgress { CurrentStep = ++currentStep, TotalSteps = totalSteps, CurrentOperation = "Scanning system cache..." });
                    var cacheItems = await ScanSystemCacheAsync(cancellationToken);
                    result.CleanableItems.AddRange(cacheItems);
                }

                // Scan Log Files
                if (options.ScanLogFiles)
                {
                    progress?.Report(new ScanProgress { CurrentStep = ++currentStep, TotalSteps = totalSteps, CurrentOperation = "Scanning log files..." });
                    var logItems = await ScanLogFilesAsync(cancellationToken);
                    result.CleanableItems.AddRange(logItems);
                }

                // Scan Recycle Bin
                if (options.ScanRecycleBin)
                {
                    progress?.Report(new ScanProgress { CurrentStep = ++currentStep, TotalSteps = totalSteps, CurrentOperation = "Scanning recycle bin..." });
                    var recycleItems = await ScanRecycleBinAsync(cancellationToken);
                    result.CleanableItems.AddRange(recycleItems);
                }

                // Scan Registry
                if (options.ScanRegistry)
                {
                    progress?.Report(new ScanProgress { CurrentStep = ++currentStep, TotalSteps = totalSteps, CurrentOperation = "Scanning registry..." });
                    var registryItems = await ScanRegistryAsync(cancellationToken);
                    result.CleanableItems.AddRange(registryItems);
                }

                // Calculate total size
                result.TotalSize = result.CleanableItems.Sum(item => item.Size);
                result.ItemCount = result.CleanableItems.Count;

                _loggingService.LogInfo($"Scan completed. Found {result.ItemCount} items totaling {FormatBytes(result.TotalSize)}");
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Error during scan: {ex.Message}", ex);
                throw;
            }

            return result;
        }

        public async Task<CleaningResult> CleanSelectedItemsAsync(IEnumerable<CleanableItem> items, IProgress<CleaningProgress> progress, CancellationToken cancellationToken)
        {
            var result = new CleaningResult();
            var itemsList = items.ToList();
            var totalItems = itemsList.Count;
            var processedItems = 0;

            try
            {
                foreach (var item in itemsList)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    progress?.Report(new CleaningProgress 
                    { 
                        ProcessedItems = processedItems, 
                        TotalItems = totalItems, 
                        CurrentItem = item.Name,
                        CurrentOperation = $"Cleaning {item.Category}..."
                    });

                    try
                    {
                        await CleanItemAsync(item, cancellationToken);
                        result.CleanedItems.Add(item);
                        result.TotalSizeFreed += item.Size;
                    }
                    catch (Exception ex)
                    {
                        _loggingService.LogError($"Error cleaning item {item.Name}: {ex.Message}", ex);
                        result.FailedItems.Add(new FailedCleaningItem { Item = item, Error = ex.Message });
                    }

                    processedItems++;
                }

                result.Success = result.FailedItems.Count == 0;
                _loggingService.LogInfo($"Cleaning completed. Freed {FormatBytes(result.TotalSizeFreed)} from {result.CleanedItems.Count} items");
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Error during cleaning: {ex.Message}", ex);
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        private async Task<List<CleanableItem>> ScanTemporaryFilesAsync(CancellationToken cancellationToken)
        {
            var items = new List<CleanableItem>();
            var tempPaths = new[]
            {
                Path.GetTempPath(),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp")
            };

            foreach (var tempPath in tempPaths.Where(Directory.Exists))
            {
                try
                {
                    var files = Directory.GetFiles(tempPath, "*", SearchOption.AllDirectories);
                    foreach (var file in files)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        
                        var fileInfo = new FileInfo(file);
                        if (fileInfo.Exists && CanDeleteFile(fileInfo))
                        {
                            items.Add(new CleanableItem
                            {
                                Name = fileInfo.Name,
                                Path = fileInfo.FullName,
                                Size = fileInfo.Length,
                                Category = "Temporary Files",
                                LastModified = fileInfo.LastWriteTime,
                                IsSelected = true
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    _loggingService.LogWarning($"Could not scan temp path {tempPath}: {ex.Message}");
                }
            }

            return items;
        }

        private async Task<List<CleanableItem>> ScanSystemCacheAsync(CancellationToken cancellationToken)
        {
            var items = new List<CleanableItem>();
            var cachePaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Explorer", "thumbcache_*.db"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "config", "systemprofile", "AppData", "Local", "Microsoft", "Windows", "WebCache"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Prefetch")
            };

            // Add cache scanning logic here
            return items;
        }

        private async Task<List<CleanableItem>> ScanLogFilesAsync(CancellationToken cancellationToken)
        {
            var items = new List<CleanableItem>();
            var logPaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Logs"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Debug"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Minidump")
            };

            // Add log file scanning logic here
            return items;
        }

        private async Task<List<CleanableItem>> ScanRecycleBinAsync(CancellationToken cancellationToken)
        {
            var items = new List<CleanableItem>();
            // Add recycle bin scanning logic here
            return items;
        }

        private async Task<List<CleanableItem>> ScanRegistryAsync(CancellationToken cancellationToken)
        {
            var items = new List<CleanableItem>();
            // Add registry scanning logic here
            return items;
        }

        private async Task CleanItemAsync(CleanableItem item, CancellationToken cancellationToken)
        {
            switch (item.Category)
            {
                case "Temporary Files":
                case "System Cache":
                case "Log Files":
                    await DeleteFileAsync(item.Path, cancellationToken);
                    break;
                case "Registry":
                    await CleanRegistryItemAsync(item, cancellationToken);
                    break;
                case "Recycle Bin":
                    await EmptyRecycleBinAsync(cancellationToken);
                    break;
            }
        }

        private async Task DeleteFileAsync(string filePath, CancellationToken cancellationToken)
        {
            await Task.Run(() =>
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
                else if (Directory.Exists(filePath))
                {
                    Directory.Delete(filePath, true);
                }
            }, cancellationToken);
        }

        private async Task CleanRegistryItemAsync(CleanableItem item, CancellationToken cancellationToken)
        {
            // Registry cleaning logic
            await Task.CompletedTask;
        }

        private async Task EmptyRecycleBinAsync(CancellationToken cancellationToken)
        {
            // Recycle bin emptying logic
            await Task.CompletedTask;
        }

        public async Task<long> CalculateSizeAsync(IEnumerable<string> paths)
        {
            return await Task.Run(() =>
            {
                long totalSize = 0;
                foreach (var path in paths)
                {
                    try
                    {
                        if (File.Exists(path))
                        {
                            totalSize += new FileInfo(path).Length;
                        }
                        else if (Directory.Exists(path))
                        {
                            totalSize += CalculateDirectorySize(path);
                        }
                    }
                    catch (Exception ex)
                    {
                        _loggingService.LogWarning($"Could not calculate size for {path}: {ex.Message}");
                    }
                }
                return totalSize;
            });
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

        private bool CanDeleteFile(FileInfo fileInfo)
        {
            try
            {
                // Check if file is older than 24 hours and not in use
                return fileInfo.LastAccessTime < DateTime.Now.AddDays(-1) && !IsFileInUse(fileInfo.FullName);
            }
            catch
            {
                return false;
            }
        }

        private bool IsFileInUse(string filePath)
        {
            try
            {
                using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    return false;
                }
            }
            catch
            {
                return true;
            }
        }

        private int GetTotalScanSteps(ScanOptions options)
        {
            int steps = 0;
            if (options.ScanTemporaryFiles) steps++;
            if (options.ScanSystemCache) steps++;
            if (options.ScanLogFiles) steps++;
            if (options.ScanRecycleBin) steps++;
            if (options.ScanRegistry) steps++;
            return steps;
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
    }
}
