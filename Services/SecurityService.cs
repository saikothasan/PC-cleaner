using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WindowsPCCleaner.Models;

namespace WindowsPCCleaner.Services
{
    public interface ISecurityService
    {
        Task<List<CleanableItem>> ScanPrivacyFilesAsync(PrivacyScanOptions options, IProgress<ScanProgress> progress, CancellationToken cancellationToken);
        Task<SecurityScanResult> PerformSecurityScanAsync(IProgress<ScanProgress> progress, CancellationToken cancellationToken);
        void SecureDeleteFile(string filePath);
        Task SecureDeleteFilesAsync(IEnumerable<string> filePaths, IProgress<CleaningProgress> progress, CancellationToken cancellationToken);
        string CalculateFileHash(string filePath);
        bool IsFileSigned(string filePath);
        Task<List<SuspiciousFile>> FindSuspiciousFilesAsync(string path, IProgress<ScanProgress> progress, CancellationToken cancellationToken);
        Task<List<PrivacyRisk>> AssessPrivacyRisksAsync();
        Task<bool> CreateSecureBackupAsync(string sourcePath, string backupPath);
    }

    public class SecurityService : ISecurityService
    {
        private readonly ILoggingService _loggingService;
        
        private readonly Dictionary<string, PrivacyFileType> _privacyFilePatterns = new()
        {
            // Browser data
            ["*cookies*"] = PrivacyFileType.Cookies,
            ["*history*"] = PrivacyFileType.BrowsingHistory,
            ["*cache*"] = PrivacyFileType.Cache,
            ["*login data*"] = PrivacyFileType.SavedPasswords,
            ["*web data*"] = PrivacyFileType.FormData,
            
            // System privacy files
            ["*recent*"] = PrivacyFileType.RecentFiles,
            ["*mru*"] = PrivacyFileType.RecentFiles,
            ["*prefetch*"] = PrivacyFileType.SystemLogs,
            ["*.log"] = PrivacyFileType.SystemLogs,
            ["*temp*"] = PrivacyFileType.TemporaryFiles,
            ["*tmp*"] = PrivacyFileType.TemporaryFiles,
            
            // User data
            ["*documents*"] = PrivacyFileType.PersonalFiles,
            ["*pictures*"] = PrivacyFileType.PersonalFiles,
            ["*downloads*"] = PrivacyFileType.PersonalFiles,
            ["*desktop*"] = PrivacyFileType.PersonalFiles,
            
            // Application data
            ["*appdata*"] = PrivacyFileType.ApplicationData,
            ["*roaming*"] = PrivacyFileType.ApplicationData,
            ["*local*"] = PrivacyFileType.ApplicationData
        };

        private readonly string[] _suspiciousExtensions = 
        {
            ".exe", ".scr", ".bat", ".cmd", ".com", ".pif", ".vbs", ".js", ".jar", ".ps1"
        };

        private readonly string[] _knownMalwareSignatures = 
        {
            "trojan", "virus", "malware", "spyware", "adware", "rootkit", "keylogger", "backdoor"
        };

        public SecurityService(ILoggingService loggingService)
        {
            _loggingService = loggingService;
        }

        public async Task<List<CleanableItem>> ScanPrivacyFilesAsync(PrivacyScanOptions options, IProgress<ScanProgress> progress, CancellationToken cancellationToken)
        {
            var items = new List<CleanableItem>();

            try
            {
                var scanAreas = GetPrivacyScanAreas(options);
                var totalAreas = scanAreas.Count;
                var currentArea = 0;

                foreach (var area in scanAreas)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    progress?.Report(new ScanProgress
                    {
                        CurrentStep = currentArea + 1,
                        TotalSteps = totalAreas,
                        CurrentOperation = $"Scanning {area.Name}..."
                    });

                    try
                    {
                        var areaItems = await ScanPrivacyAreaAsync(area, cancellationToken);
                        items.AddRange(areaItems);
                    }
                    catch (Exception ex)
                    {
                        _loggingService.LogError($"Error scanning privacy area {area.Name}: {ex.Message}", ex);
                    }

                    currentArea++;
                }

                _loggingService.LogInfo($"Privacy scan completed. Found {items.Count} privacy-related items.");
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Error during privacy scan: {ex.Message}", ex);
                throw;
            }

            return items;
        }

        public async Task<SecurityScanResult> PerformSecurityScanAsync(IProgress<ScanProgress> progress, CancellationToken cancellationToken)
        {
            var result = new SecurityScanResult
            {
                ScanTime = DateTime.Now
            };

            try
            {
                progress?.Report(new ScanProgress { CurrentOperation = "Performing security scan..." });

                // Scan for suspicious files
                progress?.Report(new ScanProgress { CurrentOperation = "Scanning for suspicious files..." });
                result.SuspiciousFiles = await FindSuspiciousFilesAsync(Environment.GetFolderPath(Environment.SpecialFolder.System), progress, cancellationToken);

                // Assess privacy risks
                progress?.Report(new ScanProgress { CurrentOperation = "Assessing privacy risks..." });
                result.PrivacyRisks = await AssessPrivacyRisksAsync();

                // Check for unsigned executables in startup
                progress?.Report(new ScanProgress { CurrentOperation = "Checking startup security..." });
                result.UnsignedStartupItems = await FindUnsignedStartupItemsAsync();

                // Check for suspicious network connections
                progress?.Report(new ScanProgress { CurrentOperation = "Checking network connections..." });
                result.SuspiciousConnections = await FindSuspiciousNetworkConnectionsAsync();

                // Calculate overall security score
                result.SecurityScore = CalculateSecurityScore(result);

                _loggingService.LogInfo($"Security scan completed. Security score: {result.SecurityScore}/100");
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Error during security scan: {ex.Message}", ex);
                result.SecurityScore = 0;
                result.Errors.Add(ex.Message);
            }

            return result;
        }

        public void SecureDeleteFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    return;

                var fileInfo = new FileInfo(filePath);
                var fileSize = fileInfo.Length;

                // DoD 5220.22-M standard: 3-pass overwrite
                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Write, FileShare.None);
                
                // Pass 1: Write zeros
                OverwriteFile(fileStream, 0x00, fileSize);
                
                // Pass 2: Write ones
                OverwriteFile(fileStream, 0xFF, fileSize);
                
                // Pass 3: Write random data
                var random = new Random();
                var buffer = new byte[4096];
                fileStream.Seek(0, SeekOrigin.Begin);
                
                for (long written = 0; written < fileSize; written += buffer.Length)
                {
                    var bytesToWrite = (int)Math.Min(buffer.Length, fileSize - written);
                    random.NextBytes(buffer);
                    fileStream.Write(buffer, 0, bytesToWrite);
                }
                
                fileStream.Flush();
                fileStream.Close();

                // Delete the file
                File.Delete(filePath);

                _loggingService.LogInfo($"Securely deleted file: {filePath}");
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Error securely deleting file {filePath}: {ex.Message}", ex);
                throw;
            }
        }

        public async Task SecureDeleteFilesAsync(IEnumerable<string> filePaths, IProgress<CleaningProgress> progress, CancellationToken cancellationToken)
        {
            var fileList = filePaths.ToList();
            var totalFiles = fileList.Count;
            var processedFiles = 0;

            await Task.Run(() =>
            {
                foreach (var filePath in fileList)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    progress?.Report(new CleaningProgress
                    {
                        ProcessedItems = processedFiles,
                        TotalItems = totalFiles,
                        CurrentItem = Path.GetFileName(filePath),
                        CurrentOperation = "Securely deleting files..."
                    });

                    try
                    {
                        SecureDeleteFile(filePath);
                    }
                    catch (Exception ex)
                    {
                        _loggingService.LogError($"Error securely deleting {filePath}: {ex.Message}", ex);
                    }

                    processedFiles++;
                }
            }, cancellationToken);
        }

        public string CalculateFileHash(string filePath)
        {
            try
            {
                using var sha256 = SHA256.Create();
                using var fileStream = File.OpenRead(filePath);
                var hashBytes = sha256.ComputeHash(fileStream);
                return Convert.ToHexString(hashBytes);
            }
            catch (Exception ex)
            {
                _loggingService.LogWarning($"Could not calculate hash for {filePath}: {ex.Message}");
                return "";
            }
        }

        public bool IsFileSigned(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    return false;

                var certificate = X509Certificate.CreateFromSignedFile(filePath);
                return certificate != null;
            }
            catch
            {
                return false;
            }
        }

        public async Task<List<SuspiciousFile>> FindSuspiciousFilesAsync(string path, IProgress<ScanProgress> progress, CancellationToken cancellationToken)
        {
            var suspiciousFiles = new List<SuspiciousFile>();

            await Task.Run(() =>
            {
                try
                {
                    progress?.Report(new ScanProgress { CurrentOperation = "Scanning for suspicious files..." });

                    var files = Directory.GetFiles(path, "*", SearchOption.AllDirectories)
                        .Where(file => _suspiciousExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
                        .ToArray();

                    var totalFiles = files.Length;
                    var processedFiles = 0;

                    foreach (var file in files)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (processedFiles % 100 == 0)
                        {
                            progress?.Report(new ScanProgress
                            {
                                ProcessedFiles = processedFiles,
                                CurrentOperation = $"Analyzed {processedFiles:N0} of {totalFiles:N0} files..."
                            });
                        }

                        try
                        {
                            var suspiciousFile = AnalyzeFileForSuspiciousActivity(file);
                            if (suspiciousFile != null)
                            {
                                suspiciousFiles.Add(suspiciousFile);
                            }
                        }
                        catch (Exception ex)
                        {
                            _loggingService.LogWarning($"Could not analyze file {file}: {ex.Message}");
                        }

                        processedFiles++;
                    }

                    _loggingService.LogInfo($"Found {suspiciousFiles.Count} suspicious files");
                }
                catch (Exception ex)
                {
                    _loggingService.LogError($"Error finding suspicious files: {ex.Message}", ex);
                    throw;
                }
            }, cancellationToken);

            return suspiciousFiles.OrderByDescending(f => f.RiskLevel).ToList();
        }

        public async Task<List<PrivacyRisk>> AssessPrivacyRisksAsync()
        {
            var risks = new List<PrivacyRisk>();

            await Task.Run(() =>
            {
                try
                {
                    // Check for browser data
                    var browserDataRisk = AssessBrowserDataRisk();
                    if (browserDataRisk != null) risks.Add(browserDataRisk);

                    // Check for system logs
                    var systemLogsRisk = AssessSystemLogsRisk();
                    if (systemLogsRisk != null) risks.Add(systemLogsRisk);

                    // Check for recent files
                    var recentFilesRisk = AssessRecentFilesRisk();
                    if (recentFilesRisk != null) risks.Add(recentFilesRisk);

                    // Check for temporary files
                    var tempFilesRisk = AssessTempFilesRisk();
                    if (tempFilesRisk != null) risks.Add(tempFilesRisk);

                    // Check for network history
                    var networkRisk = AssessNetworkHistoryRisk();
                    if (networkRisk != null) risks.Add(networkRisk);

                    _loggingService.LogInfo($"Identified {risks.Count} privacy risks");
                }
                catch (Exception ex)
                {
                    _loggingService.LogError($"Error assessing privacy risks: {ex.Message}", ex);
                }
            });

            return risks;
        }

        public async Task<bool> CreateSecureBackupAsync(string sourcePath, string backupPath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(backupPath));

                    if (File.Exists(sourcePath))
                    {
                        File.Copy(sourcePath, backupPath, true);
                        
                        // Verify backup integrity
                        var sourceHash = CalculateFileHash(sourcePath);
                        var backupHash = CalculateFileHash(backupPath);
                        
                        if (sourceHash == backupHash)
                        {
                            _loggingService.LogInfo($"Secure backup created: {backupPath}");
                            return true;
                        }
                        else
                        {
                            _loggingService.LogError("Backup verification failed - hash mismatch");
                            File.Delete(backupPath);
                            return false;
                        }
                    }
                    else if (Directory.Exists(sourcePath))
                    {
                        CopyDirectory(sourcePath, backupPath);
                        _loggingService.LogInfo($"Secure directory backup created: {backupPath}");
                        return true;
                    }

                    return false;
                }
                catch (Exception ex)
                {
                    _loggingService.LogError($"Error creating secure backup: {ex.Message}", ex);
                    return false;
                }
            });
        }

        private List<PrivacyScanArea> GetPrivacyScanAreas(PrivacyScanOptions options)
        {
            var areas = new List<PrivacyScanArea>();

            if (options.ScanBrowserData)
            {
                areas.Add(new PrivacyScanArea
                {
                    Name = "Browser Data",
                    Paths = new[]
                    {
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Google", "Chrome"),
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mozilla", "Firefox"),
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Edge")
                    },
                    FileTypes = new[] { PrivacyFileType.Cookies, PrivacyFileType.BrowsingHistory, PrivacyFileType.Cache }
                });
            }

            if (options.ScanSystemLogs)
            {
                areas.Add(new PrivacyScanArea
                {
                    Name = "System Logs",
                    Paths = new[]
                    {
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Logs"),
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp"),
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Prefetch")
                    },
                    FileTypes = new[] { PrivacyFileType.SystemLogs, PrivacyFileType.TemporaryFiles }
                });
            }

            if (options.ScanRecentFiles)
            {
                areas.Add(new PrivacyScanArea
                {
                    Name = "Recent Files",
                    Paths = new[]
                    {
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "Windows", "Recent"),
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Explorer")
                    },
                    FileTypes = new[] { PrivacyFileType.RecentFiles }
                });
            }

            if (options.ScanTemporaryFiles)
            {
                areas.Add(new PrivacyScanArea
                {
                    Name = "Temporary Files",
                    Paths = new[]
                    {
                        Path.GetTempPath(),
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp")
                    },
                    FileTypes = new[] { PrivacyFileType.TemporaryFiles }
                });
            }

            return areas;
        }

        private async Task<List<CleanableItem>> ScanPrivacyAreaAsync(PrivacyScanArea area, CancellationToken cancellationToken)
        {
            var items = new List<CleanableItem>();

            await Task.Run(() =>
            {
                foreach (var path in area.Paths)
                {
                    if (!Directory.Exists(path)) continue;

                    try
                    {
                        var files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
                        
                        foreach (var file in files)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            try
                            {
                                var fileType = DeterminePrivacyFileType(file);
                                if (area.FileTypes.Contains(fileType))
                                {
                                    var fileInfo = new FileInfo(file);
                                    items.Add(new CleanableItem
                                    {
                                        Name = $"Privacy File: {fileInfo.Name}",
                                        Path = file,
                                        Size = fileInfo.Length,
                                        Category = $"Privacy - {fileType}",
                                        Risk = GetPrivacyRiskLevel(fileType),
                                        Description = GetPrivacyFileDescription(fileType),
                                        Icon = GetPrivacyFileIcon(fileType),
                                        IsSelected = GetPrivacyRiskLevel(fileType) == CleaningRisk.Safe
                                    });
                                }
                            }
                            catch (Exception ex)
                            {
                                _loggingService.LogWarning($"Could not process privacy file {file}: {ex.Message}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _loggingService.LogWarning($"Could not scan privacy path {path}: {ex.Message}");
                    }
                }
            }, cancellationToken);

            return items;
        }

        private SuspiciousFile AnalyzeFileForSuspiciousActivity(string filePath)
        {
            try
            {
                var fileInfo = new FileInfo(filePath);
                var fileName = fileInfo.Name.ToLowerInvariant();
                var suspiciousFile = new SuspiciousFile
                {
                    Path = filePath,
                    Name = fileInfo.Name,
                    Size = fileInfo.Length,
                    LastModified = fileInfo.LastWriteTime,
                    RiskLevel = SecurityRisk.Low,
                    Reasons = new List<string>()
                };

                // Check for malware signatures in filename
                if (_knownMalwareSignatures.Any(signature => fileName.Contains(signature)))
                {
                    suspiciousFile.RiskLevel = SecurityRisk.High;
                    suspiciousFile.Reasons.Add("Filename contains malware signature");
                }

                // Check if file is unsigned
                if (!IsFileSigned(filePath))
                {
                    suspiciousFile.RiskLevel = Math.Max(suspiciousFile.RiskLevel, SecurityRisk.Medium);
                    suspiciousFile.Reasons.Add("File is not digitally signed");
                }

                // Check file location
                var systemPaths = new[]
                {
                    Environment.GetFolderPath(Environment.SpecialFolder.System),
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
                };

                var isInSystemPath = systemPaths.Any(path => filePath.StartsWith(path, StringComparison.OrdinalIgnoreCase));
                if (!isInSystemPath)
                {
                    suspiciousFile.RiskLevel = Math.Max(suspiciousFile.RiskLevel, SecurityRisk.Medium);
                    suspiciousFile.Reasons.Add("File located outside system directories");
                }

                // Check file size (very small or very large executables can be suspicious)
                if (fileInfo.Length < 1024) // Less than 1KB
                {
                    suspiciousFile.RiskLevel = Math.Max(suspiciousFile.RiskLevel, SecurityRisk.Medium);
                    suspiciousFile.Reasons.Add("Unusually small executable file");
                }
                else if (fileInfo.Length > 100 * 1024 * 1024) // Greater than 100MB
                {
                    suspiciousFile.RiskLevel = Math.Max(suspiciousFile.RiskLevel, SecurityRisk.Medium);
                    suspiciousFile.Reasons.Add("Unusually large executable file");
                }

                // Check creation/modification time (recently created files in system areas)
                if (isInSystemPath && fileInfo.CreationTime > DateTime.Now.AddDays(-7))
                {
                    suspiciousFile.RiskLevel = Math.Max(suspiciousFile.RiskLevel, SecurityRisk.Medium);
                    suspiciousFile.Reasons.Add("Recently created file in system directory");
                }

                // Only return if there are suspicious indicators
                return suspiciousFile.Reasons.Count > 0 ? suspiciousFile : null;
            }
            catch (Exception ex)
            {
                _loggingService.LogWarning($"Error analyzing file {filePath}: {ex.Message}");
                return null;
            }
        }

        private async Task<List<string>> FindUnsignedStartupItemsAsync()
        {
            var unsignedItems = new List<string>();

            await Task.Run(() =>
            {
                try
                {
                    // This would integrate with the StartupManagerService
                    // For now, we'll do a basic check of common startup locations
                    var startupPaths = new[]
                    {
                        Environment.GetFolderPath(Environment.SpecialFolder.Startup),
                        Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup)
                    };

                    foreach (var path in startupPaths)
                    {
                        if (Directory.Exists(path))
                        {
                            var files = Directory.GetFiles(path, "*.exe", SearchOption.TopDirectoryOnly);
                            foreach (var file in files)
                            {
                                if (!IsFileSigned(file))
                                {
                                    unsignedItems.Add(file);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _loggingService.LogError($"Error finding unsigned startup items: {ex.Message}", ex);
                }
            });

            return unsignedItems;
        }

        private async Task<List<string>> FindSuspiciousNetworkConnectionsAsync()
        {
            var suspiciousConnections = new List<string>();

            await Task.Run(() =>
            {
                try
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "netstat",
                        Arguments = "-an",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    };

                    using var process = Process.Start(startInfo);
                    if (process != null)
                    {
                        var output = process.StandardOutput.ReadToEnd();
                        process.WaitForExit();

                        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                        foreach (var line in lines)
                        {
                            // Look for connections to suspicious ports or addresses
                            if (line.Contains("ESTABLISHED") && 
                                (line.Contains(":6667") || line.Contains(":1337") || line.Contains(":31337")))
                            {
                                suspiciousConnections.Add(line.Trim());
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _loggingService.LogError($"Error finding suspicious network connections: {ex.Message}", ex);
                }
            });

            return suspiciousConnections;
        }

        private int CalculateSecurityScore(SecurityScanResult result)
        {
            var score = 100;

            // Deduct points for suspicious files
            score -= Math.Min(result.SuspiciousFiles.Count * 5, 30);

            // Deduct points for privacy risks
            score -= Math.Min(result.PrivacyRisks.Count * 3, 20);

            // Deduct points for unsigned startup items
            score -= Math.Min(result.UnsignedStartupItems.Count * 2, 15);

            // Deduct points for suspicious connections
            score -= Math.Min(result.SuspiciousConnections.Count * 10, 25);

            return Math.Max(score, 0);
        }

        private PrivacyRisk AssessBrowserDataRisk()
        {
            try
            {
                var browserPaths = new[]
                {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Google", "Chrome"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mozilla", "Firefox"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Edge")
                };

                var totalSize = 0L;
                var fileCount = 0;

                foreach (var path in browserPaths)
                {
                    if (Directory.Exists(path))
                    {
                        var files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
                        fileCount += files.Length;
                        totalSize += files.Sum(f => new FileInfo(f).Length);
                    }
                }

                if (fileCount > 0)
                {
                    return new PrivacyRisk
                    {
                        Type = PrivacyRiskType.BrowserData,
                        Severity = totalSize > 100 * 1024 * 1024 ? RiskSeverity.High : RiskSeverity.Medium,
                        Description = $"Browser data contains {fileCount:N0} files ({FormatBytes(totalSize)})",
                        Recommendation = "Clear browser data regularly to protect privacy",
                        AffectedFiles = fileCount
                    };
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogWarning($"Error assessing browser data risk: {ex.Message}");
            }

            return null;
        }

        private PrivacyRisk AssessSystemLogsRisk()
        {
            try
            {
                var logPaths = new[]
                {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Logs"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp")
                };

                var totalSize = 0L;
                var fileCount = 0;

                foreach (var path in logPaths)
                {
                    if (Directory.Exists(path))
                    {
                        var files = Directory.GetFiles(path, "*.log", SearchOption.AllDirectories);
                        fileCount += files.Length;
                        totalSize += files.Sum(f => new FileInfo(f).Length);
                    }
                }

                if (fileCount > 100)
                {
                    return new PrivacyRisk
                    {
                        Type = PrivacyRiskType.SystemLogs,
                        Severity = RiskSeverity.Medium,
                        Description = $"System contains {fileCount:N0} log files ({FormatBytes(totalSize)})",
                        Recommendation = "Clean old log files to free space and protect privacy",
                        AffectedFiles = fileCount
                    };
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogWarning($"Error assessing system logs risk: {ex.Message}");
            }

            return null;
        }

        private PrivacyRisk AssessRecentFilesRisk()
        {
            try
            {
                var recentPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
                    "Microsoft", "Windows", "Recent");

                if (Directory.Exists(recentPath))
                {
                    var files = Directory.GetFiles(recentPath, "*", SearchOption.AllDirectories);
                    if (files.Length > 50)
                    {
                        return new PrivacyRisk
                        {
                            Type = PrivacyRiskType.RecentFiles,
                            Severity = RiskSeverity.Low,
                            Description = $"Recent files list contains {files.Length:N0} entries",
                            Recommendation = "Clear recent files list to protect privacy",
                            AffectedFiles = files.Length
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogWarning($"Error assessing recent files risk: {ex.Message}");
            }

            return null;
        }

        private PrivacyRisk AssessTempFilesRisk()
        {
            try
            {
                var tempPath = Path.GetTempPath();
                if (Directory.Exists(tempPath))
                {
                    var files = Directory.GetFiles(tempPath, "*", SearchOption.AllDirectories);
                    var totalSize = files.Sum(f => new FileInfo(f).Length);

                    if (totalSize > 500 * 1024 * 1024) // > 500MB
                    {
                        return new PrivacyRisk
                        {
                            Type = PrivacyRiskType.TemporaryFiles,
                            Severity = RiskSeverity.Medium,
                            Description = $"Temporary files folder contains {FormatBytes(totalSize)} of data",
                            Recommendation = "Clean temporary files to free space and protect privacy",
                            AffectedFiles = files.Length
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogWarning($"Error assessing temp files risk: {ex.Message}");
            }

            return null;
        }

        private PrivacyRisk AssessNetworkHistoryRisk()
        {
            try
            {
                // Check for network history in registry
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\TypedPaths");
                if (key != null)
                {
                    var valueCount = key.GetValueNames().Length;
                    if (valueCount > 10)
                    {
                        return new PrivacyRisk
                        {
                            Type = PrivacyRiskType.NetworkHistory,
                            Severity = RiskSeverity.Low,
                            Description = $"Network history contains {valueCount} entries",
                            Recommendation = "Clear network history to protect privacy",
                            AffectedFiles = valueCount
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogWarning($"Error assessing network history risk: {ex.Message}");
            }

            return null;
        }

        private void OverwriteFile(FileStream fileStream, byte value, long fileSize)
        {
            var buffer = new byte[4096];
            Array.Fill(buffer, value);
            
            fileStream.Seek(0, SeekOrigin.Begin);
            for (long written = 0; written < fileSize; written += buffer.Length)
            {
                var bytesToWrite = (int)Math.Min(buffer.Length, fileSize - written);
                fileStream.Write(buffer, 0, bytesToWrite);
            }
            fileStream.Flush();
        }

        private void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var destFile = Path.Combine(destDir, Path.GetFileName(file));
                File.Copy(file, destFile, true);
            }

            foreach (var subDir in Directory.GetDirectories(sourceDir))
            {
                var destSubDir = Path.Combine(destDir, Path.GetFileName(subDir));
                CopyDirectory(subDir, destSubDir);
            }
        }

        private PrivacyFileType DeterminePrivacyFileType(string filePath)
        {
            var fileName = Path.GetFileName(filePath).ToLowerInvariant();
            var directory = Path.GetDirectoryName(filePath).ToLowerInvariant();

            foreach (var pattern in _privacyFilePatterns)
            {
                var searchPattern = pattern.Key.Replace("*", "");
                if (fileName.Contains(searchPattern) || directory.Contains(searchPattern))
                {
                    return pattern.Value;
                }
            }

            return PrivacyFileType.ApplicationData;
        }

        private CleaningRisk GetPrivacyRiskLevel(PrivacyFileType fileType)
        {
            return fileType switch
            {
                PrivacyFileType.SavedPasswords => CleaningRisk.High,
                PrivacyFileType.PersonalFiles => CleaningRisk.High,
                PrivacyFileType.Cookies => CleaningRisk.Medium,
                PrivacyFileType.FormData => CleaningRisk.Medium,
                PrivacyFileType.BrowsingHistory => CleaningRisk.Low,
                PrivacyFileType.Cache => CleaningRisk.Safe,
                PrivacyFileType.TemporaryFiles => CleaningRisk.Safe,
                PrivacyFileType.SystemLogs => CleaningRisk.Safe,
                PrivacyFileType.RecentFiles => CleaningRisk.Safe,
                _ => CleaningRisk.Low
            };
        }

        private string GetPrivacyFileDescription(PrivacyFileType fileType)
        {
            return fileType switch
            {
                PrivacyFileType.SavedPasswords => "Saved passwords - WARNING: You will lose all saved passwords!",
                PrivacyFileType.PersonalFiles => "Personal files - Review carefully before deletion",
                PrivacyFileType.Cookies => "Website cookies - You may need to log in to websites again",
                PrivacyFileType.FormData => "Saved form data - Auto-fill information will be lost",
                PrivacyFileType.BrowsingHistory => "Web browsing history",
                PrivacyFileType.Cache => "Temporary cache files",
                PrivacyFileType.TemporaryFiles => "Temporary files",
                PrivacyFileType.SystemLogs => "System log files",
                PrivacyFileType.RecentFiles => "Recent files history",
                PrivacyFileType.ApplicationData => "Application data files",
                _ => "Privacy-related file"
            };
        }

        private string GetPrivacyFileIcon(PrivacyFileType fileType)
        {
            return fileType switch
            {
                PrivacyFileType.SavedPasswords => "🔐",
                PrivacyFileType.PersonalFiles => "📁",
                PrivacyFileType.Cookies => "🍪",
                PrivacyFileType.FormData => "📝",
                PrivacyFileType.BrowsingHistory => "📚",
                PrivacyFileType.Cache => "💾",
                PrivacyFileType.TemporaryFiles => "🗂️",
                PrivacyFileType.SystemLogs => "📋",
                PrivacyFileType.RecentFiles => "📄",
                PrivacyFileType.ApplicationData => "⚙️",
                _ => "🔒"
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
    }
}
