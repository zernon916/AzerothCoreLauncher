using System;

namespace AzerothCoreLauncher
{
    public class ItemData
    {
        public int Entry { get; set; }
        public string Name { get; set; } = string.Empty;
        public int DisplayId { get; set; }
        public int Quality { get; set; }
        public int InventoryType { get; set; }
        public int Stackable { get; set; }
        public int ItemLevel { get; set; }
        public int RequiredLevel { get; set; }
        public long BuyPrice { get; set; }
        public int SellPrice { get; set; }
        public int Armor { get; set; }
        public short? HolyRes { get; set; }
        public short? FireRes { get; set; }
        public short? NatureRes { get; set; }
        public short? FrostRes { get; set; }
        public short? ShadowRes { get; set; }
        public short? ArcaneRes { get; set; }
        public int SocketColor1 { get; set; }
        public int SocketContent1 { get; set; }
        public int SocketColor2 { get; set; }
        public int SocketContent2 { get; set; }
        public int SocketColor3 { get; set; }
        public int SocketContent3 { get; set; }
        public int SocketBonus { get; set; }
        public int SpellId1 { get; set; }
        public int SpellId2 { get; set; }
        public int SpellId3 { get; set; }
        public int SpellId4 { get; set; }
        public int SpellId5 { get; set; }
        public int MaxDurability { get; set; }
    }
}
