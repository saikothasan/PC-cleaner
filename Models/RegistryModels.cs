using System;
using System.Collections.Generic;
using Microsoft.Win32;

namespace WindowsPCCleaner.Models
{
    // Registry Service Models
    public class RegistryScanArea
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public RegistryHive Hive { get; set; }
        public string KeyPath { get; set; }
        public CleaningRisk Risk { get; set; }
        public RegistryScanType ScanType { get; set; }
    }

    public class RegistryScanOptions
    {
        public bool ScanUninstallEntries { get; set; } = true;
        public bool ScanStartupEntries { get; set; } = true;
        public bool ScanFileAssociations { get; set; } = false;
        public bool ScanComObjects { get; set; } = false;
        public bool ScanSharedDlls { get; set; } = false;
        public bool ScanRecentDocuments { get; set; } = true;
        public bool ScanMruLists { get; set; } = true;
    }

    public class RegistryCleaningResult
    {
        public bool Success { get; set; }
        public List<CleanableItem> CleanedItems { get; set; } = new();
        public List<FailedCleaningItem> FailedItems { get; set; } = new();
        public string BackupPath { get; set; }
    }

    public class RegistryIssue
    {
        public RegistryIssueType Type { get; set; }
        public string Description { get; set; }
        public IssueSeverity Severity { get; set; }
        public string Recommendation { get; set; }
        public string RegistryPath { get; set; }
    }

    public enum RegistryScanType
    {
        UninstallEntries,
        StartupEntries,
        FileAssociations,
        ComObjects,
        SharedDlls,
        RecentDocuments,
        MruLists
    }

    public enum RegistryIssueType
    {
        IntegrityViolation,
        ValidationError,
        InvalidEntry,
        MissingReference
    }

    public enum IssueSeverity
    {
        Low,
        Medium,
        High,
        Critical
    }
}
