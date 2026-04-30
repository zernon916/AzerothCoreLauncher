using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace AzerothCoreLauncher
{
    public partial class BanAccountPopup : Window
    {
        private readonly string _accountName;
        private readonly string _playerName;
        private readonly Process? _worldProcess;
        
        public BanAccountPopup(string accountName, string playerName, Process? worldProcess)
        {
            InitializeComponent();
            _accountName = accountName;
            _playerName = playerName;
            _worldProcess = worldProcess;
            
            // Load saved size
            LoadWindowSize();
            
            TxtAccountName.Text = accountName;
            TxtPlayerName.Text = playerName;
            
            BtnBan.Click += BtnBan_Click;
            BtnCancel.Click += BtnCancel_Click;
        }
        
        private void LoadWindowSize()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\AzerothCoreLauncher\BanAccountPopup");
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
                using var key = Registry.CurrentUser.CreateSubKey(@"Software\AzerothCoreLauncher\BanAccountPopup");
                key?.SetValue("Width", (int)Width);
                key?.SetValue("Height", (int)Height);
            }
            catch
            {
                // Ignore errors
            }
        }
        
        private void BtnBan_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedItem = CboDuration.SelectedItem as ComboBoxItem;
                if (selectedItem == null)
                {
                    MessageBox.Show("Please select a duration", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                string duration = selectedItem.Tag?.ToString() ?? "perm";
                string reason = TxtReason.Text.Trim();
                
                if (string.IsNullOrEmpty(reason))
                {
                    MessageBox.Show("Please enter a reason", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                // Disable button during processing
                BtnBan.IsEnabled = false;
                BtnCancel.IsEnabled = false;
                
                // Send announce command
                string announceCommand = $"announce {_playerName} HAS BEEN BANNED! Reason: {reason}";
                _worldProcess?.StandardInput.WriteLine(announceCommand);
                
                // Wait 10 seconds then ban
                Task.Delay(10000).ContinueWith(t =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            // Send ban command
                            string banCommand = $"ban account {_accountName} {duration} {reason}";
                            _worldProcess?.StandardInput.WriteLine(banCommand);
                            
                            MessageBox.Show($"Account {_accountName} has been banned", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                            DialogResult = true;
                            Close();
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Failed to ban account: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                            BtnBan.IsEnabled = true;
                            BtnCancel.IsEnabled = true;
                        }
                    });
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to ban account: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                BtnBan.IsEnabled = true;
                BtnCancel.IsEnabled = true;
            }
        }
        
        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
