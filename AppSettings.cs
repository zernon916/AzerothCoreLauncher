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
        
        // Server Health Monitoring Settings
        public bool EnableHealthMonitoring { get; set; } = true;
        public int MemoryAlertThresholdMB { get; set; } = 4096; // 4GB
        public int HealthCheckIntervalSeconds { get; set; } = 5;
        
        // Database Backup Settings
        public bool BackupDatabaseBeforeRestart { get; set; } = false;
        public string BackupDirectory { get; set; } = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Backups");
        
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
                if (!Directory.Exists(directory))
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
                if (!Directory.Exists(directory))
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
    }
}
