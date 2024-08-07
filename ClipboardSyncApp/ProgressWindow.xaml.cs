using System.Windows;

namespace ClipboardSyncApp
{
    public partial class ProgressWindow : Window
    {
        public ProgressWindow()
        {
            InitializeComponent();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            // Handle cancellation if needed
            this.Close();
        }

        public void UpdateProgress(double value)
        {
            ProgressBar.Value = value;
        }
    }
}
