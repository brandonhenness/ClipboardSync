using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using WinForms = System.Windows.Forms;
using WpfClipboard = System.Windows.Clipboard;
using System.Drawing;
using System.Text.Json;
using System.Configuration;

namespace ClipboardSyncApp
{
    public partial class MainWindow : Window
    {
        private static WinForms.NotifyIcon? trayIcon;
        private static readonly string ClipboardDataFileName = "clipboard_data.json";
        private static readonly string ClipboardHtmlFileName = "clipboard_data.html";
        private static readonly string ClipboardRtfFileName = "clipboard_data.rtf";
        private ProgressWindow progressWindow = new ProgressWindow();
        Configuration AppConfig = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
        private static SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);
        private static bool isEventRegistered = false;

        public MainWindow()
        {
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(UnhandledExceptionHandler);
            Dispatcher.UnhandledException += Dispatcher_UnhandledException;

            InitializeComponent();
            this.Hide(); // Ensure MainWindow is hidden at startup
            SetupTrayIcon();
            if (!isEventRegistered)
            {
                ClipboardNotification.ClipboardUpdate += ClipboardChanged;
                isEventRegistered = true;
            }
            CheckForFlashDriveAndLoadClipboard();
        }

        private void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = (Exception)e.ExceptionObject;
            LogMessage($"Unhandled exception: {ex.Message}\n{ex.StackTrace}");
        }

        private void Dispatcher_UnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            LogMessage($"Unhandled dispatcher exception: {e.Exception.Message}\n{e.Exception.StackTrace}");
            e.Handled = true;
        }

        private void SetupTrayIcon()
        {
            try
            {
                if (trayIcon == null)
                {
                    var iconUri = new Uri("pack://application:,,,/Resources/clipboard-sync_dark.ico");
                    using (var stream = System.Windows.Application.GetResourceStream(iconUri).Stream)
                    {
                        trayIcon = new WinForms.NotifyIcon
                        {
                            Icon = new Icon(stream),
                            Visible = true
                        };
                    }

                    var trayMenu = new WinForms.ContextMenuStrip();
                    var settingsSubMenu = new WinForms.ToolStripMenuItem("Settings");
                    settingsSubMenu.DropDownItems.Add("Start with Windows", null, ToggleStartWithWindows);
                    settingsSubMenu.DropDownItems.Add("Change Flash Drive Label", null, ChangeFlashDriveLabel_Click);

                    var helpSubMenu = new WinForms.ToolStripMenuItem("Help");
                    helpSubMenu.DropDownItems.Add("About", null, OpenAbout);

                    trayMenu.Items.Add("ClipboardSyncApp", null); // Title
                    trayMenu.Items.Add(new WinForms.ToolStripSeparator());
                    trayMenu.Items.Add(settingsSubMenu);
                    trayMenu.Items.Add(helpSubMenu);
                    trayMenu.Items.Add(new WinForms.ToolStripSeparator());
                    trayMenu.Items.Add("Exit", null, Exit);

                    trayIcon.ContextMenuStrip = trayMenu;
                    trayIcon.DoubleClick += (s, e) => this.Show();
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error setting up tray icon: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void ToggleStartWithWindows(object? sender, EventArgs e)
        {
            bool currentValue = bool.Parse(AppConfig.AppSettings.Settings["StartWithWindows"].Value);
            AppConfig.AppSettings.Settings["StartWithWindows"].Value = (!currentValue).ToString();
            AppConfig.Save(ConfigurationSaveMode.Modified);
            LogMessage($"Start with Windows set to: {!currentValue}");
        }

        private void ChangeFlashDriveLabel_Click(object? sender, EventArgs e)
        {
            var inputBox = new InputBox("Enter new flash drive label:", "Change Flash Drive Label");
            if (inputBox.ShowDialog() == true)
            {
                string newLabel = inputBox.ResponseText;
                if (!string.IsNullOrEmpty(newLabel))
                {
                    AppConfig.AppSettings.Settings["FlashDriveLabel"].Value = newLabel;
                    AppConfig.Save(ConfigurationSaveMode.Modified);
                    LogMessage($"Flash drive label changed to: {newLabel}");
                }
            }
        }

        private void ClipboardChanged(object? sender, EventArgs e)
        {
            LogMessage("ClipboardChanged event triggered.");
            try
            {
                var clipboardData = WpfClipboard.GetDataObject();
                if (clipboardData != null)
                {
                    LogMessage("Clipboard data obtained.");
                    SaveClipboardToFlashDrive(clipboardData);
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error handling clipboard change: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private async void SaveClipboardToFlashDrive(System.Windows.IDataObject clipboardData)
        {
            await semaphore.WaitAsync();
            try
            {
                LogMessage("SaveClipboardToFlashDrive started.");
                string flashDrivePath = Path.Combine(GetFlashDrivePath(), ClipboardDataFileName);
                string htmlFilePath = Path.Combine(GetFlashDrivePath(), ClipboardHtmlFileName);
                string rtfFilePath = Path.Combine(GetFlashDrivePath(), ClipboardRtfFileName);

                if (clipboardData.GetDataPresent(System.Windows.DataFormats.Html))
                {
                    LogMessage("Clipboard data is HTML.");
                    await DeleteExistingFilesAsync(new string[] { htmlFilePath, rtfFilePath, flashDrivePath });

                    string htmlData = (string)clipboardData.GetData(System.Windows.DataFormats.Html);
                    await File.WriteAllTextAsync(htmlFilePath, htmlData);
                    await File.WriteAllTextAsync(flashDrivePath, JsonSerializer.Serialize(new ClipboardData { Type = System.Windows.DataFormats.Html.ToString(), Data = ClipboardHtmlFileName }));
                }
                else if (clipboardData.GetDataPresent(System.Windows.DataFormats.Rtf))
                {
                    LogMessage("Clipboard data is RTF.");
                    await DeleteExistingFilesAsync(new string[] { htmlFilePath, rtfFilePath, flashDrivePath });

                    string rtfData = (string)clipboardData.GetData(System.Windows.DataFormats.Rtf);
                    await File.WriteAllTextAsync(rtfFilePath, rtfData);
                    await File.WriteAllTextAsync(flashDrivePath, JsonSerializer.Serialize(new ClipboardData { Type = System.Windows.DataFormats.Rtf.ToString(), Data = ClipboardRtfFileName }));
                }
                else if (clipboardData.GetDataPresent(System.Windows.DataFormats.Text))
                {
                    LogMessage("Clipboard data is Text.");
                    await DeleteExistingFilesAsync(new string[] { htmlFilePath, rtfFilePath, flashDrivePath });

                    string textData = (string)clipboardData.GetData(System.Windows.DataFormats.Text);
                    await File.WriteAllTextAsync(flashDrivePath, JsonSerializer.Serialize(new ClipboardData { Type = System.Windows.DataFormats.Text.ToString(), Data = textData }));
                }
                else if (clipboardData.GetDataPresent(System.Windows.DataFormats.FileDrop))
                {
                    LogMessage("Clipboard data is FileDrop.");
                    progressWindow = new ProgressWindow();
                    progressWindow.Show();

                    string[] files = (string[])clipboardData.GetData(System.Windows.DataFormats.FileDrop);
                    var relativePaths = new List<string>();

                    if (File.Exists(flashDrivePath))
                    {
                        string existingDataJson = await File.ReadAllTextAsync(flashDrivePath);
                        var existingData = JsonSerializer.Deserialize<ClipboardData>(existingDataJson);
                        if (existingData != null && existingData.Type == System.Windows.DataFormats.FileDrop.ToString())
                        {
                            string[] existingFiles = existingData.Data.Split(';');
                            await DeleteExistingFilesAsync(existingFiles.Select(f => Path.Combine(GetFlashDrivePath(), f)).ToArray());
                        }
                    }

                    for (int i = 0; i < files.Length; i++)
                    {
                        string file = files[i];
                        string fileName = Path.GetFileName(file);
                        string destinationPath = Path.Combine(GetFlashDrivePath(), fileName);
                        if (Directory.Exists(file))
                        {
                            await CopyDirectoryAsync(file, destinationPath, i, files.Length);
                        }
                        else
                        {
                            await CopyFileAsync(file, destinationPath, i, files.Length);
                        }
                        relativePaths.Add(fileName);
                    }

                    await File.WriteAllTextAsync(flashDrivePath, JsonSerializer.Serialize(new ClipboardData { Type = System.Windows.DataFormats.FileDrop.ToString(), Data = string.Join(";", relativePaths) }));

                    progressWindow.Close();
                }
                else
                {
                    LogMessage("Clipboard data is Unknown.");
                    await File.WriteAllTextAsync(flashDrivePath, JsonSerializer.Serialize(new ClipboardData { Type = "Unknown", Data = "Unsupported clipboard data format" }));
                }
            }
            catch (System.Runtime.InteropServices.COMException ex)
            {
                LogMessage($"Failed to access clipboard: {ex.Message}");
            }
            catch (Exception ex)
            {
                LogMessage($"Error saving clipboard to flash drive: {ex.Message}\n{ex.StackTrace}");
            }
            finally
            {
                semaphore.Release();
            }
        }

        private async Task CopyFileAsync(string sourceFile, string destinationFile, int currentItem, int totalItems)
        {
            progressWindow.Dispatcher.Invoke(() =>
            {
                progressWindow.ProgressBar.Maximum = totalItems;
                progressWindow.ProgressBar.Value = currentItem + 1;
            });

            try
            {
                using (FileStream sourceStream = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (FileStream destinationStream = new FileStream(destinationFile, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await sourceStream.CopyToAsync(destinationStream);
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error copying file '{sourceFile}' to '{destinationFile}': {ex.Message}\n{ex.StackTrace}");
            }
        }

        private async Task CopyDirectoryAsync(string sourceDir, string destinationDir, int currentItem, int totalItems)
        {
            Directory.CreateDirectory(destinationDir);

            DirectoryInfo dir = new DirectoryInfo(sourceDir);
            FileInfo[] files = dir.GetFiles();
            DirectoryInfo[] subDirs = dir.GetDirectories();

            progressWindow.Dispatcher.Invoke(() =>
            {
                progressWindow.ProgressBar.Maximum = totalItems;
                progressWindow.ProgressBar.Value = currentItem + 1;
            });

            foreach (FileInfo file in files)
            {
                string tempPath = Path.Combine(destinationDir, file.Name);
                await CopyFileAsync(file.FullName, tempPath, currentItem, totalItems);
            }

            foreach (DirectoryInfo subDir in subDirs)
            {
                string tempPath = Path.Combine(destinationDir, subDir.Name);
                await CopyDirectoryAsync(subDir.FullName, tempPath, currentItem, totalItems);
            }
        }

        private async Task DeleteExistingFilesAsync(string[] files)
        {
            foreach (var file in files)
            {
                try
                {
                    if (File.Exists(file))
                    {
                        await Task.Run(() => File.Delete(file));
                    }
                    else if (Directory.Exists(file))
                    {
                        await Task.Run(() => Directory.Delete(file, true));
                    }
                }
                catch (Exception ex)
                {
                    LogMessage($"Error deleting file or directory '{file}': {ex.Message}\n{ex.StackTrace}");
                }
            }
        }

        private void LoadClipboardFromFlashDrive()
        {
            LogMessage("Loading clipboard data from flash drive.");
            string flashDrivePath = Path.Combine(GetFlashDrivePath(), ClipboardDataFileName);
            if (File.Exists(flashDrivePath))
            {
                try
                {
                    string jsonData = File.ReadAllText(flashDrivePath);
                    LogMessage($"Raw JSON data: {jsonData}");

                    var clipboardData = JsonSerializer.Deserialize<ClipboardData>(jsonData);
                    if (clipboardData != null)
                    {
                        LogMessage($"Deserialized Clipboard Data - Type: {clipboardData.Type}, Data: {clipboardData.Data}");
                        Dispatcher.Invoke(() => SetClipboardData(clipboardData));
                    }
                    else
                    {
                        LogMessage("Failed to deserialize clipboard data from JSON.");
                    }
                }
                catch (Exception ex)
                {
                    LogMessage($"Error loading clipboard data: {ex.Message}");
                }
            }
            else
            {
                LogMessage("No clipboard data found on flash drive.");
            }
        }

        private void SetClipboardData(ClipboardData clipboardData)
        {
            int retryCount = 3;
            while (retryCount-- > 0)
            {
                try
                {
                    if (clipboardData.Type == System.Windows.DataFormats.Text.ToString())
                    {
                        WpfClipboard.SetData(System.Windows.DataFormats.Text, clipboardData.Data);
                        LogMessage("Clipboard text data set.");
                    }
                    else if (clipboardData.Type == System.Windows.DataFormats.Html.ToString())
                    {
                        if (File.Exists(Path.Combine(GetFlashDrivePath(), clipboardData.Data)))
                        {
                            string htmlData = File.ReadAllText(Path.Combine(GetFlashDrivePath(), clipboardData.Data));
                            WpfClipboard.SetData(System.Windows.DataFormats.Html, htmlData);
                            LogMessage("Clipboard HTML data set.");
                        }
                    }
                    else if (clipboardData.Type == System.Windows.DataFormats.Rtf.ToString())
                    {
                        if (File.Exists(Path.Combine(GetFlashDrivePath(), clipboardData.Data)))
                        {
                            string rtfData = File.ReadAllText(Path.Combine(GetFlashDrivePath(), clipboardData.Data));
                            WpfClipboard.SetData(System.Windows.DataFormats.Rtf, rtfData);
                            LogMessage("Clipboard RTF data set.");
                        }
                    }
                    else if (clipboardData.Type == System.Windows.DataFormats.FileDrop.ToString())
                    {
                        string[] fileDropList = clipboardData.Data.Split(';')
                            .Select(file => Path.Combine(GetFlashDrivePath(), file))
                            .ToArray();
                        WpfClipboard.SetData(System.Windows.DataFormats.FileDrop, fileDropList);
                        LogMessage("Clipboard file drop data set.");
                    }
                    return;
                }
                catch (System.Runtime.InteropServices.COMException ex)
                {
                    LogMessage($"Failed to set clipboard data: {ex.Message}. Retrying...");
                    Thread.Sleep(500);
                }
                catch (Exception ex)
                {
                    LogMessage($"Error setting clipboard data: {ex.Message}\n{ex.StackTrace}");
                }
            }
        }

        private string GetFlashDrivePath()
        {
            var allDrives = DriveInfo.GetDrives();
            string? flashDriveLabel = AppConfig.AppSettings.Settings["FlashDriveLabel"]?.Value;
            if (flashDriveLabel == null)
            {
                LogMessage("FlashDriveLabel is not configured in AppConfig.");
                return string.Empty;
            }

            foreach (DriveInfo d in allDrives)
            {
                if (d.DriveType == DriveType.Removable && d.IsReady && d.VolumeLabel == flashDriveLabel)
                {
                    return d.RootDirectory.FullName;
                }
            }
            return string.Empty;
        }

        private bool IsFlashDriveConnected()
        {
            var allDrives = DriveInfo.GetDrives();
            string? flashDriveLabel = AppConfig.AppSettings.Settings["FlashDriveLabel"]?.Value;
            if (flashDriveLabel == null)
            {
                LogMessage("FlashDriveLabel is not configured in AppConfig.");
                return false;
            }

            foreach (DriveInfo d in allDrives)
            {
                if (d.DriveType == DriveType.Removable && d.IsReady && d.VolumeLabel == flashDriveLabel)
                {
                    LogMessage($"USB Event Detected: {d.Name}");
                    return true;
                }
            }
            return false;
        }

        private void Exit(object? sender, EventArgs e)
        {
            if (trayIcon != null) trayIcon.Visible = false;
            System.Windows.Application.Current.Shutdown();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            this.Hide();
            base.OnClosing(e);
        }

        private void LogMessage(string message)
        {
            try
            {
                File.AppendAllText("clipboard_sync_log.txt", $"{DateTime.Now}: {message}{Environment.NewLine}");
            }
            catch (Exception ex)
            {
                // Handle any errors that occur during logging
                Console.WriteLine($"Failed to log message: {ex.Message}");
            }
        }

        private void OpenAbout(object? sender, EventArgs e)
        {
            System.Windows.MessageBox.Show("ClipboardSyncApp v1.0\n\nThis application synchronizes your clipboard content with a flash drive.", "About ClipboardSyncApp", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void CheckForFlashDriveAndLoadClipboard()
        {
            LogMessage("Checking for flash drive at startup.");
            try
            {
                if (IsFlashDriveConnected())
                {
                    LogMessage("Flash drive is connected at startup.");
                    LoadClipboardFromFlashDrive();
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error checking for flash drive: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }

    public static class ClipboardNotification
    {
        public static event EventHandler? ClipboardUpdate;

        private static NotificationForm form = new NotificationForm();

        public static void Start()
        {
            try
            {
                WinForms.Application.Run(form);
            }
            catch (Exception ex)
            {
                File.AppendAllText("clipboard_sync_log.txt", $"{DateTime.Now}: Error in ClipboardNotification.Start: {ex.Message}{Environment.NewLine}");
            }
        }

        private class NotificationForm : WinForms.Form
        {
            public NotificationForm()
            {
                try
                {
                    NativeMethods.SetParent(Handle, NativeMethods.HWND_MESSAGE);
                    NativeMethods.AddClipboardFormatListener(Handle);
                }
                catch (Exception ex)
                {
                    File.AppendAllText("clipboard_sync_log.txt", $"{DateTime.Now}: Error in NotificationForm constructor: {ex.Message}{Environment.NewLine}");
                }
            }

            protected override void WndProc(ref WinForms.Message m)
            {
                try
                {
                    if (m.Msg == NativeMethods.WM_CLIPBOARDUPDATE)
                    {
                        ClipboardUpdate?.Invoke(null, EventArgs.Empty);
                    }
                    base.WndProc(ref m);
                }
                catch (Exception ex)
                {
                    File.AppendAllText("clipboard_sync_log.txt", $"{DateTime.Now}: Error in WndProc: {ex.Message}{Environment.NewLine}");
                }
            }
        }

        private static class NativeMethods
        {
            public const int WM_CLIPBOARDUPDATE = 0x031D;
            public static readonly IntPtr HWND_MESSAGE = new IntPtr(-3);

            [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
            public static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

            [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
            [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
            public static extern bool AddClipboardFormatListener(IntPtr hwnd);
        }
    }

    public class ClipboardData
    {
        public string Type { get; set; } = string.Empty;
        public string Data { get; set; } = string.Empty;
    }
}
