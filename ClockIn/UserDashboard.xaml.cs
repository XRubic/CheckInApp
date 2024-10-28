using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Supabase;
using System.Threading.Tasks;
using Postgrest.Models;
using System.Diagnostics;
using System.Windows.Threading;
using Newtonsoft.Json.Linq;

namespace ClockIn
{
    public partial class UserDashboard : Window
    {
        private User currentUser;
        private Client supabase;
        private TimeEntry currentTimeEntry;
        private Break currentBreak;
        public ObservableCollection<ActionLog> ActionLogs { get; set; }
        private BreakReminderWindow breakReminderWindow;
        private DispatcherTimer clockTimer;

        public UserDashboard(User user)
        {
            InitializeComponent();
            currentUser = user;
            InitializeSupabase();
            ActionLogs = new ObservableCollection<ActionLog>();
            DataContext = this;
            InitializeBreakReminder();
            InitializeClockTimer();
            LoadTimeEntries();
            LoadCurrentStatus();
        }

        private void InitializeClockTimer()
        {
            clockTimer = new DispatcherTimer();
            clockTimer.Interval = TimeSpan.FromSeconds(1);
            clockTimer.Tick += ClockTimer_Tick;
            clockTimer.Start();
        }

        private void ClockTimer_Tick(object sender, EventArgs e)
        {
            if (CurrentTimeTextBlock != null)
            {
                CurrentTimeTextBlock.Text = $"Current Time: {DateTime.Now:HH:mm:ss}";
            }
        }

        private void InitializeSupabase()
        {
            string url = "https://jbfoxddlynsotajgbkmk.supabase.co";
            string key = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6ImpiZm94ZGRseW5zb3Rhamdia21rIiwicm9sZSI6InNlcnZpY2Vfcm9sZSIsImlhdCI6MTcyODUyOTA5NCwiZXhwIjoyMDQ0MTA1MDk0fQ.Yb8dcSQopExrCelc4YPCNmIcmx-qCucHu9xmycasxYY";
            supabase = new Client(url, key);
        }

        private void InitializeBreakReminder()
        {
            breakReminderWindow = new BreakReminderWindow();
            breakReminderWindow.EndBreakClicked += BreakReminderWindow_EndBreakClicked;
        }

        private async void BreakReminderWindow_EndBreakClicked(object sender, EventArgs e)
        {
            await EndBreak();
        }

        private async void LoadCurrentStatus()
        {
            try
            {
                Debug.WriteLine("Loading current status");
                var latestEntry = await GetLatestTimeEntry();
                Debug.WriteLine($"Latest entry found: {latestEntry?.Id}");

                if (latestEntry != null && !latestEntry.ClockOutTime.HasValue)
                {
                    Debug.WriteLine("User is currently clocked in");
                    currentTimeEntry = latestEntry;
                    ClockInOutButton.Content = "Clock Out";
                    StatusTextBlock.Text = "Current Status: Clocked In";

                    var activeBreak = await GetActiveBreak(latestEntry.Id);
                    Debug.WriteLine($"Active break found: {activeBreak?.Id}");

                    if (activeBreak != null)
                    {
                        Debug.WriteLine("User is currently on break");
                        currentBreak = activeBreak;
                        BreakButton.Content = new TextBlock { Text = "End Break" };
                        StatusTextBlock.Text = "Current Status: On Break";
                        ShowBreakReminder();
                    }
                    else
                    {
                        Debug.WriteLine("User is not on break");
                        BreakButton.Content = new TextBlock { Text = "Start Break" };
                        BreakButton.IsEnabled = true;
                    }
                }
                else
                {
                    Debug.WriteLine("User is not clocked in");
                    ClockInOutButton.Content = "Clock In";
                    StatusTextBlock.Text = "Current Status: Clocked Out";
                    BreakButton.IsEnabled = false;
                    BreakButton.Content = new TextBlock { Text = "Start Break" };
                }

                await UpdateTotalWorkedHours();
                await UpdateTotalBreakDuration();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in LoadCurrentStatus: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                MessageBox.Show($"Error loading current status: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ClockInOut_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var now = DateTime.Now;
                if (currentTimeEntry == null)
                {
                    // Clock In
                    currentTimeEntry = new TimeEntry
                    {
                        UserId = currentUser.Id,
                        ClockInTime = now,
                        Date = now.Date,  // Ensure we set the correct date
                        TotalBreakDuration = TimeSpan.Zero // Initialize break duration
                    };
                    Debug.WriteLine($"Creating new time entry - Date: {currentTimeEntry.Date:yyyy-MM-dd}, Time: {currentTimeEntry.ClockInTime:HH:mm:ss}");

                    var response = await supabase.From<TimeEntry>().Insert(currentTimeEntry);
                    if (response.Models.Count > 0)
                    {
                        currentTimeEntry = response.Models[0];
                        ClockInOutButton.Content = "Clock Out";
                        StatusTextBlock.Text = "Current Status: Clocked In";
                        BreakButton.IsEnabled = true;
                        AddActionLog("CLOCKED IN", now);
                        Debug.WriteLine($"Successfully clocked in - Entry ID: {currentTimeEntry.Id}");
                        await UpdateTotalWorkedHours();
                    }
                }
                else
                {
                    // Clock Out
                    Debug.WriteLine($"Clocking out time entry ID: {currentTimeEntry.Id}");
                    currentTimeEntry.ClockOutTime = now;
                    await supabase.From<TimeEntry>().Update(currentTimeEntry);
                    ClockInOutButton.Content = "Clock In";
                    StatusTextBlock.Text = "Current Status: Clocked Out";
                    BreakButton.IsEnabled = false;
                    AddActionLog("CLOCKED OUT", now);
                    currentTimeEntry = null;
                    await UpdateTotalWorkedHours();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in ClockInOut_Click: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                MessageBox.Show($"Error during clock in/out: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            // Implement logout logic
            var loginWindow = new LoginWindow();
            loginWindow.Show();
            this.Close();
        }

        private async void Break_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Debug.WriteLine("Break button clicked");
                Debug.WriteLine($"Current time entry: {currentTimeEntry?.Id}");
                Debug.WriteLine($"Current break: {currentBreak?.Id}");
                Debug.WriteLine($"Break button content: {BreakButton.Content}");

                if (currentTimeEntry != null)
                {
                    Debug.WriteLine("Current time entry exists");

                    // Get the actual text from the TextBlock
                    string buttonText = ((TextBlock)BreakButton.Content).Text;
                    Debug.WriteLine($"Button text: {buttonText}");

                    if (buttonText == "Start Break")
                    {
                        Debug.WriteLine("Starting new break");

                        var newBreak = new Break
                        {
                            EntryId = currentTimeEntry.Id,
                            StartTime = DateTime.UtcNow
                        };

                        Debug.WriteLine($"Creating new break for entry ID: {newBreak.EntryId}");
                        Debug.WriteLine($"Break start time: {newBreak.StartTime}");

                        try
                        {
                            var response = await supabase.From<Break>().Insert(newBreak);
                            Debug.WriteLine($"Break creation response received. Models count: {response.Models.Count}");

                            if (response.Models.Count > 0)
                            {
                                currentBreak = response.Models[0];
                                Debug.WriteLine($"New break created with ID: {currentBreak.Id}");

                                BreakButton.Content = new TextBlock { Text = "End Break" };
                                StatusTextBlock.Text = "Current Status: On Break";
                                AddActionLog("BREAK STARTED", DateTime.Now);
                                ShowBreakReminder();

                                // Update the UI immediately
                                await UpdateTotalBreakDuration();
                                await LoadTimeEntries();
                            }
                            else
                            {
                                Debug.WriteLine("Break creation failed - no models returned");
                                MessageBox.Show("Failed to start break. Please try again.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                        }
                        catch (Exception insertEx)
                        {
                            Debug.WriteLine($"Error inserting break: {insertEx.Message}");
                            throw;
                        }
                    }
                    else
                    {
                        Debug.WriteLine("Attempting to end break");
                        await EndBreak();
                    }
                }
                else
                {
                    Debug.WriteLine("No current time entry found - cannot start break");
                    MessageBox.Show("You must be clocked in to take a break.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during break operation: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                MessageBox.Show($"Error during break: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task EndBreak()
        {
            try
            {
                Debug.WriteLine("EndBreak method called");
                Debug.WriteLine($"Current break ID: {currentBreak?.Id}");

                if (currentBreak != null)
                {
                    Debug.WriteLine("Current break exists, setting end time");
                    currentBreak.EndTime = DateTime.UtcNow;

                    try
                    {
                        var updateResponse = await supabase.From<Break>().Update(currentBreak);
                        Debug.WriteLine($"Break update response received. Success: {updateResponse.Models.Count > 0}");

                        if (updateResponse.Models.Count > 0)
                        {
                            BreakButton.Content = new TextBlock { Text = "Start Break" };
                            StatusTextBlock.Text = "Current Status: Clocked In";
                            AddActionLog("BREAK ENDED", DateTime.Now);
                            HideBreakReminder();

                            // Calculate break duration
                            TimeSpan breakDuration = currentBreak.EndTime.Value - currentBreak.StartTime;
                            Debug.WriteLine($"Break duration: {breakDuration:hh\\:mm\\:ss}");

                            // Update the time entry's total break duration
                            currentTimeEntry.TotalBreakDuration += breakDuration;
                            Debug.WriteLine($"Updated total break duration: {currentTimeEntry.TotalBreakDuration:hh\\:mm\\:ss}");

                            var entryUpdateResponse = await supabase.From<TimeEntry>().Update(currentTimeEntry);
                            Debug.WriteLine($"Time entry update response received. Success: {entryUpdateResponse.Models.Count > 0}");

                            currentBreak = null;

                            // Refresh the UI
                            await LoadTimeEntries();
                            await UpdateTotalBreakDuration();
                            await UpdateTotalWorkedHours();
                        }
                        else
                        {
                            Debug.WriteLine("Break update failed - no models returned");
                            MessageBox.Show("Failed to end break. Please try again.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    catch (Exception updateEx)
                    {
                        Debug.WriteLine($"Error updating break: {updateEx.Message}");
                        throw;
                    }
                }
                else
                {
                    Debug.WriteLine("No current break found to end");
                    MessageBox.Show("No active break found to end.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in EndBreak method: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        private async Task LoadTimeEntries()
{
    try
    {
        Debug.WriteLine($"Loading time entries for today: {DateTime.Today:yyyy-MM-dd}");

        // Get today's time entries
        var timeEntries = await supabase.From<TimeEntry>()
            .Filter("user_id", Postgrest.Constants.Operator.Equals, currentUser.Id.ToString())
            .Get();

        // Filter for today's entries
        var todayEntries = timeEntries.Models
            .Where(entry => entry.ClockInTime.Date == DateTime.Today)
            .OrderBy(entry => entry.ClockInTime)
            .ToList();

        Debug.WriteLine($"Found {todayEntries.Count} entries for today");

        // Get break entries for today's time entries
        var breakEntries = new List<Break>();
        if (todayEntries.Any())
        {
            var entryIds = todayEntries.Select(te => te.Id.ToString()).ToList();
            var breaks = await supabase.From<Break>()
                .Filter("entry_id", Postgrest.Constants.Operator.In, entryIds)
                .Get();

            breakEntries = breaks.Models
                .Where(b => b.StartTime.Date == DateTime.Today)
                .OrderBy(b => b.StartTime)
                .ToList();

            Debug.WriteLine($"Found {breakEntries.Count} breaks for today");
        }

        ActionLogs.Clear();
        foreach (var entry in todayEntries)
        {
            Debug.WriteLine($"Processing time entry: {entry.Id}");
            Debug.WriteLine($"Clock In Time: {entry.ClockInTime:yyyy-MM-dd HH:mm:ss}");

            // Add clock in log
            AddActionLog("CLOCKED IN", entry.ClockInTime);

            // Add break logs for this entry
            var entryBreaks = breakEntries.Where(b => b.EntryId == entry.Id);
            foreach (var breakEntry in entryBreaks)
            {
                Debug.WriteLine($"Processing break: {breakEntry.Id}");
                Debug.WriteLine($"Break Start: {breakEntry.StartTime:yyyy-MM-dd HH:mm:ss}");
                
                AddActionLog("BREAK STARTED", breakEntry.StartTime);
                if (breakEntry.EndTime.HasValue)
                {
                    Debug.WriteLine($"Break End: {breakEntry.EndTime.Value:yyyy-MM-dd HH:mm:ss}");
                    AddActionLog("BREAK ENDED", breakEntry.EndTime.Value);
                }
            }

            // Add clock out log if exists
            if (entry.ClockOutTime.HasValue)
            {
                Debug.WriteLine($"Clock Out Time: {entry.ClockOutTime.Value:yyyy-MM-dd HH:mm:ss}");
                AddActionLog("CLOCKED OUT", entry.ClockOutTime.Value);
            }
        }

        Debug.WriteLine($"Total activities logged: {ActionLogs.Count}");
    }
    catch (Exception ex)
    {
        Debug.WriteLine($"Error loading time entries: {ex.Message}");
        Debug.WriteLine($"Stack trace: {ex.StackTrace}");
        MessageBox.Show($"Error loading time entries: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}


        private void AddActionLog(string action, DateTime timestamp)
        {
            // Ensure the timestamp is treated as a local time
            var localTimestamp = DateTime.SpecifyKind(timestamp, DateTimeKind.Local);
            ActionLogs.Add(new ActionLog { Action = action, Timestamp = localTimestamp });
        }

        private async Task<TimeEntry> GetLatestTimeEntry()
        {
            try
            {
                var entries = await supabase.From<TimeEntry>()
                    .Filter("user_id", Postgrest.Constants.Operator.Equals, currentUser.Id.ToString())
                    .Order("clock_in_time", Postgrest.Constants.Ordering.Descending)
                    .Limit(1)
                    .Get();

                return entries.Models.FirstOrDefault();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error fetching latest time entry: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }

        private async Task<Break> GetActiveBreak(Guid timeEntryId)
        {
            try
            {
                var breaks = await supabase.From<Break>()
                    .Filter("entry_id", Postgrest.Constants.Operator.Equals, timeEntryId.ToString())
                    .Get();

                return breaks.Models.FirstOrDefault(b => b.EndTime == null);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error fetching active break: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }

        private async Task UpdateTotalWorkedHours()
        {
            try
            {
                Debug.WriteLine($"Updating total worked hours for today: {DateTime.Today:yyyy-MM-dd}");

                // Get today's time entries
                var timeEntries = await supabase.From<TimeEntry>()
                    .Filter("user_id", Postgrest.Constants.Operator.Equals, currentUser.Id.ToString())
                    .Get();

                // Filter entries for today manually
                var todayEntries = timeEntries.Models.Where(entry =>
                    entry.ClockInTime.Date == DateTime.Today).ToList();

                TimeSpan totalWorked = TimeSpan.Zero;

                foreach (var entry in todayEntries)
                {
                    Debug.WriteLine($"Processing entry - ID: {entry.Id}");
                    Debug.WriteLine($"Clock In: {entry.ClockInTime:yyyy-MM-dd HH:mm:ss}");
                    Debug.WriteLine($"Clock Out: {entry.ClockOutTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Still clocked in"}");
                    Debug.WriteLine($"Total Break Duration: {entry.TotalBreakDuration:hh\\:mm\\:ss}");

                    // Calculate work duration for this entry
                    DateTime endTime = entry.ClockOutTime ?? DateTime.Now;
                    var workDuration = endTime - entry.ClockInTime;

                    // Subtract break duration if there is any
                    if (entry.TotalBreakDuration > TimeSpan.Zero)
                    {
                        workDuration -= entry.TotalBreakDuration;
                    }

                    Debug.WriteLine($"Work duration for this entry before adjustment: {workDuration:hh\\:mm\\:ss}");

                    // Ensure we don't have negative duration
                    if (workDuration > TimeSpan.Zero)
                    {
                        totalWorked += workDuration;
                        Debug.WriteLine($"Added to total worked: {workDuration:hh\\:mm\\:ss}");
                    }
                }

                Debug.WriteLine($"Final total worked hours: {totalWorked:hh\\:mm\\:ss}");
                TotalWorkedHoursTextBlock.Text = $"Today's Work Hours: {totalWorked:hh\\:mm}";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error calculating work hours: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                MessageBox.Show($"Error updating today's worked hours: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task UpdateTotalBreakDuration()
        {
            try
            {
                Debug.WriteLine($"Updating break duration for today: {DateTime.Today:yyyy-MM-dd}");

                // Get all time entries for today
                var timeEntries = await supabase.From<TimeEntry>()
                    .Filter("user_id", Postgrest.Constants.Operator.Equals, currentUser.Id.ToString())
                    .Get();

                // Filter entries for today manually
                var todayEntries = timeEntries.Models.Where(entry =>
                    entry.ClockInTime.Date == DateTime.Today).ToList();

                TimeSpan totalBreakDuration = TimeSpan.Zero;

                foreach (var entry in todayEntries)
                {
                    Debug.WriteLine($"Processing breaks for entry ID: {entry.Id}");

                    var breaks = await supabase.From<Break>()
                        .Filter("entry_id", Postgrest.Constants.Operator.Equals, entry.Id.ToString())
                        .Get();

                    var entryBreaks = breaks.Models.Where(b => b.StartTime.Date == DateTime.Today).ToList();

                    foreach (var breakEntry in entryBreaks)
                    {
                        DateTime breakStart = breakEntry.StartTime;
                        DateTime breakEnd = breakEntry.EndTime ?? DateTime.Now;

                        var breakDuration = breakEnd - breakStart;
                        Debug.WriteLine($"Break ID: {breakEntry.Id}");
                        Debug.WriteLine($"Break Start: {breakStart:yyyy-MM-dd HH:mm:ss}");
                        Debug.WriteLine($"Break End: {breakEnd:yyyy-MM-dd HH:mm:ss}");
                        Debug.WriteLine($"Break Duration: {breakDuration:hh\\:mm\\:ss}");

                        if (breakDuration > TimeSpan.Zero)
                        {
                            totalBreakDuration += breakDuration;
                            Debug.WriteLine($"Added to total break duration: {breakDuration:hh\\:mm\\:ss}");
                        }
                    }
                }

                Debug.WriteLine($"Final total break duration: {totalBreakDuration:hh\\:mm\\:ss}");
                TotalBreakDurationTextBlock.Text = $"Today's Break Duration: {totalBreakDuration:hh\\:mm}";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error calculating break duration: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                MessageBox.Show($"Error updating today's break duration: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private DateTime ConvertToLocalTime(DateTime utcTime)
        {
            return DateTime.SpecifyKind(utcTime, DateTimeKind.Utc).ToLocalTime();
        }

        private void ShowBreakReminder()
        {
            breakReminderWindow.Show();
        }

        private void HideBreakReminder()
        {
            breakReminderWindow.Hide();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            breakReminderWindow.Close();
            base.OnClosing(e);
        }
    }

    public class ActionLog
    {
        public string Action { get; set; }
        public DateTime Timestamp { get; set; }

        // Override ToString to format the time display
        public override string ToString()
        {
            return $"{Timestamp.ToString("yyyy-MM-dd HH:mm:ss")} - {Action}";
        }
    }
}