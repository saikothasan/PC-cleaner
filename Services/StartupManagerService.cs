using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using WindowsPCCleaner.Models;

namespace WindowsPCCleaner.Services
{
    public interface IStartupManagerService
    {
        Task<List<StartupItem>> GetStartupItemsAsync();
        Task<bool> EnableStartupItemAsync(StartupItem item);
        Task<bool> DisableStartupItemAsync(StartupItem item);
        Task<bool> RemoveStartupItemAsync(StartupItem item);
        Task<StartupImpactAnalysis> AnalyzeStartupImpactAsync();
        Task<List<StartupRecommendation>> GetStartupRecommendationsAsync();
    }

    public class StartupManagerService : IStartupManagerService
    {
        private readonly ILoggingService _loggingService;
        private readonly ISecurityService _securityService;

        private readonly Dictionary<StartupLocation, string[]> _startupLocations = new()
        {
            [StartupLocation.LocalMachineRun] = new[] { @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run" },
            [StartupLocation.LocalMachineRunOnce] = new[] { @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce" },
            [StartupLocation.CurrentUserRun] = new[] { @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run" },
            [StartupLocation.CurrentUserRunOnce] = new[] { @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce" },
            [StartupLocation.StartupFolder] = new[] { Environment.GetFolderPath(Environment.SpecialFolder.Startup) },
            [StartupLocation.CommonStartupFolder] = new[] { Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup) },
            [StartupLocation.TaskScheduler] = new[] { "Task Scheduler" },
            [StartupLocation.Services] = new[] { "Windows Services" }
        };

        public StartupManagerService(ILoggingService loggingService, ISecurityService securityService)
        {
            _loggingService = loggingService;
            _securityService = securityService;
        }

        public async Task<List<StartupItem>> GetStartupItemsAsync()
        {
            var startupItems = new List<StartupItem>();

            await Task.Run(() =>
            {
                try
                {
                    // Get registry startup items
                    startupItems.AddRange(GetRegistryStartupItems());
                    
                    // Get startup folder items
                    startupItems.AddRange(GetStartupFolderItems());
                    
                    // Get scheduled task startup items
                    startupItems.AddRange(GetScheduledTaskStartupItems());
                    
                    // Get service startup items
                    startupItems.AddRange(GetServiceStartupItems());

                    // Calculate startup impact for each item
                    foreach (var item in startupItems)
                    {
                        item.Impact = CalculateStartupImpact(item);
                        item.SecurityRisk = AssessSecurityRisk(item);
                    }

                    _loggingService.LogInfo($"Found {startupItems.Count} startup items");
                }
                catch (Exception ex)
                {
                    _loggingService.LogError($"Error getting startup items: {ex.Message}", ex);
                }
            });

            return startupItems.OrderBy(item => item.Name).ToList();
        }

        public async Task<bool> EnableStartupItemAsync(StartupItem item)
        {
            return await Task.Run(() =>
            {
                try
                {
                    switch (item.Location)
                    {
                        case StartupLocation.LocalMachineRun:
                        case StartupLocation.LocalMachineRunOnce:
                            return EnableRegistryStartupItem(item, RegistryHive.LocalMachine);
                        
                        case StartupLocation.CurrentUserRun:
                        case StartupLocation.CurrentUserRunOnce:
                            return EnableRegistryStartupItem(item, RegistryHive.CurrentUser);
                        
                        case StartupLocation.StartupFolder:
                        case StartupLocation.CommonStartupFolder:
                            return EnableStartupFolderItem(item);
                        
                        case StartupLocation.TaskScheduler:
                            return EnableScheduledTaskItem(item);
                        
                        case StartupLocation.Services:
                            return EnableServiceItem(item);
                        
                        default:
                            return false;
                    }
                }
                catch (Exception ex)
                {
                    _loggingService.LogError($"Error enabling startup item {item.Name}: {ex.Message}", ex);
                    return false;
                }
            });
        }

        public async Task<bool> DisableStartupItemAsync(StartupItem item)
        {
            return await Task.Run(() =>
            {
                try
                {
                    switch (item.Location)
                    {
                        case StartupLocation.LocalMachineRun:
                        case StartupLocation.LocalMachineRunOnce:
                            return DisableRegistryStartupItem(item, RegistryHive.LocalMachine);
                        
                        case StartupLocation.CurrentUserRun:
                        case StartupLocation.CurrentUserRunOnce:
                            return DisableRegistryStartupItem(item, RegistryHive.CurrentUser);
                        
                        case StartupLocation.StartupFolder:
                        case StartupLocation.CommonStartupFolder:
                            return DisableStartupFolderItem(item);
                        
                        case StartupLocation.TaskScheduler:
                            return DisableScheduledTaskItem(item);
                        
                        case StartupLocation.Services:
                            return DisableServiceItem(item);
                        
                        default:
                            return false;
                    }
                }
                catch (Exception ex)
                {
                    _loggingService.LogError($"Error disabling startup item {item.Name}: {ex.Message}", ex);
                    return false;
                }
            });
        }

        public async Task<bool> RemoveStartupItemAsync(StartupItem item)
        {
            return await Task.Run(() =>
            {
                try
                {
                    // Create backup before removal
                    var backupPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), 
                        "PC Cleaner Backups", $"Startup_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                    
                    Directory.CreateDirectory(Path.GetDirectoryName(backupPath));
                    File.WriteAllText(backupPath, $"Removed startup item: {item.Name}\nLocation: {item.Location}\nCommand: {item.Command}\nPath: {item.Path}");

                    switch (item.Location)
                    {
                        case StartupLocation.LocalMachineRun:
                        case StartupLocation.LocalMachineRunOnce:
                            return RemoveRegistryStartupItem(item, RegistryHive.LocalMachine);
                        
                        case StartupLocation.CurrentUserRun:
                        case StartupLocation.CurrentUserRunOnce:
                            return RemoveRegistryStartupItem(item, RegistryHive.CurrentUser);
                        
                        case StartupLocation.StartupFolder:
                        case StartupLocation.CommonStartupFolder:
                            return RemoveStartupFolderItem(item);
                        
                        case StartupLocation.TaskScheduler:
                            return RemoveScheduledTaskItem(item);
                        
                        default:
                            return false; // Don't allow removal of services
                    }
                }
                catch (Exception ex)
                {
                    _loggingService.LogError($"Error removing startup item {item.Name}: {ex.Message}", ex);
                    return false;
                }
            });
        }

        public async Task<StartupImpactAnalysis> AnalyzeStartupImpactAsync()
        {
            var analysis = new StartupImpactAnalysis();

            await Task.Run(() =>
            {
                try
                {
                    var startupItems = GetStartupItemsAsync().Result;
                    
                    analysis.TotalItems = startupItems.Count;
                    analysis.EnabledItems = startupItems.Count(item => item.IsEnabled);
                    analysis.DisabledItems = startupItems.Count(item => !item.IsEnabled);
                    
                    analysis.HighImpactItems = startupItems.Count(item => item.Impact == StartupImpact.High);
                    analysis.MediumImpactItems = startupItems.Count(item => item.Impact == StartupImpact.Medium);
                    analysis.LowImpactItems = startupItems.Count(item => item.Impact == StartupImpact.Low);
                    
                    analysis.SecurityRisks = startupItems.Count(item => item.SecurityRisk != SecurityRisk.Low);
                    
                    // Estimate startup time impact
                    var enabledHighImpact = startupItems.Count(item => item.IsEnabled && item.Impact == StartupImpact.High);
                    var enabledMediumImpact = startupItems.Count(item => item.IsEnabled && item.Impact == StartupImpact.Medium);
                    var enabledLowImpact = startupItems.Count(item => item.IsEnabled && item.Impact == StartupImpact.Low);
                    
                    analysis.EstimatedStartupDelay = (enabledHighImpact * 5) + (enabledMediumImpact * 2) + (enabledLowImpact * 0.5);
                    
                    // Performance score (0-100, higher is better)
                    var maxPossibleDelay = (analysis.TotalItems * 5); // Assume all high impact
                    analysis.PerformanceScore = maxPossibleDelay > 0 
                        ? Math.Max(0, 100 - (int)((analysis.EstimatedStartupDelay / maxPossibleDelay) * 100))
                        : 100;

                    _loggingService.LogInfo($"Startup analysis: {analysis.EnabledItems} enabled items, estimated delay: {analysis.EstimatedStartupDelay:F1}s");
                }
                catch (Exception ex)
                {
                    _loggingService.LogError($"Error analyzing startup impact: {ex.Message}", ex);
                }
            });

            return analysis;
        }

        public async Task<List<StartupRecommendation>> GetStartupRecommendationsAsync()
        {
            var recommendations = new List<StartupRecommendation>();

            await Task.Run(async () =>
            {
                try
                {
                    var startupItems = await GetStartupItemsAsync();
                    var analysis = await AnalyzeStartupImpactAsync();

                    // Recommend disabling high-impact items that aren't essential
                    var highImpactItems = startupItems.Where(item => 
                        item.IsEnabled && 
                        item.Impact == StartupImpact.High && 
                        !IsEssentialStartupItem(item)).ToList();

                    foreach (var item in highImpactItems)
                    {
                        recommendations.Add(new StartupRecommendation
                        {
                            Type = RecommendationType.Disable,
                            Item = item,
                            Reason = "High startup impact - consider disabling to improve boot time",
                            PotentialTimeSaving = 5.0,
                            Priority = RecommendationPriority.High
                        });
                    }

                    // Recommend removing items with security risks
                    var securityRiskItems = startupItems.Where(item => 
                        item.SecurityRisk == SecurityRisk.High).ToList();

                    foreach (var item in securityRiskItems)
                    {
                        recommendations.Add(new StartupRecommendation
                        {
                            Type = RecommendationType.Remove,
                            Item = item,
                            Reason = "Potential security risk - verify legitimacy before keeping",
                            Priority = RecommendationPriority.Critical
                        });
                    }

                    // Recommend disabling duplicate items
                    var duplicateGroups = startupItems
                        .Where(item => item.IsEnabled)
                        .GroupBy(item => GetExecutableName(item.Command))
                        .Where(group => group.Count() > 1)
                        .ToList();

                    foreach (var group in duplicateGroups)
                    {
                        var itemsToDisable = group.Skip(1); // Keep first, disable others
                        foreach (var item in itemsToDisable)
                        {
                            recommendations.Add(new StartupRecommendation
                            {
                                Type = RecommendationType.Disable,
                                Item = item,
                                Reason = "Duplicate startup entry - only one instance needed",
                                Priority = RecommendationPriority.Medium
                            });
                        }
                    }

                    // Overall performance recommendations
                    if (analysis.PerformanceScore < 70)
                    {
                        recommendations.Add(new StartupRecommendation
                        {
                            Type = RecommendationType.General,
                            Reason = $"Startup performance score is {analysis.PerformanceScore}/100. Consider disabling non-essential programs.",
                            Priority = RecommendationPriority.High
                        });
                    }

                    recommendations = recommendations.OrderByDescending(r => r.Priority).ToList();
                    _loggingService.LogInfo($"Generated {recommendations.Count} startup recommendations");
                }
                catch (Exception ex)
                {
                    _loggingService.LogError($"Error generating startup recommendations: {ex.Message}", ex);
                }
            });

            return recommendations;
        }

        private List<StartupItem> GetRegistryStartupItems()
        {
            var items = new List<StartupItem>();

            try
            {
                // Local Machine startup items
                items.AddRange(GetRegistryStartupItemsFromHive(RegistryHive.LocalMachine, 
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", StartupLocation.LocalMachineRun));
                items.AddRange(GetRegistryStartupItemsFromHive(RegistryHive.LocalMachine, 
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce", StartupLocation.LocalMachineRunOnce));

                // Current User startup items
                items.AddRange(GetRegistryStartupItemsFromHive(RegistryHive.CurrentUser, 
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", StartupLocation.CurrentUserRun));
                items.AddRange(GetRegistryStartupItemsFromHive(RegistryHive.CurrentUser, 
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce", StartupLocation.CurrentUserRunOnce));
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Error getting registry startup items: {ex.Message}", ex);
            }

            return items;
        }

        private List<StartupItem> GetRegistryStartupItemsFromHive(RegistryHive hive, string keyPath, StartupLocation location)
        {
            var items = new List<StartupItem>();

            try
            {
                var baseKey = hive == RegistryHive.LocalMachine ? Registry.LocalMachine : Registry.CurrentUser;
                using var key = baseKey.OpenSubKey(keyPath);
                
                if (key != null)
                {
                    foreach (var valueName in key.GetValueNames())
                    {
                        try
                        {
                            var command = key.GetValue(valueName)?.ToString();
                            if (!string.IsNullOrEmpty(command))
                            {
                                var executablePath = ExtractExecutablePath(command);
                                var item = new StartupItem
                                {
                                    Name = valueName,
                                    Command = command,
                                    Path = executablePath,
                                    Location = location,
                                    IsEnabled = true,
                                    Publisher = GetFilePublisher(executablePath),
                                    Description = GetFileDescription(executablePath),
                                    FileExists = File.Exists(executablePath)
                                };

                                items.Add(item);
                            }
                        }
                        catch (Exception ex)
                        {
                            _loggingService.LogWarning($"Error processing registry startup item {valueName}: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Error accessing registry key {keyPath}: {ex.Message}", ex);
            }

            return items;
        }

        private List<StartupItem> GetStartupFolderItems()
        {
            var items = new List<StartupItem>();

            try
            {
                var startupFolders = new[]
                {
                    new { Path = Environment.GetFolderPath(Environment.SpecialFolder.Startup), Location = StartupLocation.StartupFolder },
                    new { Path = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), Location = StartupLocation.CommonStartupFolder }
                };

                foreach (var folder in startupFolders)
                {
                    if (Directory.Exists(folder.Path))
                    {
                        var files = Directory.GetFiles(folder.Path, "*", SearchOption.TopDirectoryOnly);
                        foreach (var file in files)
                        {
                            try
                            {
                                var item = new StartupItem
                                {
                                    Name = Path.GetFileNameWithoutExtension(file),
                                    Command = file,
                                    Path = file,
                                    Location = folder.Location,
                                    IsEnabled = true,
                                    Publisher = GetFilePublisher(file),
                                    Description = GetFileDescription(file),
                                    FileExists = true
                                };

                                items.Add(item);
                            }
                            catch (Exception ex)
                            {
                                _loggingService.LogWarning($"Error processing startup folder item {file}: {ex.Message}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Error getting startup folder items: {ex.Message}", ex);
            }

            return items;
        }

        private List<StartupItem> GetScheduledTaskStartupItems()
        {
            var items = new List<StartupItem>();

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "schtasks",
                    Arguments = "/query /fo csv /v",
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
                    if (lines.Length > 1) // Skip header
                    {
                        for (int i = 1; i < lines.Length; i++)
                        {
                            try
                            {
                                var fields = ParseCsvLine(lines[i]);
                                if (fields.Length > 10)
                                {
                                    var taskName = fields[0].Trim('"');
                                    var status = fields[3].Trim('"');
                                    var trigger = fields[8].Trim('"');
                                    var taskToRun = fields[9].Trim('"');

                                    // Only include tasks that run at startup/logon
                                    if (trigger.Contains("At startup", StringComparison.OrdinalIgnoreCase) ||
                                        trigger.Contains("At logon", StringComparison.OrdinalIgnoreCase))
                                    {
                                        var item = new StartupItem
                                        {
                                            Name = taskName,
                                            Command = taskToRun,
                                            Path = ExtractExecutablePath(taskToRun),
                                            Location = StartupLocation.TaskScheduler,
                                            IsEnabled = status.Equals("Ready", StringComparison.OrdinalIgnoreCase),
                                            Description = $"Scheduled Task: {trigger}",
                                            FileExists = File.Exists(ExtractExecutablePath(taskToRun))
                                        };

                                        items.Add(item);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                _loggingService.LogWarning($"Error parsing scheduled task line: {ex.Message}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Error getting scheduled task startup items: {ex.Message}", ex);
            }

            return items;
        }

        private List<StartupItem> GetServiceStartupItems()
        {
            var items = new List<StartupItem>();

            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Service WHERE StartMode = 'Auto'");
                using var results = searcher.Get();

                foreach (ManagementObject service in results)
                {
                    try
                    {
                        var name = service["Name"]?.ToString();
                        var displayName = service["DisplayName"]?.ToString();
                        var pathName = service["PathName"]?.ToString();
                        var state = service["State"]?.ToString();
                        var description = service["Description"]?.ToString();

                        if (!string.IsNullOrEmpty(name))
                        {
                            var item = new StartupItem
                            {
                                Name = displayName ?? name,
                                Command = pathName ?? "",
                                Path = ExtractExecutablePath(pathName ?? ""),
                                Location = StartupLocation.Services,
                                IsEnabled = state?.Equals("Running", StringComparison.OrdinalIgnoreCase) ?? false,
                                Description = description ?? "Windows Service",
                                FileExists = File.Exists(ExtractExecutablePath(pathName ?? ""))
                            };

                            items.Add(item);
                        }
                    }
                    catch (Exception ex)
                    {
                        _loggingService.LogWarning($"Error processing service startup item: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Error getting service startup items: {ex.Message}", ex);
            }

            return items;
        }

        private StartupImpact CalculateStartupImpact(StartupItem item)
        {
            try
            {
                // Check file size
                if (File.Exists(item.Path))
                {
                    var fileInfo = new FileInfo(item.Path);
                    if (fileInfo.Length > 50 * 1024 * 1024) // > 50MB
                        return StartupImpact.High;
                }

                // Check known high-impact applications
                var highImpactApps = new[]
                {
                    "adobe", "photoshop", "office", "skype", "steam", "origin", "uplay",
                    "antivirus", "mcafee", "norton", "kaspersky", "avast"
                };

                var lowercaseName = item.Name.ToLowerInvariant();
                var lowercasePath = item.Path.ToLowerInvariant();

                if (highImpactApps.Any(app => lowercaseName.Contains(app) || lowercasePath.Contains(app)))
                    return StartupImpact.High;

                // Check system/essential items
                if (IsEssentialStartupItem(item))
                    return StartupImpact.Low;

                // Default to medium impact
                return StartupImpact.Medium;
            }
            catch
            {
                return StartupImpact.Medium;
            }
        }

        private SecurityRisk AssessSecurityRisk(StartupItem item)
        {
            try
            {
                // Check if file exists
                if (!item.FileExists)
                    return SecurityRisk.High;

                // Check file location
                var systemPaths = new[]
                {
                    Environment.GetFolderPath(Environment.SpecialFolder.System),
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
                };

                var isInSystemPath = systemPaths.Any(path => 
                    item.Path.StartsWith(path, StringComparison.OrdinalIgnoreCase));

                if (!isInSystemPath)
                    return SecurityRisk.Medium;

                // Check digital signature
                if (!_securityService.IsFileSigned(item.Path))
                    return SecurityRisk.Medium;

                return SecurityRisk.Low;
            }
            catch
            {
                return SecurityRisk.Medium;
            }
        }

        private bool IsEssentialStartupItem(StartupItem item)
        {
            var essentialItems = new[]
            {
                "windows security", "defender", "audio", "graphics", "network",
                "bluetooth", "touchpad", "synaptics", "realtek", "intel",
                "nvidia", "amd", "microsoft", "windows"
            };

            var lowercaseName = item.Name.ToLowerInvariant();
            var lowercasePublisher = (item.Publisher ?? "").ToLowerInvariant();

            return essentialItems.Any(essential => 
                lowercaseName.Contains(essential) || lowercasePublisher.Contains(essential));
        }

        private bool EnableRegistryStartupItem(StartupItem item, RegistryHive hive)
        {
            // Registry items are enabled by default when they exist
            // This method would restore a previously disabled item
            return true;
        }

        private bool DisableRegistryStartupItem(StartupItem item, RegistryHive hive)
        {
            try
            {
                var keyPath = item.Location == StartupLocation.LocalMachineRunOnce || item.Location == StartupLocation.CurrentUserRunOnce
                    ? @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce"
                    : @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

                var baseKey = hive == RegistryHive.LocalMachine ? Registry.LocalMachine : Registry.CurrentUser;
                using var key = baseKey.OpenSubKey(keyPath, true);
                
                if (key != null)
                {
                    // Move to disabled section
                    var disabledKeyPath = keyPath.Replace("\\Run", "\\Run-Disabled");
                    using var disabledKey = baseKey.CreateSubKey(disabledKeyPath);
                    
                    var value = key.GetValue(item.Name);
                    if (value != null)
                    {
                        disabledKey.SetValue(item.Name, value);
                        key.DeleteValue(item.Name);
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Error disabling registry startup item: {ex.Message}", ex);
            }

            return false;
        }

        private bool RemoveRegistryStartupItem(StartupItem item, RegistryHive hive)
        {
            try
            {
                var keyPath = item.Location == StartupLocation.LocalMachineRunOnce || item.Location == StartupLocation.CurrentUserRunOnce
                    ? @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce"
                    : @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

                var baseKey = hive == RegistryHive.LocalMachine ? Registry.LocalMachine : Registry.CurrentUser;
                using var key = baseKey.OpenSubKey(keyPath, true);
                
                if (key != null)
                {
                    key.DeleteValue(item.Name, false);
                    return true;
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Error removing registry startup item: {ex.Message}", ex);
            }

            return false;
        }

        private bool EnableStartupFolderItem(StartupItem item)
        {
            try
            {
                // If file was renamed to .disabled, rename it back
                var disabledPath = item.Path + ".disabled";
                if (File.Exists(disabledPath))
                {
                    File.Move(disabledPath, item.Path);
                    return true;
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Error enabling startup folder item: {ex.Message}", ex);
            }

            return false;
        }

        private bool DisableStartupFolderItem(StartupItem item)
        {
            try
            {
                if (File.Exists(item.Path))
                {
                    File.Move(item.Path, item.Path + ".disabled");
                    return true;
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Error disabling startup folder item: {ex.Message}", ex);
            }

            return false;
        }

        private bool RemoveStartupFolderItem(StartupItem item)
        {
            try
            {
                if (File.Exists(item.Path))
                {
                    File.Delete(item.Path);
                    return true;
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Error removing startup folder item: {ex.Message}", ex);
            }

            return false;
        }

        private bool EnableScheduledTaskItem(StartupItem item)
        {
            return ExecuteScheduledTaskCommand($"/change /tn \"{item.Name}\" /enable");
        }

        private bool DisableScheduledTaskItem(StartupItem item)
        {
            return ExecuteScheduledTaskCommand($"/change /tn \"{item.Name}\" /disable");
        }

        private bool RemoveScheduledTaskItem(StartupItem item)
        {
            return ExecuteScheduledTaskCommand($"/delete /tn \"{item.Name}\" /f");
        }

        private bool EnableServiceItem(StartupItem item)
        {
            return ExecuteServiceCommand($"config \"{item.Name}\" start= auto");
        }

        private bool DisableServiceItem(StartupItem item)
        {
            return ExecuteServiceCommand($"config \"{item.Name}\" start= disabled");
        }

        private bool ExecuteScheduledTaskCommand(string arguments)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "schtasks",
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var process = Process.Start(startInfo);
                process?.WaitForExit();
                return process?.ExitCode == 0;
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Error executing scheduled task command: {ex.Message}", ex);
                return false;
            }
        }

        private bool ExecuteServiceCommand(string arguments)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "sc",
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var process = Process.Start(startInfo);
                process?.WaitForExit();
                return process?.ExitCode == 0;
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Error executing service command: {ex.Message}", ex);
                return false;
            }
        }

        private string ExtractExecutablePath(string commandLine)
        {
            if (string.IsNullOrEmpty(commandLine)) return "";

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

        private string GetFilePublisher(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    var versionInfo = FileVersionInfo.GetVersionInfo(filePath);
                    return versionInfo.CompanyName ?? "";
                }
            }
            catch
            {
                // Ignore errors
            }

            return "";
        }

        private string GetFileDescription(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    var versionInfo = FileVersionInfo.GetVersionInfo(filePath);
                    return versionInfo.FileDescription ?? "";
                }
            }
            catch
            {
                // Ignore errors
            }

            return "";
        }

        private string GetExecutableName(string commandLine)
        {
            var path = ExtractExecutablePath(commandLine);
            return Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
        }

        private string[] ParseCsvLine(string line)
        {
            var result = new List<string>();
            var current = "";
            var inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                var c = line[i];
                
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(current);
                    current = "";
                }
                else
                {
                    current += c;
                }
            }

            result.Add(current);
            return result.ToArray();
        }
    }
}
