using System.Windows;

namespace ClipboardSyncApp
{
    public partial class InputBox : Window
    {
        public string ResponseText { get; private set; } = string.Empty;

        public InputBox(string prompt, string title)
        {
            InitializeComponent();
            Title = title;
            PromptTextBlock.Text = prompt;
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            ResponseText = ResponseTextBox.Text;
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
