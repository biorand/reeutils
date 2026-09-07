using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace ReeCompare
{
    public class AppConfig
    {
        public string? LeftFilePath { get; set; }
        public string? RightFilePath { get; set; }
        public string? RszRepoPath { get; set; }
        /// <summary>Selected game id (re2/re3/re4/re7/re8/re9/oniws/custom). Embedded RSZ is used unless "custom".</summary>
        public string GameId { get; set; } = "re4";
        public List<string> SearchHistory { get; set; } = new List<string>();
        public List<SavedSearch> SavedSearches { get; set; } = new List<SavedSearch>();
        public List<FileHistoryItem> RecentFiles { get; set; } = new List<FileHistoryItem>();
        public ComparisonSession? LastSession { get; set; }

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
                    var config = JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
                    // One-time migration: settings written before GameId existed only had
                    // a manual RszRepoPath. Keep using that file instead of dropping it.
                    if (!json.Contains("GameId", StringComparison.OrdinalIgnoreCase) &&
                        config.RszRepoPath != null && File.Exists(config.RszRepoPath))
                    {
                        config.GameId = "custom";
                    }
                    return config;
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
