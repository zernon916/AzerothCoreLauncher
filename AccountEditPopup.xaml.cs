using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using Microsoft.Win32;

namespace AzerothCoreLauncher
{
    public partial class AccountEditPopup : Window
    {
        private readonly DatabaseManager _dbManager;
        private readonly string _accountName;
        private readonly Process? _worldProcess;
        
        public AccountEditPopup(DatabaseManager dbManager, string accountName, Process? worldProcess)
        {
            InitializeComponent();
            _dbManager = dbManager;
            _accountName = accountName;
            _worldProcess = worldProcess;
            
            // Load saved size
            LoadWindowSize();
            
            TxtAccountName.Text = accountName;
            
            BtnChangePassword.Click += BtnChangePassword_Click;
            BtnSaveEmail.Click += BtnSaveEmail_Click;
            BtnBanAccount.Click += BtnBanAccount_Click;
            BtnUnbanAccount.Click += BtnUnbanAccount_Click;
            BtnViewBanHistory.Click += BtnViewBanHistory_Click;
            
            LoadAccountInfo();
        }
        
        private void LoadWindowSize()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\AzerothCoreLauncher\AccountEditPopup");
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
        
        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            SaveWindowSize();
        }
        
        private void SaveWindowSize()
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(@"Software\AzerothCoreLauncher\AccountEditPopup");
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
                var account = _dbManager.GetAccountInfo(_accountName);
                if (account == null)
                {
                    MessageBox.Show("Account not found", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                TxtEmail.Text = account.Email;
                TxtLastLogin.Text = account.LastLogin == DateTime.MinValue ? "Never" : account.LastLogin.ToString("yyyy-MM-dd HH:mm:ss");
                TxtLastIP.Text = account.LastIP;
                TxtJoinDate.Text = account.JoinDate.ToString("yyyy-MM-dd HH:mm:ss");
                TxtStatus.Text = account.Online ? "Online" : (account.Locked ? "Locked" : "Active");
                
                // Convert total time to hours
                int totalHours = account.TotalTime / 3600;
                TxtTotalTime.Text = $"{totalHours} hours";
                
                // Load characters
                var characters = _dbManager.GetCharactersByAccountId(account.Id);
                DgCharacters.ItemsSource = characters;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load account info: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void BtnChangePassword_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string newPassword = TxtNewPassword.Password;
                if (string.IsNullOrEmpty(newPassword))
                {
                    MessageBox.Show("Please enter a new password", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                _dbManager.ChangePassword(_accountName, newPassword);
                MessageBox.Show("Password changed successfully", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                TxtNewPassword.Clear();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to change password: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void BtnSaveEmail_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string newEmail = TxtEmail.Text.Trim();
                if (string.IsNullOrEmpty(newEmail))
                {
                    MessageBox.Show("Please enter an email address", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                _dbManager.ChangeEmail(_accountName, newEmail);
                MessageBox.Show("Email updated successfully", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to update email: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void BtnBanAccount_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var popup = new BanAccountPopup(_accountName, _accountName, _worldProcess);
                popup.Owner = this;
                var result = popup.ShowDialog();
                
                if (result == true)
                {
                    // Refresh account info
                    LoadAccountInfo();
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
                LoadAccountInfo();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to unban account: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
        
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
