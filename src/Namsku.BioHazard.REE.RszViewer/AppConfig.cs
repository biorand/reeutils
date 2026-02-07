using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace RszViewer
{
    public class AppConfig
    {
        public string? LeftFilePath { get; set; }
        public string? RightFilePath { get; set; }
        public string? RszRepoPath { get; set; }
        public List<string> SearchHistory { get; set; } = new List<string>();
        public List<SavedSearch> SavedSearches { get; set; } = new List<SavedSearch>();
        public List<FileHistoryItem> RecentFiles { get; set; } = new List<FileHistoryItem>();
        public ComparisonSession? LastSession { get; set; }
        public string? LastViewFolder { get; set; }
        public string? LastOpenFile { get; set; }
        public string? LastLinkerFolder { get; set; }
        public string? SpreadsheetPath { get; set; }
        public List<string> RecentFolders { get; set; } = new List<string>();
        public bool IsExplorerVisible { get; set; } = true;
        public List<string> OpenedTabPaths { get; set; } = new List<string>();
        public int SelectedTabIndex { get; set; } = 0;

        private static readonly string ConfigPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RszCompare",
            "settings.json");

        public static AppConfig Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string json = File.ReadAllText(ConfigPath);
                    return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
                }
            }
            catch { }
            return new AppConfig();
        }

        public void Save()
        {
            try
            {
                string? directory = Path.GetDirectoryName(ConfigPath);
                if (directory != null && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigPath, json);
            }
            catch { }
        }
    }

    public class SavedSearch
    {
        public string Name { get; set; } = "";
        public List<string> Queries { get; set; } = new List<string>();
        public string Description { get; set; } = "";
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    public class ComparisonSession
    {
        public List<string> ExpandedNodes { get; set; } = new List<string>();
        public double LeftScrollPos { get; set; }
        public double RightScrollPos { get; set; }
    }

    public class FileHistoryItem
    {
        public string LeftPath { get; set; } = "";
        public string RightPath { get; set; } = "";
        public DateTime LastAccessed { get; set; } = DateTime.Now;

        public override string ToString()
        {
            var l = System.IO.Path.GetFileName(LeftPath);
            var r = System.IO.Path.GetFileName(RightPath);
            return $"{l} <-> {r}"; 
        }
    }
}
