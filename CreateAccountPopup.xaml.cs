using System;
using System.Diagnostics;
using System.Windows;

namespace AzerothCoreLauncher
{
    public partial class CreateAccountPopup : Window
    {
        private readonly DatabaseManager _dbManager;
        private readonly Process? _worldProcess;
        
        public CreateAccountPopup(DatabaseManager dbManager, Process? worldProcess)
        {
            InitializeComponent();
            _dbManager = dbManager;
            _worldProcess = worldProcess;
        }
        
        private void BtnCreate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string username = TxtUsername.Text.Trim();
                string password = TxtPassword.Password;
                string confirmPassword = TxtConfirmPassword.Password;
                string email = TxtEmail.Text.Trim();
                
                // Get selected expansion
                int expansion = 2; // Default to WotLK
                if (CmbExpansion.SelectedItem is System.Windows.Controls.ComboBoxItem item)
                {
                    if (item.Tag != null)
                    {
                        expansion = Convert.ToInt32(item.Tag);
                    }
                }
                
                // Validation
                if (string.IsNullOrEmpty(username))
                {
                    MessageBox.Show("Please enter a username", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                if (string.IsNullOrEmpty(password))
                {
                    MessageBox.Show("Please enter a password", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                if (password != confirmPassword)
                {
                    MessageBox.Show("Passwords do not match", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                if (string.IsNullOrEmpty(email))
                {
                    MessageBox.Show("Please enter an email address", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                // Create account via server command
                if (_worldProcess != null && !_worldProcess.HasExited)
                {
                    string command = $"account create {username} {password} {email}";
                    _worldProcess.StandardInput.WriteLine(command);
                    
                    MessageBox.Show($"Account '{username}' created successfully via server command", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("World server is not running. Cannot create account.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                // Clear fields
                TxtUsername.Clear();
                TxtPassword.Clear();
                TxtConfirmPassword.Clear();
                TxtEmail.Clear();
                
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to create account: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
