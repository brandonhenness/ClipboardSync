using System;
using System.Configuration;
using System.IO;
using System.Windows;

namespace ClipboardSyncApp
{
    public partial class SettingsWindow : Window
    {
        Configuration AppConfig = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);

        public SettingsWindow()
        {
            InitializeComponent();
            LoadSettings();
        }

        private void LoadSettings()
        {
            try
            {
                var flashDriveLabel = AppConfig.AppSettings.Settings["FlashDriveLabel"]?.Value;
                if (flashDriveLabel != null)
                {
                    FlashDriveLabelTextBox.Text = flashDriveLabel;
                }
                else
                {
                    FlashDriveLabelTextBox.Text = string.Empty;
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to load settings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (AppConfig.AppSettings.Settings["FlashDriveLabel"] != null)
                {
                    AppConfig.AppSettings.Settings["FlashDriveLabel"].Value = FlashDriveLabelTextBox.Text;
                }
                else
                {
                    AppConfig.AppSettings.Settings.Add("FlashDriveLabel", FlashDriveLabelTextBox.Text);
                }
                AppConfig.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection("appSettings");

                System.Windows.MessageBox.Show("Settings saved successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to save settings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
