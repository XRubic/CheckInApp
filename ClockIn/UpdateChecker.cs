using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Diagnostics;
using System.Reflection;
using System.IO;
using System.Net;

namespace ClockIn
{
    public class UpdateChecker
    {
        private const string GITHUB_API_URL = "https://api.github.com/repos/YOUR_USERNAME/YOUR_REPO/releases/latest";
        private readonly string _currentVersion;
        private readonly HttpClient _httpClient;

        public UpdateChecker()
        {
            _currentVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "ClockIn-App");
            Debug.WriteLine($"Current app version: {_currentVersion}");
        }

        public static bool CheckInternetConnection()
        {
            try
            {
                using (var ping = new Ping())
                {
                    var result = ping.Send("8.8.8.8", 2000); // Google DNS server
                    Debug.WriteLine($"Internet connection check result: {result.Status}");
                    return result.Status == IPStatus.Success;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Internet connection check failed: {ex.Message}");
                return false;
            }
        }

        public async Task<(bool updateAvailable, string latestVersion, string downloadUrl)> CheckForUpdates()
        {
            try
            {
                Debug.WriteLine("Checking for updates...");
                var response = await _httpClient.GetStringAsync(GITHUB_API_URL);
                var releaseInfo = JsonSerializer.Deserialize<GitHubRelease>(response);

                if (releaseInfo != null && !string.IsNullOrEmpty(releaseInfo.TagName))
                {
                    // Remove 'v' prefix if present
                    string latestVersion = releaseInfo.TagName.TrimStart('v');
                    Debug.WriteLine($"Latest version available: {latestVersion}");

                    Version current = Version.Parse(_currentVersion);
                    Version latest = Version.Parse(latestVersion);

                    bool updateAvailable = latest > current;
                    Debug.WriteLine($"Update available: {updateAvailable}");

                    return (updateAvailable, latestVersion, releaseInfo.Assets[0].BrowserDownloadUrl);
                }

                return (false, string.Empty, string.Empty);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking for updates: {ex.Message}");
                throw;
            }
        }

        public async Task DownloadAndInstallUpdate(string downloadUrl)
        {
            try
            {
                Debug.WriteLine($"Starting update download from: {downloadUrl}");

                // Create temp directory if it doesn't exist
                string tempPath = Path.Combine(Path.GetTempPath(), "ClockInUpdates");
                Directory.CreateDirectory(tempPath);

                // Download the update
                string updateFile = Path.Combine(tempPath, "ClockInUpdate.exe");
                using (var client = new WebClient())
                {
                    await client.DownloadFileTaskAsync(new Uri(downloadUrl), updateFile);
                }

                Debug.WriteLine($"Update downloaded to: {updateFile}");

                // Create and start updater script
                string updaterScript = CreateUpdaterScript(updateFile);

                // Start the updater process
                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c {updaterScript}",
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true
                });

                // Exit the current application
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during update: {ex.Message}");
                throw;
            }
        }

        private string CreateUpdaterScript(string updateFile)
        {
            string currentExePath = Process.GetCurrentProcess().MainModule.FileName;
            string scriptPath = Path.Combine(Path.GetTempPath(), "update.bat");

            // Create batch script to handle the update
            string script = $@"
@echo off
timeout /t 2 /nobreak > nul
del ""{currentExePath}""
copy ""{updateFile}"" ""{currentExePath}""
start """" ""{currentExePath}""
del ""{updateFile}""
del ""%~f0""
";

            File.WriteAllText(scriptPath, script);
            Debug.WriteLine($"Update script created at: {scriptPath}");
            return scriptPath;
        }
    }

    public class GitHubRelease
    {
        public string TagName { get; set; }
        public GitHubAsset[] Assets { get; set; }
    }

    public class GitHubAsset
    {
        public string BrowserDownloadUrl { get; set; }
    }
}