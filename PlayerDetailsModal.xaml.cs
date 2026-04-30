using System;
using System.Collections.ObjectModel;
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

        public PlayerDetailsModal(int characterGuid, string characterName, int accountId, DatabaseManager dbManager, ItemCache itemCache) : this()
        {
            CharacterGuid = characterGuid;
            CharacterName = characterName;
            AccountId = accountId;
            _dbManager = dbManager;
            _itemCache = itemCache;

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
            var query = $"SELECT c.guid, c.name, c.race, c.class, c.level, c.zone, c.map, c.online, c.totaltime, c.leveltime, c.account FROM characters c WHERE c.guid = '{CharacterGuid}'";
            var dataTable = _dbManager!.ExecuteQuery(query);

            if (dataTable.Rows.Count > 0)
            {
                var row = dataTable.Rows[0];
                TxtProfileName.Text = row["name"].ToString();
                TxtProfileLevel.Text = row["level"].ToString();
                TxtProfileRace.Text = GetRaceName(Convert.ToInt32(row["race"]));
                TxtProfileClass.Text = GetClassName(Convert.ToInt32(row["class"]));
                TxtProfileZone.Text = row["zone"].ToString();
                TxtProfileMap.Text = row["map"].ToString();
                TxtProfileOnline.Text = Convert.ToInt32(row["online"]) == 1 ? "Yes" : "No";
                TxtProfilePlaytime.Text = FormatPlaytime(Convert.ToInt32(row["totaltime"]));
                TxtProfileLevelTime.Text = FormatPlaytime(Convert.ToInt32(row["leveltime"]));
            }
        }

        private void LoadInventory()
        {
            var query = $"SELECT ci.bag, ci.slot, ci.item, ii.itemEntry, ii.count, ii.durability, ii.randomPropertyId, ii.flags FROM character_inventory ci LEFT JOIN item_instance ii ON ci.item = ii.guid WHERE ci.guid = '{CharacterGuid}' ORDER BY ci.bag, ci.slot";
            var dataTable = _dbManager!.ExecuteQuery(query);

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
        }

        private void LoadSkills()
        {
            var query = $"SELECT skill, value, max FROM character_skills WHERE guid = '{CharacterGuid}' ORDER BY skill";
            var dataTable = _dbManager!.ExecuteQuery(query);

            SkillItems.Clear();

            foreach (System.Data.DataRow row in dataTable.Rows)
            {
                SkillItems.Add(new SkillItem
                {
                    SkillId = Convert.ToInt32(row["skill"]),
                    Value = Convert.ToInt32(row["value"]),
                    Max = Convert.ToInt32(row["max"])
                });
            }
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
