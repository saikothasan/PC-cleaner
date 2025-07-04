using System;
using System.Collections.Generic;

namespace WindowsPCCleaner.Models
{
    // Security Service Models
    public class SecurityScanResult
    {
        public DateTime ScanTime { get; set; }
        public int SecurityScore { get; set; }
        public List<SuspiciousFile> SuspiciousFiles { get; set; } = new();
        public List<PrivacyRisk> PrivacyRisks { get; set; } = new();
        public List<string> UnsignedStartupItems { get; set; } = new();
        public List<string> SuspiciousConnections { get; set; } = new();
        public List<string> Errors { get; set; } = new();
    }

    public class SuspiciousFile
    {
        public string Path { get; set; }
        public string Name { get; set; }
        public long Size { get; set; }
        public DateTime LastModified { get; set; }
        public SecurityRisk RiskLevel { get; set; }
        public List<string> Reasons { get; set; } = new();
        public string Hash { get; set; }
        public bool IsSigned { get; set; }
        public string Publisher { get; set; }
    }

    public class PrivacyRisk
    {
        public PrivacyRiskType Type { get; set; }
        public RiskSeverity Severity { get; set; }
        public string Description { get; set; }
        public string Recommendation { get; set; }
        public int AffectedFiles { get; set; }
        public long EstimatedSize { get; set; }
    }

    public class PrivacyScanArea
    {
        public string Name { get; set; }
        public string[] Paths { get; set; }
        public PrivacyFileType[] FileTypes { get; set; }
    }

    public class PrivacyScanOptions
    {
        public bool ScanBrowserData { get; set; } = true;
        public bool ScanSystemLogs { get; set; } = true;
        public bool ScanRecentFiles { get; set; } = true;
        public bool ScanTemporaryFiles { get; set; } = true;
        public bool ScanApplicationData { get; set; } = false;
        public bool ScanPersonalFiles { get; set; } = false;
    }

    // Enums
    public enum SecurityRisk
    {
        Low,
        Medium,
        High
    }

    public enum PrivacyRiskType
    {
        BrowserData,
        SystemLogs,
        RecentFiles,
        TemporaryFiles,
        NetworkHistory,
        ApplicationData,
        PersonalFiles
    }

    public enum RiskSeverity
    {
        Low,
        Medium,
        High,
        Critical
    }

    public enum PrivacyFileType
    {
        Cookies,
        BrowsingHistory,
        Cache,
        SavedPasswords,
        FormData,
        RecentFiles,
        SystemLogs,
        TemporaryFiles,
        PersonalFiles,
        ApplicationData
    }
}
