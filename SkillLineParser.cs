using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace AzerothCoreLauncher
{
    public class SkillLineParser
    {
        public static List<SkillLine> ParseSkillLineSql(string filePath)
        {
            var skillLines = new List<SkillLine>();
            var lines = File.ReadAllLines(filePath);
            
            var logPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(filePath), "Debug.log");
            File.WriteAllText(logPath, $"Parsing {filePath} with {lines.Length} lines\n");
            
            foreach (var line in lines)
            {
                if (line.StartsWith("INSERT INTO"))
                {
                    var skillLine = ParseSkillLineInsert(line, logPath);
                    if (skillLine != null)
                        skillLines.Add(skillLine);
                }
            }
            
            File.AppendAllText(logPath, $"Parsed {skillLines.Count} SkillLine records\n");
            return skillLines;
        }
        
        private static SkillLine ParseSkillLineInsert(string line, string logPath)
        {
            try
            {
                var match = Regex.Match(line, @"VALUES \((.+)\);");
                if (!match.Success)
                {
                    File.AppendAllText(logPath, $"No match for line: {line.Substring(0, Math.Min(100, line.Length))}\n");
                    return null;
                }
                
                var values = ParseValues(match.Groups[1].Value);
                
                // Log field count for first few lines
                File.AppendAllText(logPath, $"Field count: {values.Length}\n");
                if (values.Length < 10)
                {
                    File.AppendAllText(logPath, $"Fields: {string.Join(", ", values)}\n");
                }
                
                // SQL has 56 fields:
                // 0-2: ID, CategoryID, SkillCostsID
                // 3-18: DisplayName (16 languages)
                // 19: DisplayNameMask
                // 20-35: Description (16 languages)
                // 36: DescriptionMask
                // 37: SpellIconID
                // 38-53: AlternateVerb (16 languages)
                // 54: AlternateVerbMask
                // 55: CanLink
                
                if (values.Length < 56) return null;
                
                var skillLine = new SkillLine
                {
                    ID = int.Parse(values[0].Trim()),
                    CategoryID = int.Parse(values[1].Trim()),
                    SkillCostsID = int.Parse(values[2].Trim()),
                    DisplayNameMask = int.Parse(values[19].Trim()),
                    DescriptionMask = int.Parse(values[36].Trim()),
                    SpellIconID = int.Parse(values[37].Trim()),
                    AlternateVerbMask = int.Parse(values[54].Trim()),
                    CanLink = int.Parse(values[55].Trim()) != 0,
                    VerbatimSpellIcon = 0,
                    DefaultRace = 0,
                    DefaultClass = 0,
                    Flags = 0
                };
                
                // Parse display names (languages)
                var languages = new[] { "enUS", "enGB", "koKR", "frFR", "deDE", "enCN", "zhCN", "enTW", "zhTW", "esES", "esMX", "ruRU", "ptPT", "ptBR", "itIT", "Unk" };
                for (int i = 0; i < languages.Length && (3 + i) < values.Length; i++)
                {
                    var value = values[3 + i].Trim().Trim('"');
                    if (!string.IsNullOrEmpty(value))
                        skillLine.DisplayName[languages[i]] = value;
                }
                
                // Parse descriptions (languages)
                for (int i = 0; i < languages.Length && (20 + i) < values.Length; i++)
                {
                    var value = values[20 + i].Trim().Trim('"');
                    if (!string.IsNullOrEmpty(value))
                        skillLine.Description[languages[i]] = value;
                }
                
                // Parse alternate verbs (languages)
                for (int i = 0; i < languages.Length && (38 + i) < values.Length; i++)
                {
                    var value = values[38 + i].Trim().Trim('"');
                    if (!string.IsNullOrEmpty(value))
                        skillLine.AlternateVerb[languages[i]] = value;
                }
                
                return skillLine;
            }
            catch (Exception ex)
            {
                File.AppendAllText(logPath, $"Failed to parse SkillLine: {ex.Message}\n");
                File.AppendAllText(logPath, $"Line: {line.Substring(0, Math.Min(200, line.Length))}\n");
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
        
        public static void SaveSkillLineToJson(List<SkillLine> skillLines, string filePath)
        {
            var json = JsonConvert.SerializeObject(skillLines, Formatting.Indented);
            File.WriteAllText(filePath, json);
        }
        
        public static List<SkillLine> LoadSkillLineFromJson(string filePath)
        {
            if (!File.Exists(filePath))
                return new List<SkillLine>();
            
            var json = File.ReadAllText(filePath);
            return JsonConvert.DeserializeObject<List<SkillLine>>(json) ?? new List<SkillLine>();
        }
    }
}
