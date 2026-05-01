using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace AzerothCoreLauncher
{
    public class AppSettings
    {
        public string ServerDirectory { get; set; } = @"C:\Servers\AzerothCore\Build\bin\RelWithDebInfo";
        public string AuthServerExe { get; set; } = "authserver.exe";
        public string WorldServerExe { get; set; } = "worldserver.exe";
        
        public string MySqlHost { get; set; } = "localhost";
        public string MySqlPort { get; set; } = "3306";
        public string CharacterDatabase { get; set; } = "acore_characters";
        public string AuthDatabase { get; set; } = "acore_auth";
        public string MySqlUser { get; set; } = "root";
        public string MySqlPassword { get; set; } = "password";
        
        public string ConfigDirectory { get; set; } = @"C:\Servers\AzerothCore\Build\bin\RelWithDebInfo\configs";
        
        public int ConsoleLineLimit { get; set; } = 100;
        public List<ScheduledEvent> ScheduledEvents { get; set; } = new List<ScheduledEvent>();
        
        // Server Stability Settings
        public bool AutoRestartOnCrash { get; set; } = true;
        public int MaxAutoRestarts { get; set; } = 3;
        public int AutoRestartDelaySeconds { get; set; } = 5;
        public bool EnableCrashLogAnalysis { get; set; } = true;
        public bool KillExistingServersOnStartup { get; set; } = true;
        
        // Server Health Monitoring Settings
        public bool EnableHealthMonitoring { get; set; } = true;
        public int MemoryAlertThresholdMB { get; set; } = 4096; // 4GB
        public int HealthCheckIntervalSeconds { get; set; } = 5;
        
        // Database Backup Settings
        public bool BackupDatabaseBeforeRestart { get; set; } = false;
        public string BackupDirectory { get; set; } = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Backups");
        public string MySqlDumpPath { get; set; } = "mysqldump"; // Path to mysqldump executable
        public string MySqlPath { get; set; } = "mysql"; // Path to mysql executable
        
        // Notification Settings
        public bool EnableTrayIcon { get; set; } = false;
        public bool MinimizeToTray { get; set; } = false;
        public bool EnableCrashAlerts { get; set; } = true;
        public bool EnableAlertSound { get; set; } = false;
        public bool EnableEventNotifications { get; set; } = false;
        
        private static readonly string SettingsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AzerothCoreLauncher",
            "settings.json"
        );
        
        public static AppSettings Load()
        {
            try
            {
                var directory = Path.GetDirectoryName(SettingsFilePath);
                if (directory != null && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);
                
                if (File.Exists(SettingsFilePath))
                {
                    var json = File.ReadAllText(SettingsFilePath);
                    return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
            }
            catch (Exception)
            {
                // Return default settings if loading fails
            }
            
            return new AppSettings();
        }
        
        public void Save()
        {
            try
            {
                var directory = Path.GetDirectoryName(SettingsFilePath);
                if (directory != null && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);
                
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(this, options);
                File.WriteAllText(SettingsFilePath, json);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to save settings: {ex.Message}");
            }
        }
        
        public string GetAuthServerPath()
        {
            return Path.Combine(ServerDirectory, AuthServerExe);
        }
        
        public string GetWorldServerPath()
        {
            return Path.Combine(ServerDirectory, WorldServerExe);
        }
        
        public string GetWorldServerConfigPath()
        {
            return Path.Combine(ConfigDirectory, "worldserver.conf");
        }
        
        public string GetAuthServerConfigPath()
        {
            return Path.Combine(ConfigDirectory, "authserver.conf");
        }
        
        public string GetModulesDirectory()
        {
            return Path.Combine(ConfigDirectory, "modules");
        }
    }
    
    public class ConfigSection
    {
        public string Name { get; set; } = string.Empty;
        public List<ConfigSetting> Settings { get; set; } = new List<ConfigSetting>();
    }
    
    public class ConfigSetting
    {
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string OriginalLine { get; set; } = string.Empty;
        public int LineNumber { get; set; }
        public bool IsStandardFormat { get; set; } = true;
    }
    
    public class Notification
    {
        public DateTime Time { get; set; } = DateTime.Now;
        public string Type { get; set; } = "Info";
        public string Message { get; set; } = string.Empty;
    }
}
