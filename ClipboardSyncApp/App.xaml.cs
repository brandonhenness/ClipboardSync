using System.Windows;

namespace ClipboardSyncApp
{

    public partial class App : System.Windows.Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            MainWindow mainWindow = new MainWindow();
            mainWindow.Hide(); // Ensure MainWindow is hidden at startup
        }
    }

}
