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
        private readonly string _connectionString;
        private readonly string _authConnectionString;
        
        public DatabaseManager(string host, string port, string characterDatabase, string user, string password, string authDatabase = "acore_auth")
        {
            _connectionString = $"Server={host};Port={port};Database={characterDatabase};User={user};Password={password};";
            _authConnectionString = $"Server={host};Port={port};Database={authDatabase};User={user};Password={password};";
        }
        
        public List<PlayerInfo> GetOfflinePlayers()
        {
            var players = new List<PlayerInfo>();
            try
            {
                using var connection = new MySqlConnection(_connectionString);
                connection.Open();

                string query = @"
                    SELECT c.name, c.race, c.class, c.level, c.zone, c.map, c.account 
                    FROM characters c 
                    WHERE c.online = 0 
                    ORDER BY c.name";

                using var command = new MySqlCommand(query, connection);
                using var reader = command.ExecuteReader();

                var accountIds = new List<int>();
                var playerDict = new Dictionary<int, PlayerInfo>();

                while (reader.Read())
                {
                    int accountId = reader.GetInt32("account");
                    var player = new PlayerInfo
                    {
                        Name = reader.GetString("name"),
                        Race = GetRaceName(reader.GetInt32("race")),
                        Class = GetClassName(reader.GetInt32("class")),
                        Level = reader.GetInt32("level"),
                        Zone = GetZoneName(reader.GetInt32("zone")),
                        MapId = reader.GetInt32("map"),
                        IsOnline = false,
                        IPAddress = "",
                        AccountId = accountId
                    };

                    players.Add(player);
                    accountIds.Add(accountId);
                    playerDict[accountId] = player;
                }

                if (accountIds.Count > 0)
                {
                    FetchIPAddresses(accountIds, playerDict);
                }

                // Filter out players without IP or with 127.0.0.1 (bots)
                players = players.Where(p => !string.IsNullOrEmpty(p.IPAddress) && p.IPAddress != "127.0.0.1").ToList();
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to query offline players: {ex.Message}");
            }

            return players;
        }
        
        public List<PlayerInfo> SearchOfflinePlayers(string searchTerm)
        {
            var players = new List<PlayerInfo>();
            try
            {
                using var connection = new MySqlConnection(_connectionString);
                connection.Open();

                string query = @"
                    SELECT c.name, c.race, c.class, c.level, c.zone, c.map, c.account 
                    FROM characters c 
                    WHERE c.online = 0 AND c.name LIKE @searchTerm
                    ORDER BY c.name";

                using var command = new MySqlCommand(query, connection);
                command.Parameters.AddWithValue("@searchTerm", $"%{searchTerm}%");
                using var reader = command.ExecuteReader();

                var accountIds = new List<int>();
                var playerDict = new Dictionary<int, PlayerInfo>();

                while (reader.Read())
                {
                    int accountId = reader.GetInt32("account");
                    var player = new PlayerInfo
                    {
                        Name = reader.GetString("name"),
                        Race = GetRaceName(reader.GetInt32("race")),
                        Class = GetClassName(reader.GetInt32("class")),
                        Level = reader.GetInt32("level"),
                        Zone = GetZoneName(reader.GetInt32("zone")),
                        MapId = reader.GetInt32("map"),
                        IsOnline = false,
                        IPAddress = "",
                        AccountId = accountId
                    };

                    players.Add(player);
                    accountIds.Add(accountId);
                    playerDict[accountId] = player;
                }

                if (accountIds.Count > 0)
                {
                    FetchIPAddresses(accountIds, playerDict);
                }

                // Filter out players without IP or with 127.0.0.1 (bots)
                players = players.Where(p => !string.IsNullOrEmpty(p.IPAddress) && p.IPAddress != "127.0.0.1").ToList();
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to search offline players: {ex.Message}");
            }

            return players;
        }
        
        public List<PlayerInfo> GetOnlinePlayers()
        {
            var players = new List<PlayerInfo>();
            
            try
            {
                using var connection = new MySqlConnection(_connectionString);
                connection.Open();
                
                string query = @"
                    SELECT c.name, c.race, c.class, c.level, c.zone, c.map, c.account 
                    FROM characters c 
                    WHERE c.online = 1 
                    ORDER BY c.name";
                
                using var command = new MySqlCommand(query, connection);
                using var reader = command.ExecuteReader();
                
                var accountIds = new List<int>();
                var playerDict = new Dictionary<int, PlayerInfo>();
                
                while (reader.Read())
                {
                    int accountId = reader.GetInt32("account");
                    var player = new PlayerInfo
                    {
                        Name = reader.GetString("name"),
                        Race = GetRaceName(reader.GetInt32("race")),
                        Class = GetClassName(reader.GetInt32("class")),
                        Level = reader.GetInt32("level"),
                        Zone = GetZoneName(reader.GetInt32("zone")),
                        MapId = reader.GetInt32("map"),
                        IsOnline = true,
                        IPAddress = "",
                        AccountId = accountId
                    };
                    
                    players.Add(player);
                    accountIds.Add(accountId);
                    playerDict[accountId] = player;
                }
                
                // Now fetch IP addresses from auth database
                if (accountIds.Count > 0)
                {
                    FetchIPAddresses(accountIds, playerDict);
                }
                
                // Filter out players without IP or with 127.0.0.1 (bots)
                players = players.Where(p => !string.IsNullOrEmpty(p.IPAddress) && p.IPAddress != "127.0.0.1").ToList();
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to query online players: {ex.Message}");
            }
            
            return players;
        }
        
        private void FetchIPAddresses(List<int> accountIds, Dictionary<int, PlayerInfo> playerDict)
        {
            try
            {
                using var authConnection = new MySqlConnection(_authConnectionString);
                authConnection.Open();
                
                string accountIdsStr = string.Join(",", accountIds);
                string query = $"SELECT id, last_attempt_ip FROM account WHERE id IN ({accountIdsStr})";
                
                using var command = new MySqlCommand(query, authConnection);
                using var reader = command.ExecuteReader();
                
                while (reader.Read())
                {
                    int accountId = reader.GetInt32("id");
                    if (playerDict.ContainsKey(accountId))
                    {
                        playerDict[accountId].IPAddress = reader.IsDBNull("last_attempt_ip") ? "" : reader.GetString("last_attempt_ip");
                    }
                }
            }
            catch (Exception)
            {
                // If we can't fetch IP addresses, just leave them empty
            }
        }
        
        public List<PlayerInfo> SearchPlayers(string searchTerm)
        {
            var players = new List<PlayerInfo>();
            
            try
            {
                using var connection = new MySqlConnection(_connectionString);
                connection.Open();
                
                string query = @"
                    SELECT c.name, c.race, c.class, c.level, c.zone, c.map, c.online, c.account 
                    FROM characters c 
                    WHERE c.name LIKE @search 
                    ORDER BY c.name";
                
                using var command = new MySqlCommand(query, connection);
                command.Parameters.AddWithValue("@search", $"%{searchTerm}%");
                using var reader = command.ExecuteReader();
                
                var accountIds = new List<int>();
                var playerDict = new Dictionary<int, PlayerInfo>();
                
                while (reader.Read())
                {
                    int accountId = reader.GetInt32("account");
                    var player = new PlayerInfo
                    {
                        Name = reader.GetString("name"),
                        Race = GetRaceName(reader.GetInt32("race")),
                        Class = GetClassName(reader.GetInt32("class")),
                        Level = reader.GetInt32("level"),
                        Zone = GetZoneName(reader.GetInt32("zone")),
                        MapId = reader.GetInt32("map"),
                        IsOnline = reader.GetBoolean("online"),
                        IPAddress = "",
                        AccountId = accountId
                    };
                    
                    players.Add(player);
                    accountIds.Add(accountId);
                    playerDict[accountId] = player;
                }
                
                // Now fetch IP addresses from auth database
                if (accountIds.Count > 0)
                {
                    FetchIPAddresses(accountIds, playerDict);
                }
                
                // Filter out players without IP or with 127.0.0.1 (bots)
                players = players.Where(p => !string.IsNullOrEmpty(p.IPAddress) && p.IPAddress != "127.0.0.1").ToList();
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
        
        public string BackupDatabase(string backupDirectory, string mysqldumpPathSetting = "mysqldump")
        {
            try
            {
                // Create backup directory if it doesn't exist
                if (!Directory.Exists(backupDirectory))
                    Directory.CreateDirectory(backupDirectory);
                
                // Generate backup filename with timestamp
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string backupFile = Path.Combine(backupDirectory, $"acore_characters_backup_{timestamp}.sql");
                
                // Parse connection string properly
                var parts = _connectionString.Split(';');
                var connectionParams = new Dictionary<string, string>();
                foreach (var part in parts)
                {
                    var keyValue = part.Split('=');
                    if (keyValue.Length == 2)
                    {
                        connectionParams[keyValue[0].Trim()] = keyValue[1].Trim();
                    }
                }
                
                string host = connectionParams.ContainsKey("Server") ? connectionParams["Server"] : "localhost";
                string port = connectionParams.ContainsKey("Port") ? connectionParams["Port"] : "3306";
                string user = connectionParams.ContainsKey("User") ? connectionParams["User"] : (connectionParams.ContainsKey("User Id") ? connectionParams["User Id"] : "root");
                string password = connectionParams.ContainsKey("Password") ? connectionParams["Password"] : "";
                string database = connectionParams.ContainsKey("Database") ? connectionParams["Database"] : "acore_characters";
                
                // Use mysqldump to backup the database
                string mysqldumpPath = mysqldumpPathSetting ?? "mysqldump"; // Use setting or default to PATH
                string arguments = $"-h{host} -P{port} -u{user} -p{password} {database} -r \"{backupFile}\"";
                
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
        
        public void RestoreDatabase(string backupFile, string mysqlPathSetting = "mysql")
        {
            try
            {
                if (!File.Exists(backupFile))
                    throw new Exception($"Backup file not found: {backupFile}");
                
                // Parse connection string properly
                var parts = _connectionString.Split(';');
                var connectionParams = new Dictionary<string, string>();
                foreach (var part in parts)
                {
                    var keyValue = part.Split('=');
                    if (keyValue.Length == 2)
                    {
                        connectionParams[keyValue[0].Trim()] = keyValue[1].Trim();
                    }
                }
                
                string host = connectionParams.ContainsKey("Server") ? connectionParams["Server"] : "localhost";
                string port = connectionParams.ContainsKey("Port") ? connectionParams["Port"] : "3306";
                string user = connectionParams.ContainsKey("User") ? connectionParams["User"] : (connectionParams.ContainsKey("User Id") ? connectionParams["User Id"] : "root");
                string password = connectionParams.ContainsKey("Password") ? connectionParams["Password"] : "";
                string database = connectionParams.ContainsKey("Database") ? connectionParams["Database"] : "acore_characters";
                
                // Use mysql to restore the database
                string mysqlPath = mysqlPathSetting ?? "mysql"; // Use setting or default to PATH
                string arguments = $"-h{host} -P{port} -u{user} -p{password} {database} < \"{backupFile}\"";
                
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
        
        // IP Ban Management
        public List<IPBan> GetIPBans()
        {
            var bans = new List<IPBan>();
            
            try
            {
                using var connection = new MySqlConnection(_authConnectionString);
                connection.Open();
                
                string query = @"
                    SELECT ip, bandate, bannedby, banreason 
                    FROM ip_banned 
                    WHERE unbandate IS NULL OR unbandate > UNIX_TIMESTAMP()
                    ORDER BY bandate DESC";
                
                using var command = new MySqlCommand(query, connection);
                using var reader = command.ExecuteReader();
                
                while (reader.Read())
                {
                    bans.Add(new IPBan
                    {
                        IP = reader.GetString("ip"),
                        BannedAt = reader.GetInt64("bandate"),
                        BannedBy = reader.GetString("bannedby"),
                        Reason = reader.GetString("banreason")
                    });
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get IP bans: {ex.Message}");
            }
            
            return bans;
        }
        
        public void BanIP(string ip, string reason, string bannedBy)
        {
            try
            {
                using var connection = new MySqlConnection(_authConnectionString);
                connection.Open();
                
                string query = @"
                    INSERT INTO ip_banned (ip, bandate, bannedby, banreason)
                    VALUES (@ip, UNIX_TIMESTAMP(), @bannedBy, @reason)";
                
                using var command = new MySqlCommand(query, connection);
                command.Parameters.AddWithValue("@ip", ip);
                command.Parameters.AddWithValue("@bannedBy", bannedBy);
                command.Parameters.AddWithValue("@reason", reason);
                command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to ban IP: {ex.Message}");
            }
        }
        
        public void UnbanIP(string ip)
        {
            try
            {
                using var connection = new MySqlConnection(_authConnectionString);
                connection.Open();
                
                string query = "DELETE FROM ip_banned WHERE ip = @ip";
                
                using var command = new MySqlCommand(query, connection);
                command.Parameters.AddWithValue("@ip", ip);
                command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to unban IP: {ex.Message}");
            }
        }
        
        public bool IsIPBanned(string ip)
        {
            try
            {
                using var connection = new MySqlConnection(_authConnectionString);
                connection.Open();
                
                string query = "SELECT COUNT(*) FROM ip_banned WHERE ip = @ip";
                
                using var command = new MySqlCommand(query, connection);
                command.Parameters.AddWithValue("@ip", ip);
                
                var result = command.ExecuteScalar();
                return Convert.ToInt32(result) > 0;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to check IP ban status: {ex.Message}");
            }
        }
        
        // Account Security
        public AccountInfo GetAccountInfo(string username)
        {
            try
            {
                using var connection = new MySqlConnection(_authConnectionString);
                connection.Open();
                
                string query = @"
                    SELECT id, username, email, last_login, locked, expansion, joindate, last_ip, online, totaltime, failed_logins
                    FROM account 
                    WHERE username = @username";
                
                using var command = new MySqlCommand(query, connection);
                command.Parameters.AddWithValue("@username", username);
                using var reader = command.ExecuteReader();
                
                if (reader.Read())
                {
                    return new AccountInfo
                    {
                        Id = reader.GetInt32("id"),
                        Username = reader.GetString("username"),
                        Email = reader.GetString("email"),
                        LastLogin = reader.IsDBNull("last_login") ? DateTime.MinValue : reader.GetDateTime("last_login"),
                        Locked = reader.GetInt32("locked") == 1,
                        Expansion = reader.GetInt32("expansion"),
                        JoinDate = reader.GetDateTime("joindate"),
                        LastIP = reader.GetString("last_ip"),
                        Online = reader.GetInt32("online") == 1,
                        TotalTime = reader.GetInt32("totaltime"),
                        FailedLogins = reader.GetInt32("failed_logins")
                    };
                }
                
                return null!;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get account info: {ex.Message}");
            }
        }
        
        public string GetUsernameByAccountId(int accountId)
        {
            try
            {
                using var connection = new MySqlConnection(_authConnectionString);
                connection.Open();
                
                string query = "SELECT username FROM account WHERE id = @accountId";
                
                using var command = new MySqlCommand(query, connection);
                command.Parameters.AddWithValue("@accountId", accountId);
                var result = command.ExecuteScalar();
                
                return result?.ToString() ?? string.Empty;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get username by account ID: {ex.Message}");
            }
        }
        
        public List<AccountInfo> GetAllAccounts()
        {
            var accounts = new List<AccountInfo>();
            try
            {
                using var connection = new MySqlConnection(_authConnectionString);
                connection.Open();
                
                string query = @"
                    SELECT a.id, a.username, a.email, a.last_login, a.locked, a.expansion, a.joindate, a.last_ip, a.online, a.totaltime, a.failed_logins, 
                           COALESCE(aa.gmlevel, 0) as gmlevel
                    FROM account a
                    LEFT JOIN account_access aa ON a.id = aa.id
                    ORDER BY a.username";
                
                using var command = new MySqlCommand(query, connection);
                using var reader = command.ExecuteReader();
                
                while (reader.Read())
                {
                    accounts.Add(new AccountInfo
                    {
                        Id = reader.GetInt32("id"),
                        Username = reader.GetString("username"),
                        Email = reader.GetString("email"),
                        LastLogin = reader.IsDBNull("last_login") ? DateTime.MinValue : reader.GetDateTime("last_login"),
                        Locked = reader.GetInt32("locked") == 1,
                        Expansion = reader.GetInt32("expansion"),
                        JoinDate = reader.GetDateTime("joindate"),
                        LastIP = reader.GetString("last_ip"),
                        Online = reader.GetInt32("online") == 1,
                        TotalTime = reader.GetInt32("totaltime"),
                        FailedLogins = reader.GetInt32("failed_logins"),
                        GMLevel = reader.GetInt32("gmlevel")
                    });
                }
                
                return accounts;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get all accounts: {ex.Message}");
            }
        }
        
        public void ChangePassword(string username, string newPassword)
        {
            try
            {
                using var connection = new MySqlConnection(_authConnectionString);
                connection.Open();
                
                // Use SHA1 hashing for password (AzerothCore default)
                using var sha1 = System.Security.Cryptography.SHA1.Create();
                var hashBytes = sha1.ComputeHash(System.Text.Encoding.UTF8.GetBytes(newPassword.ToUpper()));
                string hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
                
                string query = "UPDATE account SET verifier = @verifier, salt = @salt WHERE username = @username";
                
                using var command = new MySqlCommand(query, connection);
                command.Parameters.AddWithValue("@verifier", hash);
                command.Parameters.AddWithValue("@salt", new byte[32]);
                command.Parameters.AddWithValue("@username", username);
                command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to change password: {ex.Message}");
            }
        }
        
        public void ChangeEmail(string username, string newEmail)
        {
            try
            {
                using var connection = new MySqlConnection(_authConnectionString);
                connection.Open();
                
                string query = "UPDATE account SET email = @email WHERE username = @username";
                
                using var command = new MySqlCommand(query, connection);
                command.Parameters.AddWithValue("@email", newEmail);
                command.Parameters.AddWithValue("@username", username);
                command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to change email: {ex.Message}");
            }
        }
        
        public void CreateAccount(string username, string password, string email, int expansion)
        {
            try
            {
                using var connection = new MySqlConnection(_authConnectionString);
                connection.Open();
                
                // Check if username already exists
                string checkQuery = "SELECT COUNT(*) FROM account WHERE username = @username";
                using var checkCommand = new MySqlCommand(checkQuery, connection);
                checkCommand.Parameters.AddWithValue("@username", username);
                var count = Convert.ToInt32(checkCommand.ExecuteScalar());
                
                if (count > 0)
                {
                    throw new Exception("Username already exists");
                }
                
                // AzerothCore uses SRP6 for password hashing
                // Insert with NULL for verifier/salt - server will handle password setup
                string insertQuery = @"
                    INSERT INTO account (username, salt, verifier, session_key, email, expansion, joindate, last_ip, failed_logins, locked)
                    VALUES (@username, NULL, NULL, NULL, @email, @expansion, CURRENT_TIMESTAMP, '0.0.0.0', 0, 0)";
                
                using var command = new MySqlCommand(insertQuery, connection);
                command.Parameters.AddWithValue("@username", username);
                command.Parameters.AddWithValue("@email", email);
                command.Parameters.AddWithValue("@expansion", expansion);
                command.ExecuteNonQuery();
                
                // Store password for later use via server command
                // The password will be set using account.set password command
                // For now, just create the account without password
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to create account: {ex.Message}");
            }
        }
        
        public string GetCreateAccountCommand(string username, string password)
        {
            // Returns the command to set password after account creation
            return $"account set password {username} {password}";
        }
        
        public void DeleteAccount(string username)
        {
            try
            {
                using var connection = new MySqlConnection(_authConnectionString);
                connection.Open();
                
                // Get the account ID first
                string getAccountIdQuery = "SELECT id FROM account WHERE username = @username";
                int accountId;
                using var getAccountIdCommand = new MySqlCommand(getAccountIdQuery, connection);
                getAccountIdCommand.Parameters.AddWithValue("@username", username);
                var result = getAccountIdCommand.ExecuteScalar();
                
                if (result == null)
                {
                    throw new Exception("Account not found");
                }
                
                accountId = Convert.ToInt32(result);
                
                // Delete associated characters from character database
                using var charConnection = new MySqlConnection(_connectionString);
                charConnection.Open();
                
                string deleteCharactersQuery = "DELETE FROM characters WHERE account = @accountId";
                using var deleteCharactersCommand = new MySqlCommand(deleteCharactersQuery, charConnection);
                deleteCharactersCommand.Parameters.AddWithValue("@accountId", accountId);
                deleteCharactersCommand.ExecuteNonQuery();
                
                // Then delete the account
                string deleteAccountQuery = "DELETE FROM account WHERE username = @username";
                using var deleteAccountCommand = new MySqlCommand(deleteAccountQuery, connection);
                deleteAccountCommand.Parameters.AddWithValue("@username", username);
                deleteAccountCommand.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to delete account: {ex.Message}");
            }
        }
        
        public void LockAccount(string username)
        {
            try
            {
                using var connection = new MySqlConnection(_authConnectionString);
                connection.Open();
                
                string query = "UPDATE account SET locked = 1 WHERE username = @username";
                
                using var command = new MySqlCommand(query, connection);
                command.Parameters.AddWithValue("@username", username);
                command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to lock account: {ex.Message}");
            }
        }
        
        public void BanAccount(string username, string reason, string bannedBy)
        {
            try
            {
                using var connection = new MySqlConnection(_authConnectionString);
                connection.Open();
                
                // First get account ID from username
                string getIdQuery = "SELECT id FROM account WHERE username = @username";
                int accountId = 0;
                
                using var idCommand = new MySqlCommand(getIdQuery, connection);
                idCommand.Parameters.AddWithValue("@username", username);
                var result = idCommand.ExecuteScalar();
                if (result != null)
                {
                    accountId = Convert.ToInt32(result);
                }
                
                if (accountId == 0)
                    throw new Exception($"Account {username} not found");
                
                // Insert into account_banned table
                string banQuery = @"
                    INSERT INTO account_banned (id, bandate, unbandate, bannedby, banreason, active)
                    VALUES (@accountId, UNIX_TIMESTAMP(), UNIX_TIMESTAMP() + 2592000, @bannedBy, @reason, 1)";
                
                using var banCommand = new MySqlCommand(banQuery, connection);
                banCommand.Parameters.AddWithValue("@accountId", accountId);
                banCommand.Parameters.AddWithValue("@bannedBy", bannedBy);
                banCommand.Parameters.AddWithValue("@reason", reason);
                banCommand.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to ban account: {ex.Message}");
            }
        }
        
        public void UnbanAccount(string username)
        {
            try
            {
                using var connection = new MySqlConnection(_authConnectionString);
                connection.Open();
                
                // First get account ID from username
                string getIdQuery = "SELECT id FROM account WHERE username = @username";
                int accountId = 0;
                
                using var idCommand = new MySqlCommand(getIdQuery, connection);
                idCommand.Parameters.AddWithValue("@username", username);
                var result = idCommand.ExecuteScalar();
                if (result != null)
                {
                    accountId = Convert.ToInt32(result);
                }
                
                if (accountId == 0)
                    throw new Exception($"Account {username} not found");
                
                // Update account_banned to set active = 0
                string unbanQuery = "UPDATE account_banned SET active = 0 WHERE id = @accountId AND active = 1";
                
                using var unbanCommand = new MySqlCommand(unbanQuery, connection);
                unbanCommand.Parameters.AddWithValue("@accountId", accountId);
                unbanCommand.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to unban account: {ex.Message}");
            }
        }
        
        public List<AccountBanHistory> GetAccountBanHistory(string username)
        {
            var history = new List<AccountBanHistory>();
            
            try
            {
                using var connection = new MySqlConnection(_authConnectionString);
                connection.Open();
                
                // First get account ID from username
                string getIdQuery = "SELECT id FROM account WHERE username = @username";
                int accountId = 0;
                
                using var idCommand = new MySqlCommand(getIdQuery, connection);
                idCommand.Parameters.AddWithValue("@username", username);
                var result = idCommand.ExecuteScalar();
                if (result != null)
                {
                    accountId = Convert.ToInt32(result);
                }
                
                if (accountId == 0)
                    return history;
                
                // Get ban history
                string query = @"
                    SELECT id, bandate, unbandate, bannedby, banreason, active 
                    FROM account_banned 
                    WHERE id = @accountId 
                    ORDER BY bandate DESC";
                
                using var command = new MySqlCommand(query, connection);
                command.Parameters.AddWithValue("@accountId", accountId);
                using var reader = command.ExecuteReader();
                
                while (reader.Read())
                {
                    history.Add(new AccountBanHistory
                    {
                        AccountId = reader.GetInt32("id"),
                        BanDate = reader.GetInt64("bandate"),
                        UnbanDate = reader.IsDBNull("unbandate") ? 0 : reader.GetInt64("unbandate"),
                        BannedBy = reader.GetString("bannedby"),
                        BanReason = reader.GetString("banreason"),
                        Active = reader.GetBoolean("active")
                    });
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get account ban history: {ex.Message}");
            }
            
            return history;
        }
        
        public List<AccountBanHistory> GetAllAccountBans()
        {
            var bans = new List<AccountBanHistory>();
            
            try
            {
                using var connection = new MySqlConnection(_authConnectionString);
                connection.Open();
                
                string query = @"
                    SELECT id, bandate, unbandate, bannedby, banreason, active 
                    FROM account_banned 
                    ORDER BY bandate DESC";
                
                using var command = new MySqlCommand(query, connection);
                using var reader = command.ExecuteReader();
                
                while (reader.Read())
                {
                    bans.Add(new AccountBanHistory
                    {
                        AccountId = reader.GetInt32("id"),
                        BanDate = reader.GetInt64("bandate"),
                        UnbanDate = reader.IsDBNull("unbandate") ? 0 : reader.GetInt64("unbandate"),
                        BannedBy = reader.GetString("bannedby"),
                        BanReason = reader.GetString("banreason"),
                        Active = reader.GetBoolean("active")
                    });
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get all account bans: {ex.Message}");
            }
            
            return bans;
        }
        
        public List<IPBanHistory> GetIPBanHistory(string ip)
        {
            var history = new List<IPBanHistory>();
            
            try
            {
                using var connection = new MySqlConnection(_authConnectionString);
                connection.Open();
                
                string query = @"
                    SELECT ip, bandate, unbandate, bannedby, banreason 
                    FROM ip_banned 
                    WHERE ip = @ip 
                    ORDER BY bandate DESC";
                
                using var command = new MySqlCommand(query, connection);
                command.Parameters.AddWithValue("@ip", ip);
                using var reader = command.ExecuteReader();
                
                while (reader.Read())
                {
                    history.Add(new IPBanHistory
                    {
                        IP = reader.GetString("ip"),
                        BanDate = reader.GetInt64("bandate"),
                        UnbanDate = reader.IsDBNull("unbandate") ? 0 : reader.GetInt64("unbandate"),
                        BannedBy = reader.GetString("bannedby"),
                        BanReason = reader.GetString("banreason")
                    });
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get IP ban history: {ex.Message}");
            }
            
            return history;
        }
        
        public List<IPBanHistory> GetAllIPBans()
        {
            var bans = new List<IPBanHistory>();
            
            try
            {
                using var connection = new MySqlConnection(_authConnectionString);
                connection.Open();
                
                string query = @"
                    SELECT ip, bandate, unbandate, bannedby, banreason 
                    FROM ip_banned 
                    ORDER BY bandate DESC";
                
                using var command = new MySqlCommand(query, connection);
                using var reader = command.ExecuteReader();
                
                while (reader.Read())
                {
                    bans.Add(new IPBanHistory
                    {
                        IP = reader.GetString("ip"),
                        BanDate = reader.GetInt64("bandate"),
                        UnbanDate = reader.IsDBNull("unbandate") ? 0 : reader.GetInt64("unbandate"),
                        BannedBy = reader.GetString("bannedby"),
                        BanReason = reader.GetString("banreason")
                    });
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get all IP bans: {ex.Message}");
            }
            
            return bans;
        }
        
        public void UnlockAccount(string username)
        {
            try
            {
                using var connection = new MySqlConnection(_authConnectionString);
                connection.Open();
                
                string query = "UPDATE account SET locked = 0 WHERE username = @username";
                
                using var command = new MySqlCommand(query, connection);
                command.Parameters.AddWithValue("@username", username);
                command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to unlock account: {ex.Message}");
            }
        }
        
        // Login History
        public List<LoginHistory> GetLoginHistory(string username, int limit = 50)
        {
            var history = new List<LoginHistory>();
            
            try
            {
                using var connection = new MySqlConnection(_authConnectionString);
                connection.Open();
                
                string query = @"
                    SELECT login_time, ip, success, error 
                    FROM account_log 
                    WHERE username = @username
                    ORDER BY login_time DESC
                    LIMIT @limit";
                
                using var command = new MySqlCommand(query, connection);
                command.Parameters.AddWithValue("@username", username);
                command.Parameters.AddWithValue("@limit", limit);
                using var reader = command.ExecuteReader();
                
                while (reader.Read())
                {
                    history.Add(new LoginHistory
                    {
                        LoginTime = reader.GetDateTime("login_time"),
                        IPAddress = reader.GetString("ip"),
                        Success = reader.GetBoolean("success"),
                        Error = reader.IsDBNull("error") ? "" : reader.GetString("error")
                    });
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get login history: {ex.Message}");
            }
            
            return history;
        }
        
        public string GetAccountUsernameByCharacterName(string characterName)
        {
            try
            {
                // First get account ID from characters database
                int accountId = 0;
                using var charConnection = new MySqlConnection(_connectionString);
                charConnection.Open();
                
                string query = @"
                    SELECT c.account 
                    FROM characters c 
                    WHERE c.name = @characterName";
                
                using var command = new MySqlCommand(query, charConnection);
                command.Parameters.AddWithValue("@characterName", characterName);
                
                var result = command.ExecuteScalar();
                if (result != null)
                {
                    accountId = Convert.ToInt32(result);
                }
                
                if (accountId == 0)
                    return "";
                
                // Now get username from auth database
                using var authConnection = new MySqlConnection(_authConnectionString);
                authConnection.Open();
                
                string usernameQuery = @"
                    SELECT username 
                    FROM account 
                    WHERE id = @accountId";
                
                using var usernameCommand = new MySqlCommand(usernameQuery, authConnection);
                usernameCommand.Parameters.AddWithValue("@accountId", accountId);
                
                var usernameResult = usernameCommand.ExecuteScalar();
                return usernameResult?.ToString() ?? "";
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get account username: {ex.Message}");
            }
        }
        
        public List<PlayerInfo> GetCharactersByAccountId(int accountId)
        {
            var characters = new List<PlayerInfo>();
            
            try
            {
                using var connection = new MySqlConnection(_connectionString);
                connection.Open();
                
                string query = @"
                    SELECT c.name, c.race, c.class, c.level, c.zone, c.map, c.online, c.account 
                    FROM characters c 
                    WHERE c.account = @accountId 
                    ORDER BY c.name";
                
                using var command = new MySqlCommand(query, connection);
                command.Parameters.AddWithValue("@accountId", accountId);
                using var reader = command.ExecuteReader();
                
                var accountIds = new List<int>();
                var playerDict = new Dictionary<int, PlayerInfo>();
                
                while (reader.Read())
                {
                    int accId = reader.GetInt32("account");
                    var player = new PlayerInfo
                    {
                        Name = reader.GetString("name"),
                        Race = GetRaceName(reader.GetInt32("race")),
                        Class = GetClassName(reader.GetInt32("class")),
                        Level = reader.GetInt32("level"),
                        Zone = GetZoneName(reader.GetInt32("zone")),
                        MapId = reader.GetInt32("map"),
                        IsOnline = reader.GetBoolean("online"),
                        IPAddress = "",
                        AccountId = accId
                    };
                    
                    characters.Add(player);
                    accountIds.Add(accId);
                    playerDict[accId] = player;
                }
                
                // Now fetch IP addresses from auth database
                if (accountIds.Count > 0)
                {
                    FetchIPAddresses(accountIds, playerDict);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get characters for account: {ex.Message}");
            }
            
            return characters;
        }
        
        public DataTable ExecuteQuery(string query)
        {
            var dataTable = new DataTable();
            
            try
            {
                using var connection = new MySqlConnection(_connectionString);
                connection.Open();
                
                using var command = new MySqlCommand(query, connection);
                using var adapter = new MySqlDataAdapter(command);
                adapter.Fill(dataTable);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to execute query: {ex.Message}");
            }
            
            return dataTable;
        }
        
        public int ExecuteNonQuery(string query)
        {
            try
            {
                using var connection = new MySqlConnection(_connectionString);
                connection.Open();
                
                using var command = new MySqlCommand(query, connection);
                return command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to execute non-query: {ex.Message}");
            }
        }
        
        public void SetGMLevel(string username, int gmLevel)
        {
            try
            {
                using var connection = new MySqlConnection(_authConnectionString);
                connection.Open();
                
                // First get the account id
                string getIdQuery = "SELECT id FROM account WHERE username = @username";
                using var getIdCommand = new MySqlCommand(getIdQuery, connection);
                getIdCommand.Parameters.AddWithValue("@username", username);
                var result = getIdCommand.ExecuteScalar();
                
                if (result == null)
                {
                    throw new Exception("Account not found");
                }
                
                int accountId = Convert.ToInt32(result);
                
                // Check if account_access entry exists
                string checkQuery = "SELECT COUNT(*) FROM account_access WHERE id = @id";
                using var checkCommand = new MySqlCommand(checkQuery, connection);
                checkCommand.Parameters.AddWithValue("@id", accountId);
                int count = Convert.ToInt32(checkCommand.ExecuteScalar());
                
                if (count > 0)
                {
                    // Update existing entry
                    string updateQuery = "UPDATE account_access SET gmlevel = @gmlevel WHERE id = @id";
                    using var updateCommand = new MySqlCommand(updateQuery, connection);
                    updateCommand.Parameters.AddWithValue("@gmlevel", gmLevel);
                    updateCommand.Parameters.AddWithValue("@id", accountId);
                    updateCommand.ExecuteNonQuery();
                }
                else
                {
                    // Insert new entry if setting to GM
                    if (gmLevel > 0)
                    {
                        string insertQuery = "INSERT INTO account_access (id, gmlevel, realmid) VALUES (@id, @gmlevel, -1)";
                        using var insertCommand = new MySqlCommand(insertQuery, connection);
                        insertCommand.Parameters.AddWithValue("@id", accountId);
                        insertCommand.Parameters.AddWithValue("@gmlevel", gmLevel);
                        insertCommand.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to set GM level: {ex.Message}");
            }
        }
        
        public List<AuctionItem> GetAllAuctions()
        {
            var auctions = new List<AuctionItem>();
            try
            {
                using var connection = new MySqlConnection(_connectionString);
                connection.Open();
                
                string query = @"
                    SELECT ah.id, ah.itemguid, ah.buyoutprice, ah.startbid, ah.time, ah.lastbid,
                           c.name as seller
                    FROM auctionhouse ah
                    LEFT JOIN characters c ON ah.itemowner = c.guid
                    ORDER BY ah.id";
                
                using var command = new MySqlCommand(query, connection);
                using var reader = command.ExecuteReader();
                
                while (reader.Read())
                {
                    int itemGuid = reader.GetInt32("itemguid");
                    string itemName = $"Item GUID {itemGuid}"; // Will need item_instance lookup for names
                    
                    auctions.Add(new AuctionItem
                    {
                        Id = reader.GetInt32("id"),
                        ItemName = itemName,
                        Seller = reader.IsDBNull("seller") ? "Unknown" : reader.GetString("seller"),
                        Buyout = reader.GetInt32("buyoutprice"),
                        Bid = reader.GetInt32("startbid"),
                        TimeLeft = reader.GetInt32("time").ToString()
                    });
                }
                
                return auctions;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get auctions: {ex.Message}");
            }
        }
        
        public List<MailItem> GetAllMail()
        {
            var mails = new List<MailItem>();
            try
            {
                using var connection = new MySqlConnection(_connectionString);
                connection.Open();
                
                string query = @"
                    SELECT m.id, m.sender, m.receiver, m.subject, m.has_items, m.money, m.deliver_time, m.stationery, m.messageType,
                           sc.name as sender_name, rc.name as receiver_name,
                           qms.RewardMailSenderEntry
                    FROM mail m
                    LEFT JOIN characters sc ON m.sender = sc.guid
                    LEFT JOIN characters rc ON m.receiver = rc.guid
                    LEFT JOIN acore_world.quest_mail_sender qms ON m.sender = qms.RewardMailSenderEntry
                    ORDER BY m.id DESC LIMIT 500";
                
                using var command = new MySqlCommand(query, connection);
                using var reader = command.ExecuteReader();
                
                while (reader.Read())
                {
                    int sender = reader.GetInt32("sender");
                    int receiver = reader.GetInt32("receiver");
                    int stationery = reader.GetInt32("stationery");
                    int messageType = reader.GetInt32("messageType");
                    
                    // Handle sender name based on message type
                    string senderName = string.Empty;
                    
                    if (messageType == 2)
                    {
                        // Auction House
                        senderName = sender switch
                        {
                            2 => "Alliance AH",
                            6 => "Horde AH",
                            7 => "Neutral AH",
                            _ => "Auction House"
                        };
                    }
                    else if (messageType == 3)
                    {
                        // Creature - show GUID for now (need creature table fix)
                        senderName = $"Creature {sender}";
                    }
                    else
                    {
                        // Normal mail or other
                        senderName = reader.IsDBNull("sender_name") ? string.Empty : reader.GetString("sender_name");
                        
                        // Check for quest reward sender
                        if (string.IsNullOrEmpty(senderName) && !reader.IsDBNull("RewardMailSenderEntry"))
                        {
                            senderName = $"Quest Reward (ID: {reader.GetInt32("RewardMailSenderEntry")})";
                        }
                        
                        // Fallback to GUID
                        if (string.IsNullOrEmpty(senderName))
                        {
                            senderName = $"GUID {sender}";
                        }
                    }
                    
                    // Handle receiver name
                    string receiverName = reader.IsDBNull("receiver_name") ? $"GUID {receiver}" : reader.GetString("receiver_name");
                    
                    // Handle subject (parse AH mail data)
                    string subject = reader.IsDBNull("subject") ? "(No Subject)" : reader.GetString("subject");
                    if (stationery == 62 && subject.Contains(':'))
                    {
                        // Auction House mail - parse the item ID
                        string[] parts = subject.Split(':');
                        if (parts.Length > 0 && int.TryParse(parts[0], out int itemId))
                        {
                            subject = $"AH Item ID: {itemId}";
                        }
                    }
                    
                    DateTime deliverTime = DateTime.MinValue;
                    try
                    {
                        deliverTime = reader.GetDateTime("deliver_time");
                    }
                    catch
                    {
                        // Invalid date, use MinValue
                    }
                    
                    mails.Add(new MailItem
                    {
                        Id = reader.GetInt32("id"),
                        Sender = senderName,
                        Receiver = receiverName,
                        Subject = subject,
                        HasItems = reader.GetInt32("has_items") == 1,
                        Gold = reader.GetInt32("money"),
                        Date = deliverTime
                    });
                }
                
                return mails;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get mail: {ex.Message}");
            }
        }
        
        public List<CurrencyData> GetAllCurrency()
        {
            var currencies = new List<CurrencyData>();
            try
            {
                using var connection = new MySqlConnection(_connectionString);
                connection.Open();
                
                string query = @"
                    SELECT c.name, c.money
                    FROM characters c
                    WHERE c.online = 1
                    ORDER BY c.name";
                
                using var command = new MySqlCommand(query, connection);
                using var reader = command.ExecuteReader();
                
                while (reader.Read())
                {
                    currencies.Add(new CurrencyData
                    {
                        Character = reader.GetString("name"),
                        HonorPoints = 0, // Would need character_stats table
                        ArenaPoints = 0, // Would need character_stats table
                        Gold = reader.GetInt32("money") / 10000 // Convert to gold
                    });
                }
                
                return currencies;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get currency data: {ex.Message}");
            }
        }
        
        public List<MailTemplate> GetAllMailTemplates()
        {
            var templates = new List<MailTemplate>();
            try
            {
                using var connection = new MySqlConnection(_connectionString);
                connection.Open();
                
                string query = "SELECT id, moneyA, moneyH, subject, body, active FROM mail_server_template ORDER BY id";
                
                using var command = new MySqlCommand(query, connection);
                using var reader = command.ExecuteReader();
                
                while (reader.Read())
                {
                    templates.Add(new MailTemplate
                    {
                        Id = reader.GetInt32("id"),
                        MoneyA = reader.GetInt32("moneyA"),
                        MoneyH = reader.GetInt32("moneyH"),
                        Subject = reader.GetString("subject"),
                        Body = reader.GetString("body"),
                        Active = reader.GetInt32("active") == 1
                    });
                }
                
                return templates;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get mail templates: {ex.Message}");
            }
        }
        
        public List<MailTemplateCondition> GetMailTemplateConditions(int templateId)
        {
            var conditions = new List<MailTemplateCondition>();
            try
            {
                using var connection = new MySqlConnection(_connectionString);
                connection.Open();
                
                string query = "SELECT id, templateID, conditionType, conditionValue, conditionState FROM mail_server_template_conditions WHERE templateID = @templateId";
                
                using var command = new MySqlCommand(query, connection);
                command.Parameters.AddWithValue("@templateId", templateId);
                using var reader = command.ExecuteReader();
                
                while (reader.Read())
                {
                    conditions.Add(new MailTemplateCondition
                    {
                        Id = reader.GetInt32("id"),
                        TemplateId = reader.GetInt32("templateID"),
                        ConditionType = reader.GetString("conditionType"),
                        ConditionValue = reader.GetInt32("conditionValue"),
                        ConditionState = reader.GetInt32("conditionState")
                    });
                }
                
                return conditions;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get mail template conditions: {ex.Message}");
            }
        }
        
        public List<MailTemplateItem> GetMailTemplateItems(int templateId)
        {
            var items = new List<MailTemplateItem>();
            try
            {
                using var connection = new MySqlConnection(_connectionString);
                connection.Open();
                
                string query = "SELECT id, templateID, faction, item, itemCount FROM mail_server_template_items WHERE templateID = @templateId";
                
                using var command = new MySqlCommand(query, connection);
                command.Parameters.AddWithValue("@templateId", templateId);
                using var reader = command.ExecuteReader();
                
                while (reader.Read())
                {
                    items.Add(new MailTemplateItem
                    {
                        Id = reader.GetInt32("id"),
                        TemplateId = reader.GetInt32("templateID"),
                        Faction = reader.GetString("faction"),
                        Item = reader.GetInt32("item"),
                        ItemCount = reader.GetInt32("itemCount")
                    });
                }
                
                return items;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get mail template items: {ex.Message}");
            }
        }
        
        public void SendMailToPlayer(string playerName, string subject, string body, int money, List<int> itemGuids)
        {
            try
            {
                using var connection = new MySqlConnection(_connectionString);
                connection.Open();
                
                // Get player GUID
                string getGuidQuery = "SELECT guid FROM characters WHERE name = @name";
                using var getGuidCommand = new MySqlCommand(getGuidQuery, connection);
                getGuidCommand.Parameters.AddWithValue("@name", playerName);
                var result = getGuidCommand.ExecuteScalar();
                
                if (result == null)
                {
                    throw new Exception("Player not found");
                }
                
                int playerGuid = Convert.ToInt32(result);
                
                // Insert mail
                string insertMailQuery = @"
                    INSERT INTO mail (messageType, stationery, mailTemplateId, sender, receiver, subject, body, has_items, expire_time, deliver_time, money, cod, checked)
                    VALUES (0, 41, 0, 0, @receiver, @subject, @body, @hasItems, UNIX_TIMESTAMP() + 259200, UNIX_TIMESTAMP(), @money, 0, 0)";
                
                using var insertMailCommand = new MySqlCommand(insertMailQuery, connection);
                insertMailCommand.Parameters.AddWithValue("@receiver", playerGuid);
                insertMailCommand.Parameters.AddWithValue("@subject", subject);
                insertMailCommand.Parameters.AddWithValue("@body", body);
                insertMailCommand.Parameters.AddWithValue("@hasItems", itemGuids.Count > 0 ? 1 : 0);
                insertMailCommand.Parameters.AddWithValue("@money", money);
                insertMailCommand.ExecuteNonQuery();
                
                // Get the inserted mail ID
                long mailId = insertMailCommand.LastInsertedId;
                
                // Insert mail items if any
                if (itemGuids.Count > 0)
                {
                    foreach (var itemGuid in itemGuids)
                    {
                        string insertItemQuery = "INSERT INTO mail_items (mail_id, item_guid, receiver) VALUES (@mailId, @itemGuid, @receiver)";
                        using var insertItemCommand = new MySqlCommand(insertItemQuery, connection);
                        insertItemCommand.Parameters.AddWithValue("@mailId", mailId);
                        insertItemCommand.Parameters.AddWithValue("@itemGuid", itemGuid);
                        insertItemCommand.Parameters.AddWithValue("@receiver", playerGuid);
                        insertItemCommand.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to send mail: {ex.Message}");
            }
        }
        
        public void SendMailToAll(string subject, string body, int money, List<int> itemGuids)
        {
            try
            {
                using var connection = new MySqlConnection(_connectionString);
                connection.Open();
                
                // Get all player GUIDs
                string getPlayersQuery = "SELECT guid FROM characters";
                using var getPlayersCommand = new MySqlCommand(getPlayersQuery, connection);
                using var reader = getPlayersCommand.ExecuteReader();
                
                var playerGuids = new List<int>();
                while (reader.Read())
                {
                    playerGuids.Add(reader.GetInt32("guid"));
                }
                reader.Close();
                
                // Send mail to each player
                foreach (var playerGuid in playerGuids)
                {
                    string insertMailQuery = @"
                        INSERT INTO mail (messageType, stationery, mailTemplateId, sender, receiver, subject, body, has_items, expire_time, deliver_time, money, cod, checked)
                        VALUES (0, 41, 0, 0, @receiver, @subject, @body, @hasItems, UNIX_TIMESTAMP() + 259200, UNIX_TIMESTAMP(), @money, 0, 0)";
                    
                    using var insertMailCommand = new MySqlCommand(insertMailQuery, connection);
                    insertMailCommand.Parameters.AddWithValue("@receiver", playerGuid);
                    insertMailCommand.Parameters.AddWithValue("@subject", subject);
                    insertMailCommand.Parameters.AddWithValue("@body", body);
                    insertMailCommand.Parameters.AddWithValue("@hasItems", itemGuids.Count > 0 ? 1 : 0);
                    insertMailCommand.Parameters.AddWithValue("@money", money);
                    insertMailCommand.ExecuteNonQuery();
                    
                    long mailId = insertMailCommand.LastInsertedId;
                    
                    if (itemGuids.Count > 0)
                    {
                        foreach (var itemGuid in itemGuids)
                        {
                            string insertItemQuery = "INSERT INTO mail_items (mail_id, item_guid, receiver) VALUES (@mailId, @itemGuid, @receiver)";
                            using var insertItemCommand = new MySqlCommand(insertItemQuery, connection);
                            insertItemCommand.Parameters.AddWithValue("@mailId", mailId);
                            insertItemCommand.Parameters.AddWithValue("@itemGuid", itemGuid);
                            insertItemCommand.Parameters.AddWithValue("@receiver", playerGuid);
                            insertItemCommand.ExecuteNonQuery();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to send mail to all: {ex.Message}");
            }
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
        public string IPAddress { get; set; } = "";
        public int AccountId { get; set; }
    }
    
    public class IPBan
    {
        public string IP { get; set; } = string.Empty;
        public long BannedAt { get; set; }
        public string BannedBy { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        
        public string BannedAtFormatted => DateTimeOffset.FromUnixTimeSeconds(BannedAt).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
    }
    
    public class AccountBanHistory
    {
        public int AccountId { get; set; }
        public long BanDate { get; set; }
        public long UnbanDate { get; set; }
        public string BannedBy { get; set; } = string.Empty;
        public string BanReason { get; set; } = string.Empty;
        public bool Active { get; set; }
        
        public string BanDateFormatted => DateTimeOffset.FromUnixTimeSeconds(BanDate).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        public string UnbanDateFormatted => UnbanDate == 0 ? "Permanent" : DateTimeOffset.FromUnixTimeSeconds(UnbanDate).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        public string ActiveFormatted => Active ? "Active" : "Inactive";
    }
    
    public class IPBanHistory
    {
        public string IP { get; set; } = string.Empty;
        public long BanDate { get; set; }
        public long UnbanDate { get; set; }
        public string BannedBy { get; set; } = string.Empty;
        public string BanReason { get; set; } = string.Empty;
        
        public string BanDateFormatted => DateTimeOffset.FromUnixTimeSeconds(BanDate).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        public string UnbanDateFormatted => UnbanDate == 0 ? "Permanent" : DateTimeOffset.FromUnixTimeSeconds(UnbanDate).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
    }
    
    public class AccountInfo
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public DateTime LastLogin { get; set; }
        public bool Locked { get; set; }
        public int Expansion { get; set; }
        public DateTime JoinDate { get; set; }
        public string LastIP { get; set; } = string.Empty;
        public bool Online { get; set; }
        public int TotalTime { get; set; }
        public int FailedLogins { get; set; }
        public int GMLevel { get; set; }
    }
    
    public class LoginHistory
    {
        public DateTime LoginTime { get; set; }
        public string IPAddress { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string Error { get; set; } = string.Empty;
    }
    
    public class AuctionItem
    {
        public int Id { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string Seller { get; set; } = string.Empty;
        public int Buyout { get; set; }
        public int Bid { get; set; }
        public string TimeLeft { get; set; } = string.Empty;
    }
    
    public class MailItem
    {
        public int Id { get; set; }
        public string Sender { get; set; } = string.Empty;
        public string Receiver { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public bool HasItems { get; set; }
        public int Gold { get; set; }
        public DateTime Date { get; set; }
    }
    
    public class CurrencyData
    {
        public string Character { get; set; } = string.Empty;
        public int HonorPoints { get; set; }
        public int ArenaPoints { get; set; }
        public int Gold { get; set; }
    }
    
    public class MailTemplate
    {
        public int Id { get; set; }
        public int MoneyA { get; set; }
        public int MoneyH { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public bool Active { get; set; }
    }
    
    public class MailTemplateCondition
    {
        public int Id { get; set; }
        public int TemplateId { get; set; }
        public string ConditionType { get; set; } = string.Empty;
        public int ConditionValue { get; set; }
        public int ConditionState { get; set; }
    }
    
    public class MailTemplateItem
    {
        public int Id { get; set; }
        public int TemplateId { get; set; }
        public string Faction { get; set; } = string.Empty;
        public int Item { get; set; }
        public int ItemCount { get; set; }
    }
    
    public class ConfigParser
    {
        public static List<ConfigSection> ParseConfigFile(string filePath)
        {
            var sections = new List<ConfigSection>();
            var currentSection = new ConfigSection { Name = "General" };
            var lines = System.IO.File.ReadAllLines(filePath);
            
            string lastComment = string.Empty;
            
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                
                // Skip empty lines
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                
                // Track comments as potential descriptions
                if (line.StartsWith("#") || line.StartsWith("//"))
                {
                    lastComment = line.TrimStart('#', '/').Trim();
                    continue;
                }
                
                // Section headers
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    if (currentSection.Settings.Any())
                    {
                        sections.Add(currentSection);
                    }
                    currentSection = new ConfigSection { Name = line.Trim('[', ']') };
                    lastComment = string.Empty;
                    continue;
                }
                
                // Parse key = value
                if (line.Contains("="))
                {
                    var parts = line.Split(new[] { '=' }, 2);
                    if (parts.Length == 2)
                    {
                        var key = parts[0].Trim();
                        var value = parts[1].Trim();
                        
                        // Check if it's standard format
                        bool isStandard = !value.Contains(",") && !value.Contains("\"") && !value.Contains("'");
                        
                        currentSection.Settings.Add(new ConfigSetting
                        {
                            Key = key,
                            Value = value,
                            Description = string.IsNullOrEmpty(lastComment) ? "No description available" : lastComment,
                            OriginalLine = lines[i],
                            LineNumber = i,
                            IsStandardFormat = isStandard
                        });
                        
                        lastComment = string.Empty;
                    }
                }
                else
                {
                    // Non-standard format, add as text
                    currentSection.Settings.Add(new ConfigSetting
                    {
                        Key = line,
                        Value = line,
                        Description = string.IsNullOrEmpty(lastComment) ? "Non-standard config line" : lastComment,
                        OriginalLine = lines[i],
                        LineNumber = i,
                        IsStandardFormat = false
                    });
                    
                    lastComment = string.Empty;
                }
            }
            
            if (currentSection.Settings.Any())
            {
                sections.Add(currentSection);
            }
            
            return sections;
        }
        
        public static void SaveConfigFile(string filePath, List<ConfigSection> sections, string[] originalLines)
        {
            var newLines = (string[])originalLines.Clone();
            
            foreach (var section in sections)
            {
                foreach (var setting in section.Settings)
                {
                    if (setting.IsStandardFormat && setting.LineNumber < newLines.Length)
                    {
                        // Update the value while preserving the key and formatting
                        var originalLine = newLines[setting.LineNumber];
                        var parts = originalLine.Split(new[] { '=' }, 2);
                        if (parts.Length == 2)
                        {
                            newLines[setting.LineNumber] = $"{parts[0].Trim()}= {setting.Value}";
                        }
                    }
                }
            }
            
            System.IO.File.WriteAllLines(filePath, newLines);
        }
    }
    
    public class PlayerCountHistory
    {
        public DateTime Timestamp { get; set; }
        public int PlayerCount { get; set; }
        public int PeakThisHour { get; set; }
    }
    
    public class PeakHour
    {
        public int Hour { get; set; }
        public double AveragePlayers { get; set; }
        public int PeakPlayers { get; set; }
        public DateTime PeakTime { get; set; }
    }
    
    public class PerformanceMetric
    {
        public DateTime Time { get; set; }
        public double CpuUsage { get; set; }
        public double MemoryUsage { get; set; }
    }
    
    public class BroadcastMessage
    {
        public DateTime Time { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}
