using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;

namespace AzerothCoreLauncher
{
    public partial class PlayerDetailsModal : Window
    {
        public int CharacterGuid { get; set; }
        public string? CharacterName { get; set; }
        public int AccountId { get; set; }

        public ObservableCollection<InventoryItem> InventoryItems { get; set; }
        public ObservableCollection<SkillItem> SkillItems { get; set; }
        public ObservableCollection<QuestItem> QuestItems { get; set; }

        private DatabaseManager? _dbManager;
        private ItemCache? _itemCache;
        private SkillCache? _skillCache;

        public PlayerDetailsModal()
        {
            InitializeComponent();
            
            InventoryItems = new ObservableCollection<InventoryItem>();
            SkillItems = new ObservableCollection<SkillItem>();
            QuestItems = new ObservableCollection<QuestItem>();
            
            DgInventory.ItemsSource = InventoryItems;
            DgSkills.ItemsSource = SkillItems;
            DgQuests.ItemsSource = QuestItems;
        }

        public PlayerDetailsModal(int characterGuid, string characterName, int accountId, DatabaseManager dbManager, ItemCache itemCache, SkillCache skillCache) : this()
        {
            CharacterGuid = characterGuid;
            CharacterName = characterName;
            AccountId = accountId;
            _dbManager = dbManager;
            _itemCache = itemCache;
            _skillCache = skillCache;

            Title = $"Player Details - {CharacterName}";
            
            LoadCharacterData();
        }

        private void LoadCharacterData()
        {
            if (_dbManager == null) return;

            try
            {
                LoadProfile();
                LoadInventory();
                LoadSkills();
                LoadQuests();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load character data: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadProfile()
        {
            var query = $"SELECT c.guid, c.name, c.race, c.class, c.level, c.zone, c.map, c.online, c.totaltime, c.leveltime, c.account, c.money FROM characters c WHERE c.guid = '{CharacterGuid}'";
            var dataTable = _dbManager!.ExecuteQuery(query);

            if (dataTable.Rows.Count > 0)
            {
                var row = dataTable.Rows[0];
                TxtProfileName.Text = $"Name: {row["name"]}";
                TxtProfileLevel.Text = $"Level: {row["level"]}";
                TxtProfileRace.Text = $"Race: {GetRaceName(Convert.ToInt32(row["race"]))}";
                TxtProfileClass.Text = $"Class: {GetClassName(Convert.ToInt32(row["class"]))}";
                
                int zoneId = 0;
                if (row["zone"] != DBNull.Value && int.TryParse(row["zone"].ToString(), out zoneId))
                {
                    var zoneName = GetZoneName(zoneId);
                    TxtProfileZone.Text = $"Zone: {zoneName} (ID: {zoneId})";
                }
                else
                {
                    TxtProfileZone.Text = "Zone: Unknown";
                }
                
                TxtProfileMap.Text = $"Map: {row["map"]}";
                TxtProfileOnline.Text = $"Online: {(Convert.ToInt32(row["online"]) == 1 ? "Yes" : "No")}";
                TxtProfilePlaytime.Text = $"Total Playtime: {FormatPlaytime(Convert.ToInt32(row["totaltime"]))}";
                TxtProfileLevelTime.Text = $"Level Playtime: {FormatPlaytime(Convert.ToInt32(row["leveltime"]))}";
                
                // Load money (money is stored in copper, 100 copper = 1 silver, 100 silver = 1 gold)
                if (row["money"] != DBNull.Value)
                {
                    int copper = Convert.ToInt32(row["money"]);
                    int gold = copper / 10000;
                    int silver = (copper % 10000) / 100;
                    int remainingCopper = copper % 100;
                    
                    TxtProfileGold.Text = gold.ToString();
                    TxtProfileSilver.Text = silver.ToString();
                    TxtProfileCopper.Text = remainingCopper.ToString();
                }
                else
                {
                    TxtProfileGold.Text = "0";
                    TxtProfileSilver.Text = "0";
                    TxtProfileCopper.Text = "0";
                }
            }
        }

        private void LoadInventory()
        {
            try
            {
                var basePath = AppDomain.CurrentDomain.BaseDirectory;
                var dataPath = System.IO.Path.Combine(basePath, "data");
                var logPath = System.IO.Path.Combine(dataPath, "Debug.log");
                
                File.AppendAllText(logPath, $"LoadInventory called for CharacterGuid: {CharacterGuid}\n");
                File.AppendAllText(logPath, $"_itemCache is null: {_itemCache == null}\n");
                
                var query = $"SELECT ci.bag, ci.slot, ci.item, ii.itemEntry, ii.count, ii.durability, ii.randomPropertyId, ii.flags FROM character_inventory ci LEFT JOIN item_instance ii ON ci.item = ii.guid WHERE ci.guid = '{CharacterGuid}' ORDER BY ci.bag, ci.slot";
                var dataTable = _dbManager!.ExecuteQuery(query);
                
                File.AppendAllText(logPath, $"Query returned {dataTable.Rows.Count} rows\n");

                InventoryItems.Clear();

                foreach (System.Data.DataRow row in dataTable.Rows)
                {
                    int itemEntry = Convert.ToInt32(row["itemEntry"]);
                    string itemName = "Unknown";
                    int quality = 0;

                    if (_itemCache != null)
                    {
                        var item = _itemCache.GetItem(itemEntry);
                        if (item != null)
                        {
                            itemName = item.Name;
                            quality = item.Quality;
                        }
                        else
                        {
                            File.AppendAllText(logPath, $"Item not found in cache: {itemEntry}\n");
                        }
                    }

                    InventoryItems.Add(new InventoryItem
                    {
                        Slot = $"{row["bag"]}:{row["slot"]}",
                        ItemEntry = itemEntry,
                        ItemName = itemName,
                        Count = Convert.ToInt32(row["count"]),
                        Durability = Convert.ToInt32(row["durability"]),
                        Quality = quality
                    });
                }
                
                File.AppendAllText(logPath, $"Added {InventoryItems.Count} items to InventoryItems\n");
            }
            catch (Exception ex)
            {
                var basePath = AppDomain.CurrentDomain.BaseDirectory;
                var dataPath = System.IO.Path.Combine(basePath, "data");
                var logPath = System.IO.Path.Combine(dataPath, "Debug.log");
                File.AppendAllText(logPath, $"LoadInventory failed: {ex.Message}\n");
            }
        }

        private void LoadSkills()
        {
            var query = $"SELECT skill, value, max FROM character_skills WHERE guid = '{CharacterGuid}' ORDER BY skill";
            var dataTable = _dbManager!.ExecuteQuery(query);

            SkillItems.Clear();

            foreach (System.Data.DataRow row in dataTable.Rows)
            {
                int skillId = Convert.ToInt32(row["skill"]);
                string skillName = "Unknown";
                
                if (_skillCache != null)
                {
                    var skill = _skillCache.GetSkill(skillId);
                    if (skill != null)
                    {
                        skillName = skill.Name;
                    }
                }
                
                SkillItems.Add(new SkillItem
                {
                    SkillId = skillId,
                    SkillName = skillName,
                    Value = Convert.ToInt32(row["value"]),
                    Max = Convert.ToInt32(row["max"])
                });
            }

            DgSkills.ItemsSource = SkillItems;
        }

        private void LoadQuests()
        {
            var query = $"SELECT guid, quest, status, explored, timer, mobcount1, mobcount2, mobcount3, mobcount4, itemcount1, itemcount2, itemcount3, itemcount4, itemcount5, itemcount6, playercount FROM character_queststatus WHERE guid = '{CharacterGuid}' ORDER BY status DESC, quest";
            var dataTable = _dbManager!.ExecuteQuery(query);

            QuestItems.Clear();

            foreach (System.Data.DataRow row in dataTable.Rows)
            {
                QuestItems.Add(new QuestItem
                {
                    QuestId = Convert.ToInt32(row["quest"]),
                    Status = Convert.ToInt32(row["status"]),
                    Explored = Convert.ToInt32(row["explored"]),
                    MobCount1 = Convert.ToInt16(row["mobcount1"]),
                    MobCount2 = Convert.ToInt16(row["mobcount2"]),
                    MobCount3 = Convert.ToInt16(row["mobcount3"]),
                    MobCount4 = Convert.ToInt16(row["mobcount4"]),
                    ItemCount1 = Convert.ToInt16(row["itemcount1"]),
                    ItemCount2 = Convert.ToInt16(row["itemcount2"]),
                    ItemCount3 = Convert.ToInt16(row["itemcount3"]),
                    ItemCount4 = Convert.ToInt16(row["itemcount4"]),
                    ItemCount5 = Convert.ToInt16(row["itemcount5"]),
                    ItemCount6 = Convert.ToInt16(row["itemcount6"])
                });
            }
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
            return zoneId switch
            {
                0 => "Eastern Kingdoms",
                1 => "Kalimdor",
                530 => "Outland",
                571 => "Northrend",
                860 => "Pandaria",
                1116 => "Draenor",
                1220 => "Broken Isles",
                1642 => "Kul Tiras",
                1643 => "Zandalar",
                1519 => "Stormwind City",
                1637 => "Orgrimmar",
                1638 => "Ironforge",
                1497 => "Undercity",
                1657 => "Darnassus",
                1498 => "Thunder Bluff",
                1537 => "Silvermoon City",
                3557 => "Exodar",
                1499 => "Moonglade",
                440 => "Tanaris",
                85 => "Tirisfal Glades",
                10 => "Duskwood",
                11 => "Wetlands",
                12 => "Elwynn Forest",
                14 => "Arathi Highlands",
                15 => "Badlands",
                16 => "Blasted Lands",
                17 => "Teldrassil",
                28 => "Western Plaguelands",
                33 => "Stranglethorn Vale",
                36 => "Alterac Mountains",
                38 => "Loch Modan",
                40 => "Westfall",
                41 => "Deadwind Pass",
                44 => "Redridge Mountains",
                45 => "Arathi Basin",
                46 => "Burning Steppes",
                47 => "The Hinterlands",
                48 => "Searing Gorge",
                49 => "Un'Goro Crater",
                50 => "Silithus",
                51 => "Winterspring",
                139 => "Eastern Plaguelands",
                141 => "Teldrassil",
                148 => "Darkshore",
                215 => "Mulgore",
                267 => "Hillsbrad Foothills",
                331 => "Ashenvale",
                357 => "Feralas",
                400 => "Thousand Needles",
                405 => "Desolace",
                406 => "Stonetalon Mountains",
                490 => "Un'Goro Crater",
                618 => "Winterspring",
                1377 => "Silithus",
                3430 => "Eversong Woods",
                3487 => "Hellfire Peninsula",
                3518 => "Nagrand",
                3519 => "Terokkar Forest",
                3520 => "Zangarmarsh",
                3521 => "Blade's Edge Mountains",
                3522 => "Netherstorm",
                3523 => "Shadowmoon Valley",
                3524 => "Azuremyst Isle",
                3525 => "Bloodmyst Isle",
                3537 => "Borean Tundra",
                3711 => "Shattrath City",
                3703 => "Dalaran",
                4197 => "Wintergrasp",
                4395 => "Dalaran",
                4494 => "Icecrown",
                4710 => "The Ruby Sanctum",
                4812 => "Mount Hyjal",
                4813 => "Deepholm",
                4815 => "Uldum",
                4862 => "Tol Barad",
                5034 => "The Jade Forest",
                5104 => "Valley of the Four Winds",
                5135 => "Kun-Lai Summit",
                5145 => "Townlong Steppes",
                5165 => "Vale of Eternal Blossoms",
                5287 => "Isle of Thunder",
                5428 => "Timeless Isle",
                5785 => "Shadowmoon Valley",
                5791 => "Frostfire Ridge",
                5887 => "Tanaan Jungle",
                5905 => "Broken Shore",
                6308 => "Argus",
                6341 => "Nazjatar",
                6461 => "Mechagon",
                _ => $"Zone {zoneId}"
            };
        }

        private string FormatPlaytime(int seconds)
        {
            var span = TimeSpan.FromSeconds(seconds);
            return $"{span.Days}d {span.Hours}h {span.Minutes}m";
        }

        private void BtnFilterEquipment_Click(object sender, RoutedEventArgs e)
        {
            var filtered = InventoryItems.Where(item => item.Slot.StartsWith("0:") && Convert.ToInt32(item.Slot.Split(':')[1]) <= 18);
            DgInventory.ItemsSource = new ObservableCollection<InventoryItem>(filtered);
        }

        private void BtnFilterBackpack_Click(object sender, RoutedEventArgs e)
        {
            var filtered = InventoryItems.Where(item => item.Slot.StartsWith("0:") && Convert.ToInt32(item.Slot.Split(':')[1]) >= 19);
            DgInventory.ItemsSource = new ObservableCollection<InventoryItem>(filtered);
        }

        private void BtnFilterBank_Click(object sender, RoutedEventArgs e)
        {
            var filtered = InventoryItems.Where(item => !item.Slot.StartsWith("0:"));
            DgInventory.ItemsSource = new ObservableCollection<InventoryItem>(filtered);
        }

        private void BtnFilterAll_Click(object sender, RoutedEventArgs e)
        {
            DgInventory.ItemsSource = InventoryItems;
        }

        private void BtnFilterActiveQuests_Click(object sender, RoutedEventArgs e)
        {
            var filtered = QuestItems.Where(q => q.Status > 0);
            DgQuests.ItemsSource = new ObservableCollection<QuestItem>(filtered);
        }

        private void BtnFilterCompletedQuests_Click(object sender, RoutedEventArgs e)
        {
            var filtered = QuestItems.Where(q => q.Status == 0);
            DgQuests.ItemsSource = new ObservableCollection<QuestItem>(filtered);
        }

        private void BtnFilterAllQuests_Click(object sender, RoutedEventArgs e)
        {
            DgQuests.ItemsSource = QuestItems;
        }

        private void BtnSaveGold_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(TxtProfileGold.Text, out int gold) || gold < 0 ||
                !int.TryParse(TxtProfileSilver.Text, out int silver) || silver < 0 ||
                !int.TryParse(TxtProfileCopper.Text, out int copper) || copper < 0)
            {
                MessageBox.Show("Please enter valid amounts for gold, silver, and copper.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                // Convert gold, silver, copper to copper (1 gold = 10000 copper, 1 silver = 100 copper)
                int totalCopper = (gold * 10000) + (silver * 100) + copper;
                var query = $"UPDATE characters SET money = {totalCopper} WHERE guid = '{CharacterGuid}'";
                _dbManager!.ExecuteNonQuery(query);
                MessageBox.Show("Money amount updated successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to update money: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public class InventoryItem
    {
        public string Slot { get; set; } = string.Empty;
        public int ItemEntry { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public int Count { get; set; }
        public int Durability { get; set; }
        public int Quality { get; set; }
    }

    public class SkillItem
    {
        public int SkillId { get; set; }
        public string SkillName { get; set; } = string.Empty;
        public int Value { get; set; }
        public int Max { get; set; }
    }

    public class QuestItem
    {
        public int QuestId { get; set; }
        public int Status { get; set; }
        public int Explored { get; set; }
        public short MobCount1 { get; set; }
        public short MobCount2 { get; set; }
        public short MobCount3 { get; set; }
        public short MobCount4 { get; set; }
        public short ItemCount1 { get; set; }
        public short ItemCount2 { get; set; }
        public short ItemCount3 { get; set; }
        public short ItemCount4 { get; set; }
        public short ItemCount5 { get; set; }
        public short ItemCount6 { get; set; }
    }
}
