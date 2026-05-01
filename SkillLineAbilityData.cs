namespace AzerothCoreLauncher
{
    public class SkillLineAbility
    {
        public int ID { get; set; }
        public int SkillLine { get; set; }
        public int Spell { get; set; }
        public int RaceMask { get; set; }
        public int ClassMask { get; set; }
        public int MinSkillLineRank { get; set; }
        public int SupercededBySpell { get; set; }
        public int AcquireMethod { get; set; }
        public int TrivialSkillLineRankHigh { get; set; }
        public int TrivialSkillLineRankLow { get; set; }
        public int CharacterPoints_1 { get; set; }
        public int CharacterPoints_2 { get; set; }
        public int TradeSkillCategoryID { get; set; }
    }
}
