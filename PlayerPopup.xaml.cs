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

        public PlayerPopup()
        {
            InitializeComponent();
        }

        public PlayerPopup(int characterGuid, string characterName, int accountId, DatabaseManager dbManager, ItemCache itemCache) : this()
        {
            CharacterGuid = characterGuid;
            CharacterName = characterName;
            AccountId = accountId;
            _dbManager = dbManager;
            _itemCache = itemCache;

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
                    TxtZone.Text = $"Zone: {row["zone"]}";
                    
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
            if (_dbManager != null && _itemCache != null)
            {
                var modal = new PlayerDetailsModal(CharacterGuid, CharacterName ?? "", AccountId, _dbManager, _itemCache)
                {
                    Owner = this
                };
                modal.ShowDialog();
            }
        }
    }
}
