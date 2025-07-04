using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Threading;
using System.Threading.Tasks;
using WindowsPCCleaner.Models;

namespace WindowsPCCleaner.Services
{
    public interface IDiskAnalysisService
    {
        Task<List<DiskInfo>> GetDiskInfoAsync();
        Task<DiskAnalysisResult> AnalyzeDiskUsageAsync(string drivePath, IProgress<ScanProgress> progress, CancellationToken cancellationToken);
        Task<List<LargeFile>> FindLargeFilesAsync(string path, long minimumSize, IProgress<ScanProgress> progress, CancellationToken cancellationToken);
        Task<List<DuplicateFileGroup>> FindDuplicateFilesAsync(string path, IProgress<ScanProgress> progress, CancellationToken cancellationToken);
        Task<List<EmptyFolder>> FindEmptyFoldersAsync(string path, IProgress<ScanProgress> progress, CancellationToken cancellationToken);
        Task<DiskHealthInfo> GetDiskHealthAsync(string driveLetter);
    }

    public class DiskAnalysisService : IDiskAnalysisService
    {
        private readonly ILoggingService _loggingService;
        private readonly ISecurityService _securityService;

        public DiskAnalysisService(ILoggingService loggingService, ISecurityService securityService)
        {
            _loggingService = loggingService;
            _securityService = securityService;
        }

        public async Task<List<DiskInfo>> GetDiskInfoAsync()
        {
            var diskInfos = new List<DiskInfo>();

            await Task.Run(() =>
            {
                try
                {
                    var drives = DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed);

                    foreach (var drive in drives)
                    {
                        try
                        {
                            var diskInfo = new DiskInfo
                            {
                                DriveLetter = drive.Name,
                                Label = drive.VolumeLabel,
                                FileSystem = drive.DriveFormat,
                                TotalSpace = drive.TotalSize,
                                FreeSpace = drive.AvailableFreeSpace,
                                UsedSpace = drive.TotalSize - drive.AvailableFreeSpace,
                                HealthStatus = GetDriveHealthStatus(drive.Name)
                            };

                            diskInfos.Add(diskInfo);
                        }
                        catch (Exception ex)
                        {
                            _loggingService.LogWarning($"Could not get info for drive {drive.Name}: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _loggingService.LogError($"Error getting disk information: {ex.Message}", ex);
                }
            });

            return diskInfos;
        }

        public async Task<DiskAnalysisResult> AnalyzeDiskUsageAsync(string drivePath, IProgress<ScanProgress> progress, CancellationToken cancellationToken)
        {
            var result = new DiskAnalysisResult
            {
                DrivePath = drivePath,
                AnalysisTime = DateTime.Now
            };

            try
            {
                progress?.Report(new ScanProgress { CurrentOperation = "Analyzing disk usage..." });

                var rootDirectories = Directory.GetDirectories(drivePath);
                var totalDirectories = rootDirectories.Length;
                var currentDirectory = 0;

                foreach (var directory in rootDirectories)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var dirName = Path.GetFileName(directory);
                    progress?.Report(new ScanProgress
                    {
                        CurrentStep = currentDirectory + 1,
                        TotalSteps = totalDirectories,
                        CurrentOperation = $"Analyzing {dirName}..."
                    });

                    try
                    {
                        var size = await CalculateDirectorySizeAsync(directory, cancellationToken);
                        var fileCount = await CountFilesInDirectoryAsync(directory, cancellationToken);

                        result.DirectoryUsage.Add(new DirectoryUsage
                        {
                            Path = directory,
                            Name = dirName,
                            Size = size,
                            FileCount = fileCount,
                            Percentage = 0 // Will be calculated after all directories are processed
                        });

                        result.TotalSize += size;
                        result.TotalFiles += fileCount;
                    }
                    catch (Exception ex)
                    {
                        _loggingService.LogWarning($"Could not analyze directory {directory}: {ex.Message}");
                    }

                    currentDirectory++;
                }

                // Calculate percentages
                foreach (var usage in result.DirectoryUsage)
                {
                    usage.Percentage = result.TotalSize > 0 ? (double)usage.Size / result.TotalSize * 100 : 0;
                }

                // Sort by size descending
                result.DirectoryUsage = result.DirectoryUsage.OrderByDescending(d => d.Size).ToList();

                // Identify largest directories
                result.LargestDirectories = result.DirectoryUsage.Take(10).ToList();

                _loggingService.LogInfo($"Disk analysis completed for {drivePath}. Total size: {FormatBytes(result.TotalSize)}, Files: {result.TotalFiles:N0}");
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Error analyzing disk usage: {ex.Message}", ex);
                throw;
            }

            return result;
        }

        public async Task<List<LargeFile>> FindLargeFilesAsync(string path, long minimumSize, IProgress<ScanProgress> progress, CancellationToken cancellationToken)
        {
            var largeFiles = new List<LargeFile>();

            await Task.Run(() =>
            {
                try
                {
                    progress?.Report(new ScanProgress { CurrentOperation = "Searching for large files..." });

                    var files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
                    var totalFiles = files.Length;
                    var processedFiles = 0;

                    foreach (var file in files)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (processedFiles % 1000 == 0)
                        {
                            progress?.Report(new ScanProgress
                            {
                                ProcessedFiles = processedFiles,
                                CurrentOperation = $"Processed {processedFiles:N0} of {totalFiles:N0} files..."
                            });
                        }

                        try
                        {
                            var fileInfo = new FileInfo(file);
                            if (fileInfo.Length >= minimumSize)
                            {
                                largeFiles.Add(new LargeFile
                                {
                                    Path = file,
                                    Name = fileInfo.Name,
                                    Size = fileInfo.Length,
                                    LastModified = fileInfo.LastWriteTime,
                                    LastAccessed = fileInfo.LastAccessTime,
                                    Extension = fileInfo.Extension,
                                    Directory = fileInfo.DirectoryName,
                                    IsReadOnly = fileInfo.IsReadOnly,
                                    Attributes = fileInfo.Attributes
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            _loggingService.LogWarning($"Could not process file {file}: {ex.Message}");
                        }

                        processedFiles++;
                    }

                    // Sort by size descending
                    largeFiles = largeFiles.OrderByDescending(f => f.Size).ToList();

                    _loggingService.LogInfo($"Found {largeFiles.Count:N0} large files (>= {FormatBytes(minimumSize)})");
                }
                catch (Exception ex)
                {
                    _loggingService.LogError($"Error finding large files: {ex.Message}", ex);
                    throw;
                }
            }, cancellationToken);

            return largeFiles;
        }

        public async Task<List<DuplicateFileGroup>> FindDuplicateFilesAsync(string path, IProgress<ScanProgress> progress, CancellationToken cancellationToken)
        {
            var duplicateGroups = new List<DuplicateFileGroup>();

            await Task.Run(() =>
            {
                try
                {
                    progress?.Report(new ScanProgress { CurrentOperation = "Scanning for duplicate files..." });

                    var files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
                    var totalFiles = files.Length;
                    var processedFiles = 0;

                    // Group files by size first (quick filter)
                    var sizeGroups = new Dictionary<long, List<string>>();

                    foreach (var file in files)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (processedFiles % 1000 == 0)
                        {
                            progress?.Report(new ScanProgress
                            {
                                ProcessedFiles = processedFiles,
                                CurrentOperation = $"Grouping files by size: {processedFiles:N0} of {totalFiles:N0}..."
                            });
                        }

                        try
                        {
                            var fileInfo = new FileInfo(file);
                            if (fileInfo.Length > 0) // Skip empty files
                            {
                                if (!sizeGroups.ContainsKey(fileInfo.Length))
                                {
                                    sizeGroups[fileInfo.Length] = new List<string>();
                                }
                                sizeGroups[fileInfo.Length].Add(file);
                            }
                        }
                        catch (Exception ex)
                        {
                            _loggingService.LogWarning($"Could not process file {file}: {ex.Message}");
                        }

                        processedFiles++;
                    }

                    // Now check files with same size for actual duplicates using hash comparison
                    var potentialDuplicates = sizeGroups.Where(kvp => kvp.Value.Count > 1).ToList();
                    var groupsProcessed = 0;

                    foreach (var sizeGroup in potentialDuplicates)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        progress?.Report(new ScanProgress
                        {
                            CurrentStep = groupsProcessed + 1,
                            TotalSteps = potentialDuplicates.Count,
                            CurrentOperation = $"Comparing files of size {FormatBytes(sizeGroup.Key)}..."
                        });

                        var hashGroups = new Dictionary<string, List<string>>();

                        foreach (var file in sizeGroup.Value)
                        {
                            try
                            {
                                var hash = _securityService.CalculateFileHash(file);
                                if (!hashGroups.ContainsKey(hash))
                                {
                                    hashGroups[hash] = new List<string>();
                                }
                                hashGroups[hash].Add(file);
                            }
                            catch (Exception ex)
                            {
                                _loggingService.LogWarning($"Could not calculate hash for {file}: {ex.Message}");
                            }
                        }

                        // Add groups with actual duplicates
                        foreach (var hashGroup in hashGroups.Where(kvp => kvp.Value.Count > 1))
                        {
                            var duplicateFiles = hashGroup.Value.Select(filePath => new DuplicateFile
                            {
                                Path = filePath,
                                Name = Path.GetFileName(filePath),
                                Size = sizeGroup.Key,
                                LastModified = File.GetLastWriteTime(filePath),
                                Directory = Path.GetDirectoryName(filePath)
                            }).ToList();

                            duplicateGroups.Add(new DuplicateFileGroup
                            {
                                Hash = hashGroup.Key,
                                Size = sizeGroup.Key,
                                Files = duplicateFiles,
                                TotalWastedSpace = sizeGroup.Key * (duplicateFiles.Count - 1) // Keep one, delete others
                            });
                        }

                        groupsProcessed++;
                    }

                    // Sort by wasted space descending
                    duplicateGroups = duplicateGroups.OrderByDescending(g => g.TotalWastedSpace).ToList();

                    var totalWastedSpace = duplicateGroups.Sum(g => g.TotalWastedSpace);
                    _loggingService.LogInfo($"Found {duplicateGroups.Count:N0} duplicate file groups, wasting {FormatBytes(totalWastedSpace)}");
                }
                catch (Exception ex)
                {
                    _loggingService.LogError($"Error finding duplicate files: {ex.Message}", ex);
                    throw;
                }
            }, cancellationToken);

            return duplicateGroups;
        }

        public async Task<List<EmptyFolder>> FindEmptyFoldersAsync(string path, IProgress<ScanProgress> progress, CancellationToken cancellationToken)
        {
            var emptyFolders = new List<EmptyFolder>();

            await Task.Run(() =>
            {
                try
                {
                    progress?.Report(new ScanProgress { CurrentOperation = "Searching for empty folders..." });

                    var directories = Directory.GetDirectories(path, "*", SearchOption.AllDirectories);
                    var totalDirectories = directories.Length;
                    var processedDirectories = 0;

                    // Process directories in reverse order (deepest first) to handle nested empty folders
                    var sortedDirectories = directories.OrderByDescending(d => d.Length).ToArray();

                    foreach (var directory in sortedDirectories)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (processedDirectories % 100 == 0)
                        {
                            progress?.Report(new ScanProgress
                            {
                                ProcessedFiles = processedDirectories,
                                CurrentOperation = $"Checked {processedDirectories:N0} of {totalDirectories:N0} folders..."
                            });
                        }

                        try
                        {
                            if (IsDirectoryEmpty(directory))
                            {
                                var dirInfo = new DirectoryInfo(directory);
                                emptyFolders.Add(new EmptyFolder
                                {
                                    Path = directory,
                                    Name = dirInfo.Name,
                                    ParentPath = dirInfo.Parent?.FullName,
                                    CreationTime = dirInfo.CreationTime,
                                    LastModified = dirInfo.LastWriteTime,
                                    Attributes = dirInfo.Attributes,
                                    IsHidden = (dirInfo.Attributes & FileAttributes.Hidden) != 0,
                                    IsSystem = (dirInfo.Attributes & FileAttributes.System) != 0
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            _loggingService.LogWarning($"Could not check directory {directory}: {ex.Message}");
                        }

                        processedDirectories++;
                    }

                    _loggingService.LogInfo($"Found {emptyFolders.Count:N0} empty folders");
                }
                catch (Exception ex)
                {
                    _loggingService.LogError($"Error finding empty folders: {ex.Message}", ex);
                    throw;
                }
            }, cancellationToken);

            return emptyFolders;
        }

        public async Task<DiskHealthInfo> GetDiskHealthAsync(string driveLetter)
        {
            var healthInfo = new DiskHealthInfo
            {
                DriveLetter = driveLetter,
                CheckTime = DateTime.Now
            };

            await Task.Run(() =>
            {
                try
                {
                    // Get SMART data using WMI
                    var query = $"SELECT * FROM Win32_DiskDrive WHERE DeviceID LIKE '%{driveLetter.TrimEnd(':')}%'";
                    using var searcher = new ManagementObjectSearcher(query);
                    using var results = searcher.Get();

                    foreach (ManagementObject drive in results)
                    {
                        try
                        {
                            healthInfo.Model = drive["Model"]?.ToString();
                            healthInfo.SerialNumber = drive["SerialNumber"]?.ToString();
                            healthInfo.Size = Convert.ToInt64(drive["Size"] ?? 0);
                            healthInfo.InterfaceType = drive["InterfaceType"]?.ToString();

                            // Get SMART status
                            var status = drive["Status"]?.ToString();
                            healthInfo.OverallHealth = status == "OK" ? DiskHealthStatus.Healthy : DiskHealthStatus.Warning;

                            // Get additional SMART attributes
                            var smartQuery = $"SELECT * FROM MSStorageDriver_FailurePredictStatus WHERE InstanceName LIKE '%{drive["PNPDeviceID"]}%'";
                            using var smartSearcher = new ManagementObjectSearcher("root\\wmi", smartQuery);
                            using var smartResults = smartSearcher.Get();

                            foreach (ManagementObject smartData in smartResults)
                            {
                                var predictFailure = Convert.ToBoolean(smartData["PredictFailure"] ?? false);
                                if (predictFailure)
                                {
                                    healthInfo.OverallHealth = DiskHealthStatus.Critical;
                                    healthInfo.Issues.Add("SMART predicts drive failure");
                                }
                            }

                            // Get temperature if available
                            var tempQuery = $"SELECT * FROM MSStorageDriver_FailurePredictData WHERE InstanceName LIKE '%{drive["PNPDeviceID"]}%'";
                            using var tempSearcher = new ManagementObjectSearcher("root\\wmi", tempQuery);
                            using var tempResults = tempSearcher.Get();

                            foreach (ManagementObject tempData in tempResults)
                            {
                                var vendorSpecific = tempData["VendorSpecific"] as byte[];
                                if (vendorSpecific != null && vendorSpecific.Length > 12)
                                {
                                    // Temperature is typically at offset 12 for many drives
                                    healthInfo.Temperature = vendorSpecific[12];
                                    if (healthInfo.Temperature > 60)
                                    {
                                        healthInfo.Issues.Add($"High temperature: {healthInfo.Temperature}°C");
                                        if (healthInfo.OverallHealth == DiskHealthStatus.Healthy)
                                            healthInfo.OverallHealth = DiskHealthStatus.Warning;
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _loggingService.LogWarning($"Could not get detailed health info for drive: {ex.Message}");
                        }
                    }

                    // Check disk space
                    var driveInfo = new DriveInfo(driveLetter);
                    if (driveInfo.IsReady)
                    {
                        var freeSpacePercent = (double)driveInfo.AvailableFreeSpace / driveInfo.TotalSize * 100;
                        healthInfo.FreeSpacePercent = freeSpacePercent;

                        if (freeSpacePercent < 5)
                        {
                            healthInfo.Issues.Add("Very low disk space (< 5%)");
                            healthInfo.OverallHealth = DiskHealthStatus.Critical;
                        }
                        else if (freeSpacePercent < 15)
                        {
                            healthInfo.Issues.Add("Low disk space (< 15%)");
                            if (healthInfo.OverallHealth == DiskHealthStatus.Healthy)
                                healthInfo.OverallHealth = DiskHealthStatus.Warning;
                        }
                    }

                    // Check for file system errors
                    healthInfo.HasFileSystemErrors = CheckForFileSystemErrors(driveLetter);
                    if (healthInfo.HasFileSystemErrors)
                    {
                        healthInfo.Issues.Add("File system errors detected");
                        if (healthInfo.OverallHealth == DiskHealthStatus.Healthy)
                            healthInfo.OverallHealth = DiskHealthStatus.Warning;
                    }

                    // Recommendations based on health status
                    switch (healthInfo.OverallHealth)
                    {
                        case DiskHealthStatus.Warning:
                            healthInfo.Recommendations.Add("Run disk cleanup to free up space");
                            healthInfo.Recommendations.Add("Check disk for errors using chkdsk");
                            break;
                        case DiskHealthStatus.Critical:
                            healthInfo.Recommendations.Add("Backup important data immediately");
                            healthInfo.Recommendations.Add("Consider replacing the drive");
                            healthInfo.Recommendations.Add("Run full disk diagnostics");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    _loggingService.LogError($"Error getting disk health for {driveLetter}: {ex.Message}", ex);
                    healthInfo.OverallHealth = DiskHealthStatus.Unknown;
                    healthInfo.Issues.Add($"Could not determine disk health: {ex.Message}");
                }
            });

            return healthInfo;
        }

        private async Task<long> CalculateDirectorySizeAsync(string directoryPath, CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                try
                {
                    long size = 0;
                    var files = Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories);
                    
                    foreach (var file in files)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        try
                        {
                            size += new FileInfo(file).Length;
                        }
                        catch
                        {
                            // Skip files we can't access
                        }
                    }
                    
                    return size;
                }
                catch
                {
                    return 0;
                }
            }, cancellationToken);
        }

        private async Task<int> CountFilesInDirectoryAsync(string directoryPath, CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                try
                {
                    return Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories).Length;
                }
                catch
                {
                    return 0;
                }
            }, cancellationToken);
        }

        private DriveHealthStatus GetDriveHealthStatus(string driveLetter)
        {
            try
            {
                var driveInfo = new DriveInfo(driveLetter);
                if (!driveInfo.IsReady) return DriveHealthStatus.Unknown;

                var freeSpacePercent = (double)driveInfo.AvailableFreeSpace / driveInfo.TotalSize * 100;
                
                return freeSpacePercent switch
                {
                    < 5 => DriveHealthStatus.Critical,
                    < 15 => DriveHealthStatus.Warning,
                    _ => DriveHealthStatus.Healthy
                };
            }
            catch
            {
                return DriveHealthStatus.Unknown;
            }
        }

        private bool IsDirectoryEmpty(string directoryPath)
        {
            try
            {
                return !Directory.EnumerateFileSystemEntries(directoryPath).Any();
            }
            catch
            {
                return false;
            }
        }

        private bool CheckForFileSystemErrors(string driveLetter)
        {
            try
            {
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "chkdsk",
                    Arguments = $"{driveLetter} /scan",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var process = System.Diagnostics.Process.Start(startInfo);
                process?.WaitForExit(30000); // 30 second timeout

                return process?.ExitCode != 0;
            }
            catch
            {
                return false;
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
    }
}
