using System;
using System.Collections.Generic;

namespace WindowsPCCleaner.Models
{
    // Common Models used across services
    public class CleanableItem
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public long Size { get; set; }
        public string Category { get; set; }
        public CleaningRisk Risk { get; set; }
        public string Description { get; set; }
        public string Icon { get; set; }
        public bool IsSelected { get; set; }
        public BrowserType BrowserType { get; set; }
        public DateTime LastModified { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public class ScanProgress
    {
        public int CurrentStep { get; set; }
        public int TotalSteps { get; set; }
        public int ProcessedFiles { get; set; }
        public string CurrentOperation { get; set; }
        public double PercentComplete => TotalSteps > 0 ? (double)CurrentStep / TotalSteps * 100 : 0;
    }

    public class CleaningProgress
    {
        public int ProcessedItems { get; set; }
        public int TotalItems { get; set; }
        public string CurrentItem { get; set; }
        public string CurrentOperation { get; set; }
        public double PercentComplete => TotalItems > 0 ? (double)ProcessedItems / TotalItems * 100 : 0;
    }

    public class FailedCleaningItem
    {
        public CleanableItem Item { get; set; }
        public string Error { get; set; }
        public DateTime FailureTime { get; set; } = DateTime.Now;
    }

    public class SystemInfo
    {
        public string OperatingSystem { get; set; }
        public string Version { get; set; }
        public string Architecture { get; set; }
        public long TotalMemory { get; set; }
        public long AvailableMemory { get; set; }
        public string ProcessorName { get; set; }
        public int ProcessorCores { get; set; }
        public List<DiskInfo> Drives { get; set; } = new();
    }

    public class CleaningSession
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime StartTime { get; set; } = DateTime.Now;
        public DateTime? EndTime { get; set; }
        public List<CleanableItem> ItemsToClean { get; set; } = new();
        public List<CleanableItem> CleanedItems { get; set; } = new();
        public List<FailedCleaningItem> FailedItems { get; set; } = new();
        public long TotalSizeFreed { get; set; }
        public TimeSpan Duration => EndTime?.Subtract(StartTime) ?? DateTime.Now.Subtract(StartTime);
        public bool IsCompleted => EndTime.HasValue;
    }

    public enum CleaningRisk
    {
        Safe,
        Low,
        Medium,
        High
    }

    public enum ScanType
    {
        Quick,
        Full,
        Custom,
        Privacy,
        Registry,
        Browser,
        Startup,
        DiskAnalysis
    }
}
