using Supabase;
using System.Diagnostics;
using System.Windows;

namespace ClockIn
{
    public partial class App : Application
    {
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                if (!UpdateChecker.CheckInternetConnection())
                {
                    MessageBox.Show("No internet connection detected. The application requires an internet connection to function.",
                        "No Internet Connection",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    Environment.Exit(0);
                    return;
                }

                string url = "https://jbfoxddlynsotajgbkmk.supabase.co";
                string key = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6ImpiZm94ZGRseW5zb3Rhamdia21rIiwicm9sZSI6InNlcnZpY2Vfcm9sZSIsImlhdCI6MTcyODUyOTA5NCwiZXhwIjoyMDQ0MTA1MDk0fQ.Yb8dcSQopExrCelc4YPCNmIcmx-qCucHu9xmycasxYY";
                var supabase = new Client(url, key);

                var dbSetup = new DatabaseSetup(supabase);
                await dbSetup.EnsureAdminUserExists();

                var devWindow = new DevWindow(supabase);
                devWindow.Show();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Startup error: {ex.Message}");
                MessageBox.Show($"An error occurred during startup: {ex.Message}",
                    "Startup Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Environment.Exit(1);
            }
        }
    }
}