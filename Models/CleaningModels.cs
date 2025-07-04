using System;
using System.Collections.Generic;

namespace WindowsPCCleaner.Models
{
    public class CleanableItem
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public long Size { get; set; }
        public string Category { get; set; }
        public DateTime LastModified { get; set; }
        public bool IsSelected { get; set; }
        public string Description { get; set; }
        public CleaningRisk Risk { get; set; }
        public string Icon { get; set; }
    }

    public class ScanResult
    {
        public List<CleanableItem> CleanableItems { get; set; } = new List<CleanableItem>();
        public long TotalSize { get; set; }
        public int ItemCount { get; set; }
        public DateTime ScanTime { get; set; } = DateTime.Now;
        public TimeSpan ScanDuration { get; set; }
        public Dictionary<string, int> CategoryCounts { get; set; } = new Dictionary<string, int>();
    }

    public class ScanOptions
    {
        public bool ScanTemporaryFiles { get; set; } = true;
        public bool ScanSystemCache { get; set; } = true;
        public bool ScanLogFiles { get; set; } = true;
        public bool ScanRecycleBin { get; set; } = true;
        public bool ScanRegistry { get; set; } = false;
        public bool ScanBrowserData { get; set; } = false;
        public bool ScanPrivacyFiles { get; set; } = false;
        public List<string> CustomPaths { get; set; } = new List<string>();
        public List<string> ExcludedPaths { get; set; } = new List<string>();
    }

    public class ScanProgress
    {
        public int CurrentStep { get; set; }
        public int TotalSteps { get; set; }
        public string CurrentOperation { get; set; }
        public string CurrentFile { get; set; }
        public long ProcessedSize { get; set; }
        public int ProcessedFiles { get; set; }
    }

    public class CleaningResult
    {
        public bool Success { get; set; }
        public List<CleanableItem> CleanedItems { get; set; } = new List<CleanableItem>();
        public List<FailedCleaningItem> FailedItems { get; set; } = new List<FailedCleaningItem>();
        public long TotalSizeFreed { get; set; }
        public TimeSpan CleaningDuration { get; set; }
        public string ErrorMessage { get; set; }
        public DateTime CleaningTime { get; set; } = DateTime.Now;
    }

    public class CleaningProgress
    {
        public int ProcessedItems { get; set; }
        public int TotalItems { get; set; }
        public string CurrentItem { get; set; }
        public string CurrentOperation { get; set; }
        public long ProcessedSize { get; set; }
    }

    public class FailedCleaningItem
    {
        public CleanableItem Item { get; set; }
        public string Error { get; set; }
    }

    public class SystemHealthScore
    {
        public int Score { get; set; }
        public string Status { get; set; }
        public DateTime LastUpdated { get; set; }
        public string[] Recommendations { get; set; }
    }

    public class QuickStat
    {
        public string Title { get; set; }
        public string Value { get; set; }
        public double Percentage { get; set; }
        public string Icon { get; set; }
        public string Color { get; set; }
    }

    public class RecentActivity
    {
        public string Action { get; set; }
        public DateTime Timestamp { get; set; }
        public string Details { get; set; }
        public string Icon { get; set; }
        public ActivityType Type { get; set; }
    }

    public class NavigationItem
    {
        public string Name { get; set; }
        public string Icon { get; set; }
        public object ViewModel { get; set; }
        public bool IsSelected { get; set; }
    }

    public class DiskInfo
    {
        public string DriveLetter { get; set; }
        public long TotalSpace { get; set; }
        public long UsedSpace { get; set; }
        public long FreeSpace { get; set; }
        public string FileSystem { get; set; }
        public string Label { get; set; }
        public DriveHealthStatus HealthStatus { get; set; }
    }

    public enum CleaningRisk
    {
        Safe,
        Low,
        Medium,
        High,
        Critical
    }

    public enum ActivityType
    {
        Scan,
        Clean,
        Optimize,
        Error,
        Info
    }

    public enum DriveHealthStatus
    {
        Healthy,
        Warning,
        Critical,
        Unknown
    }
}
