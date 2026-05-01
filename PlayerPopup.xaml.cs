using System;
using System.Windows;

namespace AzerothCoreLauncher
{
    public partial class PlayerPopup : Window
    {
        public int CharacterGuid { get; set; }
        public string? CharacterName { get; set; }
        public int AccountId { get; set; }

        private DatabaseManager? _dbManager;
        private ItemCache? _itemCache;
        private SkillCache? _skillCache;

        public PlayerPopup()
        {
            InitializeComponent();
        }

        public PlayerPopup(int characterGuid, string characterName, int accountId, DatabaseManager dbManager, ItemCache itemCache, SkillCache skillCache) : this()
        {
            CharacterGuid = characterGuid;
            CharacterName = characterName;
            AccountId = accountId;
            _dbManager = dbManager;
            _itemCache = itemCache;
            _skillCache = skillCache;

            TxtPlayerName.Text = $"{CharacterName} - Loading...";
            
            LoadPlayerData();
        }

        private void LoadPlayerData()
        {
            if (_dbManager == null) return;

            try
            {
                // Query character profile
                var query = $"SELECT c.name, c.race, c.class, c.level, c.zone, c.online FROM characters c WHERE c.guid = '{CharacterGuid}'";
                var dataTable = _dbManager.ExecuteQuery(query);

                if (dataTable.Rows.Count > 0)
                {
                    var row = dataTable.Rows[0];
                    var raceName = GetRaceName(Convert.ToInt32(row["race"]));
                    var className = GetClassName(Convert.ToInt32(row["class"]));
                    
                    TxtPlayerName.Text = $"{CharacterName} - {raceName} {className}";
                    TxtLevel.Text = $"Level: {row["level"]}";
                    
                    int zoneId = 0;
                    if (row["zone"] != DBNull.Value && int.TryParse(row["zone"].ToString(), out zoneId))
                    {
                        TxtZone.Text = $"Zone: {GetZoneName(zoneId)}";
                    }
                    else
                    {
                        TxtZone.Text = "Zone: Unknown";
                    }
                    
                    bool isOnline = Convert.ToInt32(row["online"]) == 1;
                    TxtOnline.Text = $"Online: {(isOnline ? "●" : "○")}";
                    TxtOnline.Foreground = isOnline ? System.Windows.Media.Brushes.Green : System.Windows.Media.Brushes.Red;
                }

                // Query account info for IP (need to use auth connection)
                // For now, skip IP since we don't have separate auth connection in popup
                TxtIP.Text = "IP: Not available";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load player data: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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

        private void BtnKick_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Implement kick command
            MessageBox.Show("Kick command will be implemented", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnBan_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Implement ban dialog
            MessageBox.Show("Ban dialog will be implemented", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnDetails_Click(object sender, RoutedEventArgs e)
        {
            if (_dbManager != null && _itemCache != null && _skillCache != null)
            {
                var modal = new PlayerDetailsModal(CharacterGuid, CharacterName ?? "", AccountId, _dbManager, _itemCache, _skillCache)
                {
                    Owner = this
                };
                modal.ShowDialog();
            }
        }
    }
}
