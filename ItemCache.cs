using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AzerothCoreLauncher
{
    public class ItemCache
    {
        private Dictionary<int, ItemData> _cache = new Dictionary<int, ItemData>();
        private readonly string _cacheFilePath;

        public ItemCache(string cacheFilePath)
        {
            _cacheFilePath = cacheFilePath;
        }

        public bool Load()
        {
            try
            {
                if (!File.Exists(_cacheFilePath))
                {
                    return false;
                }

                var lines = File.ReadAllLines(_cacheFilePath);
                _cache.Clear();

                foreach (var line in lines.Skip(1)) // Skip header
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    var parts = line.Split(',');
                    if (parts.Length < 3)
                        continue;

                    var item = new ItemData
                    {
                        Entry = int.TryParse(parts[0].Trim('"'), out var entry) ? entry : 0,
                        Name = parts[1].Trim('"'),
                        DisplayId = int.TryParse(parts[2].Trim('"'), out var displayId) ? displayId : 0,
                        Quality = parts.Length > 3 && int.TryParse(parts[3].Trim('"'), out var quality) ? quality : 0,
                        InventoryType = parts.Length > 4 && int.TryParse(parts[4].Trim('"'), out var invType) ? invType : 0,
                        Stackable = parts.Length > 5 && int.TryParse(parts[5].Trim('"'), out var stackable) ? stackable : 1,
                        ItemLevel = parts.Length > 6 && int.TryParse(parts[6].Trim('"'), out var itemLevel) ? itemLevel : 0,
                        RequiredLevel = parts.Length > 7 && int.TryParse(parts[7].Trim('"'), out var reqLevel) ? reqLevel : 0,
                        BuyPrice = parts.Length > 8 && long.TryParse(parts[8].Trim('"'), out var buyPrice) ? buyPrice : 0,
                        SellPrice = parts.Length > 9 && int.TryParse(parts[9].Trim('"'), out var sellPrice) ? sellPrice : 0,
                        Armor = parts.Length > 10 && int.TryParse(parts[10].Trim('"'), out var armor) ? armor : 0,
                        HolyRes = parts.Length > 11 && short.TryParse(parts[11].Trim('"'), out var holyRes) ? holyRes : null,
                        FireRes = parts.Length > 12 && short.TryParse(parts[12].Trim('"'), out var fireRes) ? fireRes : null,
                        NatureRes = parts.Length > 13 && short.TryParse(parts[13].Trim('"'), out var natureRes) ? natureRes : null,
                        FrostRes = parts.Length > 14 && short.TryParse(parts[14].Trim('"'), out var frostRes) ? frostRes : null,
                        ShadowRes = parts.Length > 15 && short.TryParse(parts[15].Trim('"'), out var shadowRes) ? shadowRes : null,
                        ArcaneRes = parts.Length > 16 && short.TryParse(parts[16].Trim('"'), out var arcaneRes) ? arcaneRes : null,
                        SocketColor1 = parts.Length > 17 && int.TryParse(parts[17].Trim('"'), out var socket1) ? socket1 : 0,
                        SocketContent1 = parts.Length > 18 && int.TryParse(parts[18].Trim('"'), out var content1) ? content1 : 0,
                        SocketColor2 = parts.Length > 19 && int.TryParse(parts[19].Trim('"'), out var socket2) ? socket2 : 0,
                        SocketContent2 = parts.Length > 20 && int.TryParse(parts[20].Trim('"'), out var content2) ? content2 : 0,
                        SocketColor3 = parts.Length > 21 && int.TryParse(parts[21].Trim('"'), out var socket3) ? socket3 : 0,
                        SocketContent3 = parts.Length > 22 && int.TryParse(parts[22].Trim('"'), out var content3) ? content3 : 0,
                        SocketBonus = parts.Length > 23 && int.TryParse(parts[23].Trim('"'), out var bonus) ? bonus : 0,
                        SpellId1 = parts.Length > 24 && int.TryParse(parts[24].Trim('"'), out var spell1) ? spell1 : 0,
                        SpellId2 = parts.Length > 25 && int.TryParse(parts[25].Trim('"'), out var spell2) ? spell2 : 0,
                        SpellId3 = parts.Length > 26 && int.TryParse(parts[26].Trim('"'), out var spell3) ? spell3 : 0,
                        SpellId4 = parts.Length > 27 && int.TryParse(parts[27].Trim('"'), out var spell4) ? spell4 : 0,
                        SpellId5 = parts.Length > 28 && int.TryParse(parts[28].Trim('"'), out var spell5) ? spell5 : 0,
                        MaxDurability = parts.Length > 29 && int.TryParse(parts[29].Trim('"'), out var durability) ? durability : 0
                    };

                    if (item.Entry > 0)
                    {
                        _cache[item.Entry] = item;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ItemCache.Load() failed: {ex.Message}");
                return false;
            }
        }

        public ItemData? GetItem(int entry)
        {
            return _cache.TryGetValue(entry, out var item) ? item : null;
        }

        public int Count => _cache.Count;
    }
}
