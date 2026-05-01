using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace AzerothCoreLauncher
{
    public class SkillLineAbilityParser
    {
        public static List<SkillLineAbility> ParseSkillLineAbilitySql(string filePath)
        {
            var skillLineAbilities = new List<SkillLineAbility>();
            var lines = File.ReadAllLines(filePath);
            
            foreach (var line in lines)
            {
                if (line.StartsWith("INSERT INTO"))
                {
                    var ability = ParseSkillLineAbilityInsert(line);
                    if (ability != null)
                        skillLineAbilities.Add(ability);
                }
            }
            
            return skillLineAbilities;
        }
        
        private static SkillLineAbility ParseSkillLineAbilityInsert(string line)
        {
            try
            {
                var match = Regex.Match(line, @"VALUES \((.+)\);");
                if (!match.Success) return null;
                
                var values = ParseValues(match.Groups[1].Value);
                
                if (values.Length < 13) return null;
                
                var ability = new SkillLineAbility
                {
                    ID = int.Parse(values[0].Trim()),
                    SkillLine = int.Parse(values[1].Trim()),
                    Spell = int.Parse(values[2].Trim()),
                    RaceMask = int.Parse(values[3].Trim()),
                    ClassMask = int.Parse(values[4].Trim()),
                    MinSkillLineRank = int.Parse(values[5].Trim()),
                    SupercededBySpell = int.Parse(values[6].Trim()),
                    AcquireMethod = int.Parse(values[7].Trim()),
                    TrivialSkillLineRankHigh = int.Parse(values[8].Trim()),
                    TrivialSkillLineRankLow = int.Parse(values[9].Trim()),
                    CharacterPoints_1 = int.Parse(values[10].Trim()),
                    CharacterPoints_2 = int.Parse(values[11].Trim()),
                    TradeSkillCategoryID = int.Parse(values[12].Trim())
                };
                
                return ability;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to parse SkillLineAbility: {ex.Message}");
                return null;
            }
        }
        
        private static string[] ParseValues(string valuesString)
        {
            var values = new List<string>();
            var currentValue = new StringBuilder();
            var inQuotes = false;
            
            for (int i = 0; i < valuesString.Length; i++)
            {
                char c = valuesString[i];
                
                if (c == '"' && (i == 0 || valuesString[i - 1] != '\\'))
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    values.Add(currentValue.ToString());
                    currentValue.Clear();
                }
                else
                {
                    currentValue.Append(c);
                }
            }
            
            values.Add(currentValue.ToString());
            return values.ToArray();
        }
        
        public static void SaveSkillLineAbilityToJson(List<SkillLineAbility> abilities, string filePath)
        {
            var json = JsonConvert.SerializeObject(abilities, Formatting.Indented);
            File.WriteAllText(filePath, json);
        }
        
        public static List<SkillLineAbility> LoadSkillLineAbilityFromJson(string filePath)
        {
            if (!File.Exists(filePath))
                return new List<SkillLineAbility>();
            
            var json = File.ReadAllText(filePath);
            return JsonConvert.DeserializeObject<List<SkillLineAbility>>(json) ?? new List<SkillLineAbility>();
        }
    }
}
