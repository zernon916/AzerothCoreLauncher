using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using Microsoft.Win32;

namespace AzerothCoreLauncher
{
    public partial class AccountPopup : Window
    {
        private readonly DatabaseManager _dbManager;
        private readonly string _characterName;
        private readonly string _accountName;
        private readonly Process? _worldProcess;
        
        public AccountPopup(DatabaseManager dbManager, string characterName, string accountName, Process? worldProcess)
        {
            InitializeComponent();
            _dbManager = dbManager;
            _characterName = characterName;
            _accountName = accountName;
            _worldProcess = worldProcess;
            
            // Load saved size
            LoadWindowSize();
            
            TxtAccountName.Text = accountName;
            
            BtnBanAccount.Click += BtnBanAccount_Click;
            BtnUnbanAccount.Click += BtnUnbanAccount_Click;
            BtnBanIP.Click += BtnBanIP_Click;
            BtnViewBanHistory.Click += BtnViewBanHistory_Click;
            
            LoadAccountInfo();
        }
        
        private void LoadWindowSize()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\AzerothCoreLauncher\AccountPopup");
                if (key != null)
                {
                    var width = key.GetValue("Width") as int?;
                    var height = key.GetValue("Height") as int?;
                    
                    if (width.HasValue && height.HasValue)
                    {
                        Width = width.Value;
                        Height = height.Value;
                    }
                }
            }
            catch
            {
                // Ignore errors, use default size
            }
        }
        
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            base.OnClosing(e);
            SaveWindowSize();
        }
        
        private void SaveWindowSize()
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(@"Software\AzerothCoreLauncher\AccountPopup");
                key?.SetValue("Width", (int)Width);
                key?.SetValue("Height", (int)Height);
            }
            catch
            {
                // Ignore errors
            }
        }
        
        private void LoadAccountInfo()
        {
            try
            {
                // Get player info to get IP address and account ID
                var players = _dbManager.SearchPlayers(_characterName);
                string ipAddress = "";
                int accountId = 0;
                
                if (players.Count > 0)
                {
                    ipAddress = players[0].IPAddress;
                    accountId = players[0].AccountId;
                }
                
                TxtIPAddress.Text = ipAddress;
                
                // Check if IP is banned and update button text
                if (!string.IsNullOrEmpty(ipAddress))
                {
                    bool isBanned = _dbManager.IsIPBanned(ipAddress);
                    BtnBanIP.Content = isBanned ? "Unban IP Address" : "Ban IP Address";
                }
                
                // Get last login from auth database
                var accountInfo = _dbManager.GetAccountInfo(_accountName);
                if (accountInfo != null)
                {
                    TxtLastLogin.Text = accountInfo.LastLogin.ToString("yyyy-MM-dd HH:mm:ss");
                }
                
                // Load characters
                var characters = _dbManager.GetCharactersByAccountId(accountId);
                DgCharacters.ItemsSource = characters;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load account info: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void BtnBanAccount_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var popup = new BanAccountPopup(_accountName, _characterName, _worldProcess);
                popup.Owner = this;
                var result = popup.ShowDialog();
                
                if (result == true)
                {
                    // Ban was successful, refresh the ban history
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to ban account: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void BtnUnbanAccount_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Send console command
                string command = $"unban account {_accountName}";
                _worldProcess?.StandardInput.WriteLine(command);
                
                MessageBox.Show($"Account {_accountName} has been unbanned", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to unban account: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void BtnBanIP_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string ipAddress = TxtIPAddress.Text;
                if (string.IsNullOrEmpty(ipAddress))
                {
                    MessageBox.Show("No IP address to ban/unban", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                bool isBanned = _dbManager.IsIPBanned(ipAddress);
                
                if (isBanned)
                {
                    _dbManager.UnbanIP(ipAddress);
                    MessageBox.Show($"IP {ipAddress} has been unbanned", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    BtnBanIP.Content = "Ban IP Address";
                }
                else
                {
                    _dbManager.BanIP(ipAddress, "Banned from launcher", "Launcher");
                    MessageBox.Show($"IP {ipAddress} has been banned", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    BtnBanIP.Content = "Unban IP Address";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to ban/unban IP: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
        
        private void BtnViewBanHistory_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Show account ban history
                var popup = new BanHistoryPopup(_dbManager, _accountName, true);
                popup.Owner = this;
                popup.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open ban history: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
