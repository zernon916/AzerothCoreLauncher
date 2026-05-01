using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Windows;

namespace AzerothCoreLauncher
{
    public class SkillCache
    {
        private readonly string _worldConnectionString;
        private readonly Dictionary<int, SkillData> _cache;
        private bool _loaded;

        public SkillCache(string host, string port, string worldDatabase, string user, string password)
        {
            _worldConnectionString = $"Server={host};Port={port};Database={worldDatabase};User={user};Password={password};";
            _cache = new Dictionary<int, SkillData>();
            _loaded = false;
        }

        public bool Load()
        {
            try
            {
                using var connection = new MySqlConnection(_worldConnectionString);
                connection.Open();

                string query = "SELECT ID, DisplayName_Lang_enUS FROM skillline_dbc";
                using var command = new MySqlCommand(query, connection);
                using var reader = command.ExecuteReader();

                _cache.Clear();
                while (reader.Read())
                {
                    int id = reader.GetInt32("ID");
                    string name = reader.IsDBNull("DisplayName_Lang_enUS") ? "Unknown" : reader.GetString("DisplayName_Lang_enUS");

                    _cache[id] = new SkillData
                    {
                        Id = id,
                        Name = name
                    };
                }

                _loaded = true;
                System.Diagnostics.Debug.WriteLine($"SkillCache: Loaded {_cache.Count} skills from skillline_dbc");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SkillCache: Failed to load - {ex.Message}");
                return false;
            }
        }

        public SkillData? GetSkill(int skillId)
        {
            if (!_loaded)
            {
                Load();
            }

            return _cache.TryGetValue(skillId, out var skill) ? skill : null;
        }

        public int Count => _cache.Count;
    }
}
