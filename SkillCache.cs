using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace AzerothCoreLauncher
{
    public class SkillCache
    {
        private readonly string _jsonPath;
        private readonly Dictionary<int, SkillData> _cache;
        private bool _loaded;

        public SkillCache(string jsonPath = "")
        {
            if (string.IsNullOrEmpty(jsonPath))
            {
                var basePath = AppDomain.CurrentDomain.BaseDirectory;
                _jsonPath = System.IO.Path.Combine(basePath, "data", "SkillLine.json");
            }
            else
            {
                _jsonPath = jsonPath;
            }
            
            _cache = new Dictionary<int, SkillData>();
            _loaded = false;
        }

        public bool Load()
        {
            try
            {
                if (!File.Exists(_jsonPath))
                {
                    System.Diagnostics.Debug.WriteLine($"SkillCache: JSON file not found at {_jsonPath}");
                    return false;
                }

                var json = File.ReadAllText(_jsonPath);
                var skillLines = JsonConvert.DeserializeObject<List<SkillLine>>(json) ?? new List<SkillLine>();

                _cache.Clear();
                foreach (var skillLine in skillLines)
                {
                    string name = "Unknown";
                    if (skillLine.DisplayName != null && skillLine.DisplayName.ContainsKey("enUS"))
                    {
                        name = skillLine.DisplayName["enUS"];
                    }
                    else if (skillLine.DisplayName != null && skillLine.DisplayName.Count > 0)
                    {
                        name = skillLine.DisplayName.Values.First();
                    }

                    _cache[skillLine.ID] = new SkillData
                    {
                        Id = skillLine.ID,
                        Name = name
                    };
                }

                _loaded = true;
                System.Diagnostics.Debug.WriteLine($"SkillCache: Loaded {_cache.Count} skills from JSON");
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
