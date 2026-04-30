using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;

namespace AzerothCoreLauncher
{
    public class DatabaseManager
    {
        private string _connectionString;
        
        public DatabaseManager(string host, string port, string database, string user, string password)
        {
            _connectionString = $"Server={host};Port={port};Database={database};User={user};Password={password};";
        }
        
        public List<PlayerInfo> GetOnlinePlayers()
        {
            var players = new List<PlayerInfo>();
            
            try
            {
                using var connection = new MySqlConnection(_connectionString);
                connection.Open();
                
                string query = @"
                    SELECT c.name, c.race, c.class, c.level, c.zone, c.map 
                    FROM characters c 
                    WHERE c.online = 1 
                    ORDER BY c.name";
                
                using var command = new MySqlCommand(query, connection);
                using var reader = command.ExecuteReader();
                
                while (reader.Read())
                {
                    players.Add(new PlayerInfo
                    {
                        Name = reader.GetString("name"),
                        Race = GetRaceName(reader.GetInt32("race")),
                        Class = GetClassName(reader.GetInt32("class")),
                        Level = reader.GetInt32("level"),
                        Zone = GetZoneName(reader.GetInt32("zone")),
                        MapId = reader.GetInt32("map")
                    });
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to query online players: {ex.Message}");
            }
            
            return players;
        }
        
        public List<PlayerInfo> SearchPlayers(string searchTerm)
        {
            var players = new List<PlayerInfo>();
            
            try
            {
                using var connection = new MySqlConnection(_connectionString);
                connection.Open();
                
                string query = @"
                    SELECT c.name, c.race, c.class, c.level, c.zone, c.map, c.online 
                    FROM characters c 
                    WHERE c.name LIKE @search 
                    ORDER BY c.name";
                
                using var command = new MySqlCommand(query, connection);
                command.Parameters.AddWithValue("@search", $"%{searchTerm}%");
                using var reader = command.ExecuteReader();
                
                while (reader.Read())
                {
                    players.Add(new PlayerInfo
                    {
                        Name = reader.GetString("name"),
                        Race = GetRaceName(reader.GetInt32("race")),
                        Class = GetClassName(reader.GetInt32("class")),
                        Level = reader.GetInt32("level"),
                        Zone = GetZoneName(reader.GetInt32("zone")),
                        MapId = reader.GetInt32("map"),
                        IsOnline = reader.GetBoolean("online")
                    });
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to search players: {ex.Message}");
            }
            
            return players;
        }
        
        public bool ExecuteGMCommand(string command, string targetPlayer)
        {
            // This would send commands to the worldserver process
            // For now, return true to simulate success
            // TODO: Implement actual command sending via process stdin
            return true;
        }
        
        private string GetRaceName(int raceId)
        {
            return raceId switch
            {
                1 => "Human",
                2 => "Orc",
                3 => "Dwarf",
                4 => "Night Elf",
                5 => "Undead",
                6 => "Tauren",
                7 => "Gnome",
                8 => "Troll",
                10 => "Blood Elf",
                11 => "Draenei",
                _ => $"Unknown ({raceId})"
            };
        }
        
        private string GetClassName(int classId)
        {
            return classId switch
            {
                1 => "Warrior",
                2 => "Paladin",
                3 => "Hunter",
                4 => "Rogue",
                5 => "Priest",
                6 => "Death Knight",
                7 => "Shaman",
                8 => "Mage",
                9 => "Warlock",
                11 => "Druid",
                _ => $"Unknown ({classId})"
            };
        }
        
        private string GetZoneName(int zoneId)
        {
            // TODO: Implement zone name lookup from database or DBC
            return zoneId.ToString();
        }
        
        public string BackupDatabase(string backupDirectory)
        {
            try
            {
                // Create backup directory if it doesn't exist
                if (!Directory.Exists(backupDirectory))
                    Directory.CreateDirectory(backupDirectory);
                
                // Generate backup filename with timestamp
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string backupFile = Path.Combine(backupDirectory, $"acore_characters_backup_{timestamp}.sql");
                
                // Use mysqldump to backup the database
                string mysqldumpPath = "mysqldump"; // Assumes mysqldump is in PATH
                string arguments = $"-h{_connectionString.Split(';')[1].Split('=')[1]} " +
                                 $"-P{_connectionString.Split(';')[2].Split('=')[1]} " +
                                 $"-u{_connectionString.Split(';')[4].Split('=')[1]} " +
                                 $"-p{_connectionString.Split(';')[5].Split('=')[1]} " +
                                 $"{_connectionString.Split(';')[3].Split('=')[1]} " +
                                 $"-r \"{backupFile}\"";
                
                var processInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = mysqldumpPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                
                using var process = System.Diagnostics.Process.Start(processInfo);
                if (process == null)
                    throw new Exception("Failed to start mysqldump process");
                
                process.WaitForExit();
                
                if (process.ExitCode != 0)
                {
                    string error = process.StandardError.ReadToEnd();
                    throw new Exception($"mysqldump failed: {error}");
                }
                
                return backupFile;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to backup database: {ex.Message}");
            }
        }
        
        public void RestoreDatabase(string backupFile)
        {
            try
            {
                if (!File.Exists(backupFile))
                    throw new Exception($"Backup file not found: {backupFile}");
                
                // Use mysql to restore the database
                string mysqlPath = "mysql"; // Assumes mysql is in PATH
                string arguments = $"-h{_connectionString.Split(';')[1].Split('=')[1]} " +
                                 $"-P{_connectionString.Split(';')[2].Split('=')[1]} " +
                                 $"-u{_connectionString.Split(';')[4].Split('=')[1]} " +
                                 $"-p{_connectionString.Split(';')[5].Split('=')[1]} " +
                                 $"{_connectionString.Split(';')[3].Split('=')[1]} " +
                                 $"< \"{backupFile}\"";
                
                var processInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c {mysqlPath} {arguments}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                
                using var process = System.Diagnostics.Process.Start(processInfo);
                if (process == null)
                    throw new Exception("Failed to start mysql process");
                
                process.WaitForExit();
                
                if (process.ExitCode != 0)
                {
                    string error = process.StandardError.ReadToEnd();
                    throw new Exception($"mysql restore failed: {error}");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to restore database: {ex.Message}");
            }
        }
        
        public List<string> GetBackupFiles(string backupDirectory)
        {
            var backups = new List<string>();
            
            if (!Directory.Exists(backupDirectory))
                return backups;
            
            try
            {
                var files = Directory.GetFiles(backupDirectory, "acore_characters_backup_*.sql")
                                   .OrderByDescending(f => File.GetCreationTime(f));
                
                backups.AddRange(files);
            }
            catch (Exception)
            {
                // Return empty list if error
            }
            
            return backups;
        }
    }
    
    public class PlayerInfo
    {
        public string Name { get; set; } = "";
        public string Race { get; set; } = "";
        public string Class { get; set; } = "";
        public int Level { get; set; }
        public string Zone { get; set; } = "";
        public int MapId { get; set; }
        public bool IsOnline { get; set; } = true;
    }
}
