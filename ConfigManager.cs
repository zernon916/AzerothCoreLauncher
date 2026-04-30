using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AzerothCoreLauncher
{
    public class ConfigManager
    {
        private string _configPath;
        private List<ConfigEntry> _configEntries;
        
        public ConfigManager(string configPath)
        {
            _configPath = configPath;
            _configEntries = new List<ConfigEntry>();
        }
        
        public void LoadConfig()
        {
            _configEntries.Clear();
            
            if (!File.Exists(_configPath))
                throw new FileNotFoundException($"Config file not found: {_configPath}");
            
            var lines = File.ReadAllLines(_configPath);
            var currentSection = "";
            
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                
                if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#"))
                    continue;
                
                if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                {
                    currentSection = trimmed.Trim('[', ']');
                    continue;
                }
                
                if (trimmed.Contains("="))
                {
                    var parts = trimmed.Split(new[] { '=' }, 2);
                    if (parts.Length == 2)
                    {
                        _configEntries.Add(new ConfigEntry
                        {
                            Section = currentSection,
                            Key = parts[0].Trim(),
                            Value = parts[1].Trim(),
                            OriginalLine = line
                        });
                    }
                }
            }
        }
        
        public void SaveConfig()
        {
            var lines = new List<string>();
            string currentSection = "";
            
            foreach (var entry in _configEntries)
            {
                if (entry.Section != currentSection)
                {
                    if (!string.IsNullOrEmpty(currentSection))
                        lines.Add("");
                    
                    lines.Add($"[{entry.Section}]");
                    currentSection = entry.Section;
                }
                
                lines.Add($"{entry.Key} = {entry.Value}");
            }
            
            File.WriteAllLines(_configPath, lines);
        }
        
        public string GetConfigValue(string key)
        {
            var entry = _configEntries.FirstOrDefault(e => e.Key == key);
            return entry?.Value ?? "";
        }
        
        public void SetConfigValue(string key, string value)
        {
            var entry = _configEntries.FirstOrDefault(e => e.Key == key);
            if (entry != null)
            {
                entry.Value = value;
            }
        }
        
        public List<ConfigEntry> SearchConfig(string searchTerm)
        {
            return _configEntries
                .Where(e => e.Key.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                           e.Value.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                           e.Section.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
        
        public string GetFullConfigText()
        {
            var lines = new List<string>();
            string currentSection = "";
            
            foreach (var entry in _configEntries)
            {
                if (entry.Section != currentSection)
                {
                    if (!string.IsNullOrEmpty(currentSection))
                        lines.Add("");
                    
                    lines.Add($"[{entry.Section}]");
                    currentSection = entry.Section;
                }
                
                lines.Add($"{entry.Key} = {entry.Value}");
            }
            
            return string.Join("\n", lines);
        }
        
        public void SetFullConfigText(string text)
        {
            // Parse the full text back into config entries
            _configEntries.Clear();
            var lines = text.Split('\n');
            var currentSection = "";
            
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                
                if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#"))
                    continue;
                
                if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                {
                    currentSection = trimmed.Trim('[', ']');
                    continue;
                }
                
                if (trimmed.Contains("="))
                {
                    var parts = trimmed.Split(new[] { '=' }, 2);
                    if (parts.Length == 2)
                    {
                        _configEntries.Add(new ConfigEntry
                        {
                            Section = currentSection,
                            Key = parts[0].Trim(),
                            Value = parts[1].Trim()
                        });
                    }
                }
            }
        }
    }
    
    public class ConfigEntry
    {
        public string Section { get; set; } = "";
        public string Key { get; set; } = "";
        public string Value { get; set; } = "";
        public string OriginalLine { get; set; } = "";
    }
}
