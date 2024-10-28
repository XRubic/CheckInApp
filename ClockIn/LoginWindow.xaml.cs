using Supabase;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace ClockIn
{
    public partial class LoginWindow : Window
    {
        private Client supabase;

        public LoginWindow()
        {
            InitializeComponent();
            InitializeSupabase();
        }

        private void InitializeSupabase()
        {
            string url = "https://jbfoxddlynsotajgbkmk.supabase.co";
            string key = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6ImpiZm94ZGRseW5zb3Rhamdia21rIiwicm9sZSI6InNlcnZpY2Vfcm9sZSIsImlhdCI6MTcyODUyOTA5NCwiZXhwIjoyMDQ0MTA1MDk0fQ.Yb8dcSQopExrCelc4YPCNmIcmx-qCucHu9xmycasxYY";
            supabase = new Client(url, key);
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!UpdateChecker.CheckInternetConnection())
                {
                    MessageBox.Show("No internet connection detected. Please check your connection and try again.",
                        "No Internet Connection",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                var authService = new AuthService(supabase);
                var (user, error) = await authService.Login(CompanyIdTextBox.Text, UsernameTextBox.Text, PasswordBox.Password);

                if (user != null)
                {
                    // Check for updates after successful login
                    var updateChecker = new UpdateChecker();
                    try
                    {
                        var (updateAvailable, latestVersion, downloadUrl) = await updateChecker.CheckForUpdates();
                        if (updateAvailable)
                        {
                            var result = MessageBox.Show(
                                $"A new version ({latestVersion}) is available. Would you like to update now?",
                                "Update Available",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Information);

                            if (result == MessageBoxResult.Yes)
                            {
                                await updateChecker.DownloadAndInstallUpdate(downloadUrl);
                                return; // Application will restart after update
                            }
                        }
                    }
                    catch (Exception updateEx)
                    {
                        Debug.WriteLine($"Update check failed: {updateEx.Message}");
                        // Continue with login even if update check fails
                    }

                    // Proceed with normal login flow
                    if (user.IsAdmin)
                    {
                        var adminDashboard = new AdminDashboard(supabase, user);
                        adminDashboard.Show();
                    }
                    else
                    {
                        var userDashboard = new UserDashboard(user);
                        userDashboard.Show();
                    }
                    this.Close();
                }
                else
                {
                    MessageBox.Show($"Login failed: {error}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Login error: {ex.Message}");
                MessageBox.Show($"An error occurred during login: {ex.Message}");
            }
        }

        private void RegisterCompany_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Implement company registration logic
            MessageBox.Show("Company registration not implemented yet.");
        }
    }
}