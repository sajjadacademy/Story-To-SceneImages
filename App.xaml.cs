using StoryToSceneImages;
using System.Windows;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        string msg;

        // Provide a license text string as the first argument to ValidateText
        string licenseText = ""; // TODO: Replace with actual license text or load from file/config

        if (!LicenseValidator.ValidateText(licenseText, out msg))
        {
            var lw = new LicenseWindow();
            bool? result = lw.ShowDialog();

            if (result != true)
            {
                MessageBox.Show($"Application will now exit.\n\nReason: {msg}",
                                "License Required",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
                return;
            }
        }

        new MainWindow().Show();
    }
}
