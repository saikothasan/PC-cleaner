using System;
using System.Collections.Generic;

namespace WindowsPCCleaner.Models
{
    // Startup Manager Service Models
    public class StartupItem
    {
        public string Name { get; set; }
        public string Command { get; set; }
        public string Path { get; set; }
        public StartupLocation Location { get; set; }
        public bool IsEnabled { get; set; }
        public string Publisher { get; set; }
        public string Description { get; set; }
        public bool FileExists { get; set; }
        public StartupImpact Impact { get; set; }
        public SecurityRisk SecurityRisk { get; set; }
        public DateTime LastModified { get; set; }
        public long FileSize { get; set; }
    }

    public class StartupImpactAnalysis
    {
        public int TotalItems { get; set; }
        public int EnabledItems { get; set; }
        public int DisabledItems { get; set; }
        public int HighImpactItems { get; set; }
        public int MediumImpactItems { get; set; }
        public int LowImpactItems { get; set; }
        public int SecurityRisks { get; set; }
        public double EstimatedStartupDelay { get; set; }
        public int PerformanceScore { get; set; }
    }

    public class StartupRecommendation
    {
        public RecommendationType Type { get; set; }
        public StartupItem Item { get; set; }
        public string Reason { get; set; }
        public double PotentialTimeSaving { get; set; }
        public RecommendationPriority Priority { get; set; }
    }

    public enum StartupLocation
    {
        LocalMachineRun,
        LocalMachineRunOnce,
        CurrentUserRun,
        CurrentUserRunOnce,
        StartupFolder,
        CommonStartupFolder,
        TaskScheduler,
        Services
    }

    public enum StartupImpact
    {
        Low,
        Medium,
        High
    }

    public enum RecommendationType
    {
        Enable,
        Disable,
        Remove,
        General
    }

    public enum RecommendationPriority
    {
        Low,
        Medium,
        High,
        Critical
    }
}
