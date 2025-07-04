using System;
using System.Collections.Generic;
using System.IO;

namespace WindowsPCCleaner.Models
{
    // Disk Analysis Service Models
    public class DiskInfo
    {
        public string DriveLetter { get; set; }
        public string Label { get; set; }
        public string FileSystem { get; set; }
        public long TotalSpace { get; set; }
        public long FreeSpace { get; set; }
        public long UsedSpace { get; set; }
        public DriveHealthStatus HealthStatus { get; set; }
        public double UsagePercentage => TotalSpace > 0 ? (double)UsedSpace / TotalSpace * 100 : 0;
    }

    public class DiskAnalysisResult
    {
        public string DrivePath { get; set; }
        public DateTime AnalysisTime { get; set; }
        public long TotalSize { get; set; }
        public int TotalFiles { get; set; }
        public List<DirectoryUsage> DirectoryUsage { get; set; } = new();
        public List<DirectoryUsage> LargestDirectories { get; set; } = new();
    }

    public class DirectoryUsage
    {
        public string Path { get; set; }
        public string Name { get; set; }
        public long Size { get; set; }
        public int FileCount { get; set; }
        public double Percentage { get; set; }
    }

    public class LargeFile
    {
        public string Path { get; set; }
        public string Name { get; set; }
        public long Size { get; set; }
        public DateTime LastModified { get; set; }
        public DateTime LastAccessed { get; set; }
        public string Extension { get; set; }
        public string Directory { get; set; }
        public bool IsReadOnly { get; set; }
        public FileAttributes Attributes { get; set; }
    }

    public class DuplicateFileGroup
    {
        public string Hash { get; set; }
        public long Size { get; set; }
        public List<DuplicateFile> Files { get; set; } = new();
        public long TotalWastedSpace { get; set; }
    }

    public class DuplicateFile
    {
        public string Path { get; set; }
        public string Name { get; set; }
        public long Size { get; set; }
        public DateTime LastModified { get; set; }
        public string Directory { get; set; }
        public bool IsSelected { get; set; }
    }

    public class EmptyFolder
    {
        public string Path { get; set; }
        public string Name { get; set; }
        public string ParentPath { get; set; }
        public DateTime CreationTime { get; set; }
        public DateTime LastModified { get; set; }
        public FileAttributes Attributes { get; set; }
        public bool IsHidden { get; set; }
        public bool IsSystem { get; set; }
    }

    public class DiskHealthInfo
    {
        public string DriveLetter { get; set; }
        public DateTime CheckTime { get; set; }
        public DiskHealthStatus OverallHealth { get; set; }
        public string Model { get; set; }
        public string SerialNumber { get; set; }
        public long Size { get; set; }
        public string InterfaceType { get; set; }
        public int Temperature { get; set; }
        public double FreeSpacePercent { get; set; }
        public bool HasFileSystemErrors { get; set; }
        public List<string> Issues { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
    }

    public enum DriveHealthStatus
    {
        Unknown,
        Healthy,
        Warning,
        Critical
    }

    public enum DiskHealthStatus
    {
        Unknown,
        Healthy,
        Warning,
        Critical
    }
}
