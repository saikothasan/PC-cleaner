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
    public interface IRegistryService
    {
        Task<List<CleanableItem>> ScanRegistryAsync(RegistryScanOptions options, IProgress<ScanProgress> progress, CancellationToken cancellationToken);
        Task<RegistryCleaningResult> CleanRegistryItemsAsync(List<CleanableItem> items, IProgress<CleaningProgress> progress, CancellationToken cancellationToken);
        Task<bool> CreateRegistryBackupAsync(string backupPath);
        Task<bool> RestoreRegistryBackupAsync(string backupPath);
        Task<List<RegistryIssue>> ValidateRegistryIntegrityAsync();
    }

    public class RegistryService : IRegistryService
    {
        private readonly ILoggingService _loggingService;
        private readonly ISecurityService _securityService;

        private readonly Dictionary<RegistryHive, string> _hiveNames = new()
        {
            [RegistryHive.ClassesRoot] = "HKEY_CLASSES_ROOT",
            [RegistryHive.CurrentUser] = "HKEY_CURRENT_USER",
            [RegistryHive.LocalMachine] = "HKEY_LOCAL_MACHINE",
            [RegistryHive.Users] = "HKEY_USERS",
            [RegistryHive.CurrentConfig] = "HKEY_CURRENT_CONFIG"
        };

        private readonly List<RegistryScanArea> _scanAreas = new()
        {
            new RegistryScanArea
            {
                Name = "Uninstall Entries",
                Description = "Invalid software uninstall entries",
                Hive = RegistryHive.LocalMachine,
                KeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                Risk = CleaningRisk.Low,
                ScanType = RegistryScanType.UninstallEntries
            },
            new RegistryScanArea
            {
                Name = "Startup Programs",
                Description = "Invalid startup program entries",
                Hive = RegistryHive.LocalMachine,
                KeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
                Risk = CleaningRisk.Medium,
                ScanType = RegistryScanType.StartupEntries
            },
            new RegistryScanArea
            {
                Name = "File Associations",
                Description = "Invalid file type associations",
                Hive = RegistryHive.ClassesRoot,
                KeyPath = "",
                Risk = CleaningRisk.Medium,
                ScanType = RegistryScanType.FileAssociations
            },
            new RegistryScanArea
            {
                Name = "COM Objects",
                Description = "Invalid COM/ActiveX objects",
                Hive = RegistryHive.ClassesRoot,
                KeyPath = @"CLSID",
                Risk = CleaningRisk.High,
                ScanType = RegistryScanType.ComObjects
            },
            new RegistryScanArea
            {
                Name = "Shared DLLs",
                Description = "Invalid shared library references",
                Hive = RegistryHive.LocalMachine,
                KeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\SharedDLLs",
                Risk = CleaningRisk.Medium,
                ScanType = RegistryScanType.SharedDlls
            },
            new RegistryScanArea
            {
                Name = "Recent Documents",
                Description = "Recent document history",
                Hive = RegistryHive.CurrentUser,
                KeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\RecentDocs",
                Risk = CleaningRisk.Safe,
                ScanType = RegistryScanType.RecentDocuments
            },
            new RegistryScanArea
            {
                Name = "MRU Lists",
                Description = "Most Recently Used lists",
                Hive = RegistryHive.CurrentUser,
                KeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer",
                Risk = CleaningRisk.Safe,
                ScanType = RegistryScanType.MruLists
            }
        };

        public RegistryService(ILoggingService loggingService, ISecurityService securityService)
        {
            _loggingService = loggingService;
            _securityService = securityService;
        }

        public async Task<List<CleanableItem>> ScanRegistryAsync(RegistryScanOptions options, IProgress<ScanProgress> progress, CancellationToken cancellationToken)
        {
            var items = new List<CleanableItem>();
            var scanAreas = _scanAreas.Where(area => ShouldScanArea(area, options)).ToList();
            var totalAreas = scanAreas.Count;
            var currentArea = 0;

            // Create registry backup before scanning
            var backupPath = Path.Combine(Path.GetTempPath(), $"RegistryBackup_{DateTime.Now:yyyyMMdd_HHmmss}.reg");
            await CreateRegistryBackupAsync(backupPath);

            foreach (var scanArea in scanAreas)
            {
                cancellationToken.ThrowIfCancellationRequested();

                progress?.Report(new ScanProgress
                {
                    CurrentStep = currentArea + 1,
                    TotalSteps = totalAreas,
                    CurrentOperation = $"Scanning {scanArea.Name}..."
                });

                try
                {
                    var areaItems = await ScanRegistryAreaAsync(scanArea, cancellationToken);
                    items.AddRange(areaItems);
                }
                catch (Exception ex)
                {
                    _loggingService.LogError($"Error scanning registry area {scanArea.Name}: {ex.Message}", ex);
                }

                currentArea++;
            }

            _loggingService.LogInfo($"Registry scan completed. Found {items.Count} issues.");
            return items;
        }

        public async Task<RegistryCleaningResult> CleanRegistryItemsAsync(List<CleanableItem> items, IProgress<CleaningProgress> progress, CancellationToken cancellationToken)
        {
            var result = new RegistryCleaningResult();
            var totalItems = items.Count;
            var processedItems = 0;

            // Create backup before cleaning
            var backupPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), 
                "PC Cleaner Backups", $"Registry_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.reg");
            
            Directory.CreateDirectory(Path.GetDirectoryName(backupPath));
            
            if (await CreateRegistryBackupAsync(backupPath))
            {
                result.BackupPath = backupPath;
            }

            foreach (var item in items)
            {
                cancellationToken.ThrowIfCancellationRequested();

                progress?.Report(new CleaningProgress
                {
                    ProcessedItems = processedItems,
                    TotalItems = totalItems,
                    CurrentItem = item.Name,
                    CurrentOperation = "Cleaning registry entries..."
                });

                try
                {
                    await CleanRegistryItemAsync(item, cancellationToken);
                    result.CleanedItems.Add(item);
                }
                catch (Exception ex)
                {
                    _loggingService.LogError($"Error cleaning registry item {item.Name}: {ex.Message}", ex);
                    result.FailedItems.Add(new FailedCleaningItem { Item = item, Error = ex.Message });
                }

                processedItems++;
            }

            result.Success = result.FailedItems.Count == 0;
            return result;
        }

        public async Task<bool> CreateRegistryBackupAsync(string backupPath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var startInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "reg",
                        Arguments = $"export HKLM \"{backupPath}\" /y",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };

                    using var process = System.Diagnostics.Process.Start(startInfo);
                    process?.WaitForExit();

                    var success = process?.ExitCode == 0 && File.Exists(backupPath);
                    
                    if (success)
                    {
                        _loggingService.LogInfo($"Registry backup created: {backupPath}");
                    }
                    else
                    {
                        _loggingService.LogError("Failed to create registry backup");
                    }

                    return success;
                }
                catch (Exception ex)
                {
                    _loggingService.LogError($"Error creating registry backup: {ex.Message}", ex);
                    return false;
                }
            });
        }

        public async Task<bool> RestoreRegistryBackupAsync(string backupPath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (!File.Exists(backupPath))
                    {
                        _loggingService.LogError($"Backup file not found: {backupPath}");
                        return false;
                    }

                    var startInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "reg",
                        Arguments = $"import \"{backupPath}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };

                    using var process = System.Diagnostics.Process.Start(startInfo);
                    process?.WaitForExit();

                    var success = process?.ExitCode == 0;
                    
                    if (success)
                    {
                        _loggingService.LogInfo($"Registry restored from backup: {backupPath}");
                    }
                    else
                    {
                        _loggingService.LogError("Failed to restore registry backup");
                    }

                    return success;
                }
                catch (Exception ex)
                {
                    _loggingService.LogError($"Error restoring registry backup: {ex.Message}", ex);
                    return false;
                }
            });
        }

        public async Task<List<RegistryIssue>> ValidateRegistryIntegrityAsync()
        {
            var issues = new List<RegistryIssue>();

            await Task.Run(() =>
            {
                try
                {
                    // Run system file checker to validate registry integrity
                    var startInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "sfc",
                        Arguments = "/verifyonly",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };

                    using var process = System.Diagnostics.Process.Start(startInfo);
                    process?.WaitForExit();

                    if (process?.ExitCode != 0)
                    {
                        issues.Add(new RegistryIssue
                        {
                            Type = RegistryIssueType.IntegrityViolation,
                            Description = "System file integrity issues detected",
                            Severity = IssueSeverity.High,
                            Recommendation = "Run 'sfc /scannow' as administrator"
                        });
                    }
                }
                catch (Exception ex)
                {
                    _loggingService.LogError($"Error validating registry integrity: {ex.Message}", ex);
                    issues.Add(new RegistryIssue
                    {
                        Type = RegistryIssueType.ValidationError,
                        Description = $"Could not validate registry integrity: {ex.Message}",
                        Severity = IssueSeverity.Medium
                    });
                }
            });

            return issues;
        }

        private async Task<List<CleanableItem>> ScanRegistryAreaAsync(RegistryScanArea scanArea, CancellationToken cancellationToken)
        {
            var items = new List<CleanableItem>();

            await Task.Run(() =>
            {
                try
                {
                    switch (scanArea.ScanType)
                    {
                        case RegistryScanType.UninstallEntries:
                            items.AddRange(ScanUninstallEntries(scanArea));
                            break;
                        case RegistryScanType.StartupEntries:
                            items.AddRange(ScanStartupEntries(scanArea));
                            break;
                        case RegistryScanType.FileAssociations:
                            items.AddRange(ScanFileAssociations(scanArea));
                            break;
                        case RegistryScanType.ComObjects:
                            items.AddRange(ScanComObjects(scanArea));
                            break;
                        case RegistryScanType.SharedDlls:
                            items.AddRange(ScanSharedDlls(scanArea));
                            break;
                        case RegistryScanType.RecentDocuments:
                            items.AddRange(ScanRecentDocuments(scanArea));
                            break;
                        case RegistryScanType.MruLists:
                            items.AddRange(ScanMruLists(scanArea));
                            break;
                    }
                }
                catch (Exception ex)
                {
                    _loggingService.LogError($"Error scanning {scanArea.Name}: {ex.Message}", ex);
                }
            }, cancellationToken);

            return items;
        }

        private List<CleanableItem> ScanUninstallEntries(RegistryScanArea scanArea)
        {
            var items = new List<CleanableItem>();

            try
            {
                using var key = GetRegistryKey(scanArea.Hive, scanArea.KeyPath);
                if (key == null) return items;

                foreach (var subKeyName in key.GetSubKeyNames())
                {
                    try
                    {
                        using var subKey = key.OpenSubKey(subKeyName);
                        if (subKey == null) continue;

                        var displayName = subKey.GetValue("DisplayName")?.ToString();
                        var uninstallString = subKey.GetValue("UninstallString")?.ToString();

                        if (!string.IsNullOrEmpty(displayName) && !string.IsNullOrEmpty(uninstallString))
                        {
                            // Check if the uninstaller exists
                            var uninstallerPath = ExtractExecutablePath(uninstallString);
                            if (!string.IsNullOrEmpty(uninstallerPath) && !File.Exists(uninstallerPath))
                            {
                                items.Add(new CleanableItem
                                {
                                    Name = $"Invalid Uninstall Entry: {displayName}",
                                    Path = $"{_hiveNames[scanArea.Hive]}\\{scanArea.KeyPath}\\{subKeyName}",
                                    Size = EstimateRegistryKeySize(subKey),
                                    Category = "Registry - Uninstall Entries",
                                    Risk = scanArea.Risk,
                                    Description = $"Uninstaller not found: {uninstallerPath}",
                                    Icon = "🗑️",
                                    IsSelected = scanArea.Risk == CleaningRisk.Safe
                                });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _loggingService.LogWarning($"Error scanning uninstall entry {subKeyName}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Error scanning uninstall entries: {ex.Message}", ex);
            }

            return items;
        }

        private List<CleanableItem> ScanStartupEntries(RegistryScanArea scanArea)
        {
            var items = new List<CleanableItem>();

            try
            {
                var startupKeys = new[]
                {
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce",
                    @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run",
                    @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\RunOnce"
                };

                foreach (var keyPath in startupKeys)
                {
                    using var key = GetRegistryKey(RegistryHive.LocalMachine, keyPath);
                    if (key == null) continue;

                    foreach (var valueName in key.GetValueNames())
                    {
                        try
                        {
                            var value = key.GetValue(valueName)?.ToString();
                            if (!string.IsNullOrEmpty(value))
                            {
                                var executablePath = ExtractExecutablePath(value);
                                if (!string.IsNullOrEmpty(executablePath) && !File.Exists(executablePath))
                                {
                                    items.Add(new CleanableItem
                                    {
                                        Name = $"Invalid Startup Entry: {valueName}",
                                        Path = $"HKEY_LOCAL_MACHINE\\{keyPath}",
                                        Size = value.Length * 2, // Approximate size
                                        Category = "Registry - Startup Entries",
                                        Risk = CleaningRisk.Medium,
                                        Description = $"Startup program not found: {executablePath}",
                                        Icon = "🚀",
                                        IsSelected = false
                                    });
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _loggingService.LogWarning($"Error scanning startup entry {valueName}: {ex.Message}");
                        }
                    }
                }

                // Also check current user startup entries
                using var userKey = GetRegistryKey(RegistryHive.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run");
                if (userKey != null)
                {
                    foreach (var valueName in userKey.GetValueNames())
                    {
                        try
                        {
                            var value = userKey.GetValue(valueName)?.ToString();
                            if (!string.IsNullOrEmpty(value))
                            {
                                var executablePath = ExtractExecutablePath(value);
                                if (!string.IsNullOrEmpty(executablePath) && !File.Exists(executablePath))
                                {
                                    items.Add(new CleanableItem
                                    {
                                        Name = $"Invalid User Startup Entry: {valueName}",
                                        Path = $"HKEY_CURRENT_USER\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run",
                                        Size = value.Length * 2,
                                        Category = "Registry - Startup Entries",
                                        Risk = CleaningRisk.Medium,
                                        Description = $"User startup program not found: {executablePath}",
                                        Icon = "🚀",
                                        IsSelected = false
                                    });
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _loggingService.LogWarning($"Error scanning user startup entry {valueName}: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Error scanning startup entries: {ex.Message}", ex);
            }

            return items;
        }

        private List<CleanableItem> ScanFileAssociations(RegistryScanArea scanArea)
        {
            var items = new List<CleanableItem>();

            try
            {
                using var key = GetRegistryKey(scanArea.Hive, "");
                if (key == null) return items;

                foreach (var subKeyName in key.GetSubKeyNames())
                {
                    if (subKeyName.StartsWith(".")) // File extension
                    {
                        try
                        {
                            using var extKey = key.OpenSubKey(subKeyName);
                            if (extKey == null) continue;

                            var progId = extKey.GetValue("")?.ToString();
                            if (!string.IsNullOrEmpty(progId))
                            {
                                using var progIdKey = key.OpenSubKey(progId);
                                if (progIdKey == null)
                                {
                                    items.Add(new CleanableItem
                                    {
                                        Name = $"Broken File Association: {subKeyName}",
                                        Path = $"HKEY_CLASSES_ROOT\\{subKeyName}",
                                        Size = EstimateRegistryKeySize(extKey),
                                        Category = "Registry - File Associations",
                                        Risk = CleaningRisk.Medium,
                                        Description = $"References non-existent program ID: {progId}",
                                        Icon = "📄",
                                        IsSelected = false
                                    });
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _loggingService.LogWarning($"Error scanning file association {subKeyName}: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Error scanning file associations: {ex.Message}", ex);
            }

            return items;
        }

        private List<CleanableItem> ScanComObjects(RegistryScanArea scanArea)
        {
            var items = new List<CleanableItem>();

            try
            {
                using var key = GetRegistryKey(scanArea.Hive, scanArea.KeyPath);
                if (key == null) return items;

                foreach (var clsidName in key.GetSubKeyNames())
                {
                    try
                    {
                        using var clsidKey = key.OpenSubKey(clsidName);
                        if (clsidKey == null) continue;

                        using var inprocKey = clsidKey.OpenSubKey("InprocServer32");
                        if (inprocKey != null)
                        {
                            var dllPath = inprocKey.GetValue("")?.ToString();
                            if (!string.IsNullOrEmpty(dllPath) && !File.Exists(dllPath))
                            {
                                var friendlyName = clsidKey.GetValue("")?.ToString() ?? clsidName;
                                items.Add(new CleanableItem
                                {
                                    Name = $"Invalid COM Object: {friendlyName}",
                                    Path = $"HKEY_CLASSES_ROOT\\CLSID\\{clsidName}",
                                    Size = EstimateRegistryKeySize(clsidKey),
                                    Category = "Registry - COM Objects",
                                    Risk = CleaningRisk.High,
                                    Description = $"DLL not found: {dllPath}",
                                    Icon = "🔧",
                                    IsSelected = false
                                });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _loggingService.LogWarning($"Error scanning COM object {clsidName}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Error scanning COM objects: {ex.Message}", ex);
            }

            return items;
        }

        private List<CleanableItem> ScanSharedDlls(RegistryScanArea scanArea)
        {
            var items = new List<CleanableItem>();

            try
            {
                using var key = GetRegistryKey(scanArea.Hive, scanArea.KeyPath);
                if (key == null) return items;

                foreach (var valueName in key.GetValueNames())
                {
                    try
                    {
                        if (!File.Exists(valueName))
                        {
                            var refCount = key.GetValue(valueName);
                            items.Add(new CleanableItem
                            {
                                Name = $"Invalid Shared DLL: {Path.GetFileName(valueName)}",
                                Path = $"HKEY_LOCAL_MACHINE\\{scanArea.KeyPath}",
                                Size = valueName.Length * 2,
                                Category = "Registry - Shared DLLs",
                                Risk = CleaningRisk.Medium,
                                Description = $"DLL not found: {valueName} (Ref count: {refCount})",
                                Icon = "📚",
                                IsSelected = false
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        _loggingService.LogWarning($"Error scanning shared DLL {valueName}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Error scanning shared DLLs: {ex.Message}", ex);
            }

            return items;
        }

        private List<CleanableItem> ScanRecentDocuments(RegistryScanArea scanArea)
        {
            var items = new List<CleanableItem>();

            try
            {
                using var key = GetRegistryKey(scanArea.Hive, scanArea.KeyPath);
                if (key == null) return items;

                var totalSize = EstimateRegistryKeySize(key);
                if (totalSize > 0)
                {
                    items.Add(new CleanableItem
                    {
                        Name = "Recent Documents History",
                        Path = $"HKEY_CURRENT_USER\\{scanArea.KeyPath}",
                        Size = totalSize,
                        Category = "Registry - Recent Documents",
                        Risk = CleaningRisk.Safe,
                        Description = "Recently accessed documents list",
                        Icon = "📋",
                        IsSelected = true
                    });
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Error scanning recent documents: {ex.Message}", ex);
            }

            return items;
        }

        private List<CleanableItem> ScanMruLists(RegistryScanArea scanArea)
        {
            var items = new List<CleanableItem>();

            try
            {
                var mruPaths = new[]
                {
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\ComDlg32\OpenSavePidlMRU",
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\ComDlg32\LastVisitedPidlMRU",
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\RunMRU",
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\TypedPaths"
                };

                foreach (var mruPath in mruPaths)
                {
                    using var key = GetRegistryKey(RegistryHive.CurrentUser, mruPath);
                    if (key != null)
                    {
                        var size = EstimateRegistryKeySize(key);
                        if (size > 0)
                        {
                            items.Add(new CleanableItem
                            {
                                Name = $"MRU List: {Path.GetFileName(mruPath)}",
                                Path = $"HKEY_CURRENT_USER\\{mruPath}",
                                Size = size,
                                Category = "Registry - MRU Lists",
                                Risk = CleaningRisk.Safe,
                                Description = "Most Recently Used items list",
                                Icon = "📝",
                                IsSelected = true
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Error scanning MRU lists: {ex.Message}", ex);
            }

            return items;
        }

        private async Task CleanRegistryItemAsync(CleanableItem item, CancellationToken cancellationToken)
        {
            await Task.Run(() =>
            {
                try
                {
                    var pathParts = item.Path.Split('\\');
                    if (pathParts.Length < 2) return;

                    var hiveName = pathParts[0];
                    var keyPath = string.Join("\\", pathParts.Skip(1));

                    var hive = _hiveNames.FirstOrDefault(kvp => kvp.Value == hiveName).Key;
                    
                    using var key = GetRegistryKey(hive, keyPath, true);
                    if (key != null)
                    {
                        // For safety, we'll delete the key's values rather than the key itself
                        // unless it's explicitly safe to delete the entire key
                        if (item.Risk == CleaningRisk.Safe)
                        {
                            foreach (var valueName in key.GetValueNames())
                            {
                                key.DeleteValue(valueName, false);
                            }
                        }
                        else
                        {
                            // For higher risk items, we need more specific handling
                            // This would be implemented based on the specific type of registry item
                        }
                    }
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Failed to clean registry item {item.Name}: {ex.Message}", ex);
                }
            }, cancellationToken);
        }

        private RegistryKey GetRegistryKey(RegistryHive hive, string keyPath, bool writable = false)
        {
            try
            {
                var baseKey = hive switch
                {
                    RegistryHive.ClassesRoot => Registry.ClassesRoot,
                    RegistryHive.CurrentUser => Registry.CurrentUser,
                    RegistryHive.LocalMachine => Registry.LocalMachine,
                    RegistryHive.Users => Registry.Users,
                    RegistryHive.CurrentConfig => Registry.CurrentConfig,
                    _ => null
                };

                if (baseKey == null) return null;

                return string.IsNullOrEmpty(keyPath) 
                    ? baseKey 
                    : baseKey.OpenSubKey(keyPath, writable);
            }
            catch (Exception ex)
            {
                _loggingService.LogWarning($"Could not open registry key {hive}\\{keyPath}: {ex.Message}");
                return null;
            }
        }

        private bool ShouldScanArea(RegistryScanArea area, RegistryScanOptions options)
        {
            return area.ScanType switch
            {
                RegistryScanType.UninstallEntries => options.ScanUninstallEntries,
                RegistryScanType.StartupEntries => options.ScanStartupEntries,
                RegistryScanType.FileAssociations => options.ScanFileAssociations,
                RegistryScanType.ComObjects => options.ScanComObjects,
                RegistryScanType.SharedDlls => options.ScanSharedDlls,
                RegistryScanType.RecentDocuments => options.ScanRecentDocuments,
                RegistryScanType.MruLists => options.ScanMruLists,
                _ => false
            };
        }

        private string ExtractExecutablePath(string commandLine)
        {
            if (string.IsNullOrEmpty(commandLine)) return null;

            // Handle quoted paths
            if (commandLine.StartsWith("\""))
            {
                var endQuote = commandLine.IndexOf("\"", 1);
                if (endQuote > 0)
                {
                    return commandLine.Substring(1, endQuote - 1);
                }
            }

            // Handle unquoted paths
            var spaceIndex = commandLine.IndexOf(" ");
            return spaceIndex > 0 ? commandLine.Substring(0, spaceIndex) : commandLine;
        }

        private long EstimateRegistryKeySize(RegistryKey key)
        {
            try
            {
                long size = 0;
                
                // Estimate size based on value names and data
                foreach (var valueName in key.GetValueNames())
                {
                    size += valueName.Length * 2; // Unicode
                    var value = key.GetValue(valueName);
                    if (value != null)
                    {
                        size += value.ToString().Length * 2;
                    }
                }

                // Add size for subkey names
                foreach (var subKeyName in key.GetSubKeyNames())
                {
                    size += subKeyName.Length * 2;
                }

                return size;
            }
            catch
            {
                return 100; // Default estimate
            }
        }
    }
}
