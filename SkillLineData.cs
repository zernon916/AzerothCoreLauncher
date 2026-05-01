using System.Collections.Generic;

namespace AzerothCoreLauncher
{
    public class SkillLine
    {
        public int ID { get; set; }
        public int CategoryID { get; set; }
        public int SkillCostsID { get; set; }
        public Dictionary<string, string> DisplayName { get; set; } = new Dictionary<string, string>();
        public int DisplayNameMask { get; set; }
        public Dictionary<string, string> Description { get; set; } = new Dictionary<string, string>();
        public int DescriptionMask { get; set; }
        public int SpellIconID { get; set; }
        public Dictionary<string, string> AlternateVerb { get; set; } = new Dictionary<string, string>();
        public int AlternateVerbMask { get; set; }
        public bool CanLink { get; set; }
        public int VerbatimSpellIcon { get; set; }
        public int DefaultRace { get; set; }
        public int DefaultClass { get; set; }
        public int Flags { get; set; }
    }
}
