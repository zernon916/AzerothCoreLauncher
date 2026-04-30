using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using Microsoft.Win32;

namespace AzerothCoreLauncher
{
    public partial class BanHistoryPopup : Window
    {
        private readonly DatabaseManager _dbManager;
        private readonly string _target;
        private readonly bool _isAccount;
        
        public BanHistoryPopup(DatabaseManager dbManager, string target, bool isAccount)
        {
            InitializeComponent();
            _dbManager = dbManager;
            _target = target;
            _isAccount = isAccount;
            
            // Load saved size
            LoadWindowSize();
            
            TxtTitle.Text = isAccount ? "Account Ban History" : "IP Ban History";
            TxtTarget.Text = isAccount ? $"Account: {target}" : $"IP: {target}";
            
            LoadBanHistory();
        }
        
        private void LoadWindowSize()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\AzerothCoreLauncher\BanHistoryPopup");
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
                using var key = Registry.CurrentUser.CreateSubKey(@"Software\AzerothCoreLauncher\BanHistoryPopup");
                key?.SetValue("Width", (int)Width);
                key?.SetValue("Height", (int)Height);
            }
            catch
            {
                // Ignore errors
            }
        }
        
        private void LoadBanHistory()
        {
            try
            {
                if (_isAccount)
                {
                    var history = _dbManager.GetAccountBanHistory(_target);
                    DgBanHistory.ItemsSource = history;
                }
                else
                {
                    var history = _dbManager.GetIPBanHistory(_target);
                    DgBanHistory.ItemsSource = history;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load ban history: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
