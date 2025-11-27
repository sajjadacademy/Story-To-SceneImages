using System.Windows;

namespace StoryToSceneImages
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();

            // Load saved settings
            DeepAiKeyBox.Text = Properties.Settings.Default.DeepAIKey;
            LeonardoKeyBox.Text = Properties.Settings.Default.LeonardoKey;
            HuggingFaceKeyBox.Text = Properties.Settings.Default.HuggingFaceKey;

            PixlrKeyBox.Text = Properties.Settings.Default.PixlrKey;
            HeyGenKeyBox.Text = Properties.Settings.Default.HeyGenKey;
            GoogleVeoKeyBox.Text = Properties.Settings.Default.GoogleVeoKey;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.DeepAIKey = DeepAiKeyBox.Text;
            Properties.Settings.Default.LeonardoKey = LeonardoKeyBox.Text;
            Properties.Settings.Default.HuggingFaceKey = HuggingFaceKeyBox.Text;

            Properties.Settings.Default.PixlrKey = PixlrKeyBox.Text;
            Properties.Settings.Default.HeyGenKey = HeyGenKeyBox.Text;
            Properties.Settings.Default.GoogleVeoKey = GoogleVeoKeyBox.Text;

            Properties.Settings.Default.Save();

            MessageBox.Show("Settings saved successfully!", "Saved");
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
