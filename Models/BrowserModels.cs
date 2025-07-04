using System;
using System.Collections.Generic;

namespace WindowsPCCleaner.Models
{
    // Browser Service Models
    public class BrowserInfo
    {
        public BrowserType Type { get; set; }
        public string Name { get; set; }
        public string Version { get; set; }
        public string UserDataPath { get; set; }
        public bool IsRunning { get; set; }
        public int ProfileCount { get; set; }
    }

    public class BrowserConfig
    {
        public string Name { get; set; }
        public string ExecutableName { get; set; }
        public string UserDataPath { get; set; }
        public string[] CachePaths { get; set; }
        public string CookiesPath { get; set; }
        public string HistoryPath { get; set; }
        public string DownloadsPath { get; set; }
        public string PasswordsPath { get; set; }
        public string BookmarksPath { get; set; }
    }

    public class BrowserScanOptions
    {
        public bool ScanCache { get; set; } = true;
        public bool ScanCookies { get; set; } = false;
        public bool ScanHistory { get; set; } = true;
        public bool ScanDownloads { get; set; } = true;
        public bool ScanPasswords { get; set; } = false;
        public bool ScanFormData { get; set; } = false;
    }

    public class BrowserCleaningResult
    {
        public bool Success { get; set; }
        public List<CleanableItem> CleanedItems { get; set; } = new();
        public List<FailedCleaningItem> FailedItems { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public long TotalSizeFreed { get; set; }
    }

    public enum BrowserType
    {
        Chrome,
        Firefox,
        Edge,
        Opera,
        Safari,
        InternetExplorer
    }
}
