using Supabase;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using static Postgrest.Constants;

namespace ClockIn
{
    public partial class AdminDashboard : Window
    {
        private Client _supabase;
        private User _currentUser;
        private ObservableCollection<UserStatus> _users;
        private ListView _liveView;
        private System.Windows.Threading.DispatcherTimer _timer;
        private static readonly TimeZoneInfo skopjeTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time");


        public ObservableCollection<UserStatus> Users
        {
            get
            {
                if (_users == null)
                {
                    Debug.WriteLine("Users collection was null. Initializing it.");
                    _users = new ObservableCollection<UserStatus>();
                }
                return _users;
            }
            set
            {
                _users = value;
            }
        }

        public AdminDashboard(Client supabase, User currentUser)
        {
            InitializeComponent();
            _supabase = supabase;
            _currentUser = currentUser;
            Users = new ObservableCollection<UserStatus>();
            DataContext = this;

            InitializeLiveView();
            StartPeriodicDataRefresh();
        }

        private void InitializeLiveView()
        {
            _liveView = new ListView { ItemsSource = Users };
            _liveView.View = new GridView
            {
                Columns =
                {
                    new GridViewColumn { Header = "Username", DisplayMemberBinding = new Binding("Username"), Width = 150 },
                    new GridViewColumn { Header = "Status", DisplayMemberBinding = new Binding("Status"), Width = 100 },
                    new GridViewColumn { Header = "Check In Time", DisplayMemberBinding = new Binding("CheckInTime") { StringFormat = "HH:mm:ss" }, Width = 100 },
                    new GridViewColumn { Header = "Check Out Time", DisplayMemberBinding = new Binding("CheckOutTime") { StringFormat = "HH:mm:ss" }, Width = 100 },
                    new GridViewColumn { Header = "Break Count", DisplayMemberBinding = new Binding("BreakCount"), Width = 80 },
                    new GridViewColumn { Header = "Total Break Duration", DisplayMemberBinding = new Binding("TotalBreakDuration") { StringFormat = @"hh\:mm\:ss" }, Width = 120 },
                    new GridViewColumn { Header = "Actions", Width = 200, CellTemplate = CreateActionButtonTemplate() }
                }
            };

            MainContent.Content = _liveView;
        }
        #region
        private DataTemplate CreateActionButtonTemplate()
        {
            var template = new DataTemplate();
            var stackPanel = new FrameworkElementFactory(typeof(StackPanel));
            stackPanel.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);

            var checkOutButton = new FrameworkElementFactory(typeof(Button));
            checkOutButton.SetValue(Button.ContentProperty, "Check Out");
            checkOutButton.SetValue(Button.MarginProperty, new Thickness(0, 0, 5, 0));
            checkOutButton.AddHandler(Button.ClickEvent, new RoutedEventHandler(CheckOutButton_Click));

            var breakButton = new FrameworkElementFactory(typeof(Button));
            breakButton.SetValue(Button.ContentProperty, "Start Break");
            breakButton.AddHandler(Button.ClickEvent, new RoutedEventHandler(BreakButton_Click));

            stackPanel.AppendChild(checkOutButton);
            stackPanel.AppendChild(breakButton);

            template.VisualTree = stackPanel;
            return template;
        }

        private async void CheckOutButton_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            var userStatus = (UserStatus)button.DataContext;
            await CheckOutUser(userStatus.UserId);
        }

        private async void BreakButton_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            var userStatus = (UserStatus)button.DataContext;
            if (userStatus.Status == "On Break")
            {
                await EndBreak(userStatus.UserId);
            }
            else
            {
                await StartBreak(userStatus.UserId);
            }
        }

        private async Task CheckOutUser(Guid userId)
        {
            try
            {
                var latestEntry = await GetLatestTimeEntry(userId);
                if (latestEntry != null && !latestEntry.ClockOutTime.HasValue)
                {
                    latestEntry.ClockOutTime = DateTime.UtcNow;
                    await _supabase.From<TimeEntry>().Update(latestEntry);
                    await RefreshLiveData();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error checking out user: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task StartBreak(Guid userId)
        {
            try
            {
                var latestEntry = await GetLatestTimeEntry(userId);
                if (latestEntry != null && !latestEntry.ClockOutTime.HasValue)
                {
                    var newBreak = new Break
                    {
                        EntryId = latestEntry.Id,
                        StartTime = DateTime.UtcNow
                    };
                    await _supabase.From<Break>().Insert(newBreak);
                    //latestEntry.BreakCount++;
                    await _supabase.From<TimeEntry>().Update(latestEntry);
                    await RefreshLiveData();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error starting break: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task EndBreak(Guid userId)
        {
            try
            {
                var latestEntry = await GetLatestTimeEntry(userId);
                if (latestEntry != null)
                {
                    var latestBreak = await _supabase
                        .From<Break>()
                        .Where(b => b.EntryId == latestEntry.Id && b.EndTime == null)
                        .Single();

                    if (latestBreak != null)
                    {
                        latestBreak.EndTime = DateTime.UtcNow;
                        await _supabase.From<Break>().Update(latestBreak);

                        latestEntry.TotalBreakDuration += latestBreak.EndTime.Value - latestBreak.StartTime;
                        await _supabase.From<TimeEntry>().Update(latestEntry);

                        await RefreshLiveData();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error ending break: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task<TimeEntry> GetLatestTimeEntry(Guid userId)
        {
            var entries = await _supabase
                .From<TimeEntry>()
                .Where(te => te.UserId == userId)
                .Order("clock_in_time", Ordering.Descending)
                .Limit(1)
                .Get();

            return entries.Models.FirstOrDefault();
        }

        private string DetermineStatus(TimeEntry latestEntry, List<Break> userBreaks)
        {
            if (latestEntry == null) return "Offline";
            if (latestEntry.ClockOutTime.HasValue) return "Offline";

            var latestBreak = userBreaks.OrderByDescending(b => b.StartTime).FirstOrDefault();
            if (latestBreak != null && !latestBreak.EndTime.HasValue) return "On Break";

            return "Checked In";
        }

        private DateTime ToSkopjeTime(DateTime utcTime)
        {
            return TimeZoneInfo.ConvertTimeFromUtc(utcTime, skopjeTimeZone);
        }

        #endregion
        private void StartPeriodicDataRefresh()
        {
            _timer = new System.Windows.Threading.DispatcherTimer();
            _timer.Tick += async (sender, e) => await RefreshLiveData();
            _timer.Interval = TimeSpan.FromSeconds(30); // Refresh every 30 seconds
            _timer.Start();

            // Initial data load
            RefreshLiveData();
        }

        private async Task RefreshLiveData()
        {
            try
            {
                var timeEntries = await _supabase
                    .From<TimeEntry>()
                    .Select("id, user_id, clock_in_time, clock_out_time, total_break_duration, date")
                    .Order("date", Ordering.Descending)
                    .Get();

                var breaks = await _supabase
                    .From<Break>()
                    .Select("entry_id, start_time, end_time")
                    .Get();

                var users = await _supabase
                    .From<User>()
                    .Where(u => u.CompanyId == _currentUser.CompanyId)
                    .Select("id, username")
                    .Get();

                var userStatuses = users.Models.Select(user =>
                {
                    var userEntries = timeEntries.Models.Where(te => te.UserId == user.Id).ToList();
                    var latestEntry = userEntries.OrderByDescending(e => e.Date).ThenByDescending(e => e.ClockInTime).FirstOrDefault();
                    var userBreaks = breaks.Models
                        .Where(b => userEntries.Any(te => te.Id == b.EntryId))
                        .ToList();

                    var totalWorkedHours = CalculateTotalWorkedHours(userEntries, userBreaks);
                    var totalOvertimeHours = CalculateOvertimeHours(totalWorkedHours);

                    return new UserStatus
                    {
                        UserId = user.Id,
                        Username = user.Username,
                        Status = DetermineStatus(latestEntry, userBreaks),
                        CheckInTime = latestEntry?.ClockInTime,
                        CheckOutTime = latestEntry?.ClockOutTime,
                        BreakCount = userBreaks.Count,
                        TotalBreakDuration = CalculateTotalBreakDuration(userBreaks),
                        TotalWorkedHours = totalWorkedHours,
                        OvertimeHours = totalOvertimeHours
                    };
                }).ToList();

                Application.Current.Dispatcher.Invoke(() =>
                {
                    Users.Clear();
                    foreach (var status in userStatuses)
                    {
                        Users.Add(status);
                    }
                });

                Debug.WriteLine($"Refreshed data for {userStatuses.Count} users");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error refreshing data: {ex.Message}");
                MessageBox.Show($"Error refreshing data: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private TimeSpan CalculateTotalWorkedHours(List<TimeEntry> entries, List<Break> breaks)
        {
            var groupedEntries = entries.GroupBy(e => e.Date.Date)
                                        .ToDictionary(g => g.Key, g => g.ToList());

            TimeSpan totalWorked = TimeSpan.Zero;
            foreach (var dailyEntries in groupedEntries.Values)
            {
                var sortedEntries = dailyEntries.OrderBy(e => e.ClockInTime).ToList();
                DateTime? firstClockIn = null;
                DateTime? lastClockOut = null;
                TimeSpan dailyBreakDuration = TimeSpan.Zero;

                foreach (var entry in sortedEntries)
                {
                    if (!firstClockIn.HasValue)
                    {
                        firstClockIn = entry.ClockInTime;
                    }

                    if (entry.ClockOutTime.HasValue)
                    {
                        lastClockOut = entry.ClockOutTime;
                    }

                    var entryBreaks = breaks.Where(b => b.EntryId == entry.Id).ToList();
                    if (entryBreaks.Any())
                    {
                        var lastBreak = entryBreaks.OrderBy(b => b.StartTime).Last();
                        dailyBreakDuration += (lastBreak.EndTime ?? DateTime.UtcNow) - lastBreak.StartTime;
                    }
                }

                if (firstClockIn.HasValue && lastClockOut.HasValue)
                {
                    var dailyWorked = (lastClockOut.Value - firstClockIn.Value) - dailyBreakDuration;
                    totalWorked += dailyWorked;
                    Debug.WriteLine($"Date: {firstClockIn.Value.Date}, Worked: {dailyWorked}, Breaks: {dailyBreakDuration}");
                }
            }

            return totalWorked;
        }

        private TimeSpan CalculateOvertimeHours(TimeSpan totalWorkedHours)
        {
            var regularHours = TimeSpan.FromHours(8); // Assuming 8 hours is a regular workday
            return totalWorkedHours > regularHours ? totalWorkedHours - regularHours : TimeSpan.Zero;
        }


        private TimeSpan CalculateTotalBreakDuration(List<Break> userBreaks)
        {
            TimeSpan totalBreakDuration = TimeSpan.Zero;
            var groupedBreaks = userBreaks.GroupBy(b => b.EntryId);

            foreach (var group in groupedBreaks)
            {
                var lastBreak = group.OrderBy(b => b.StartTime).LastOrDefault();
                if (lastBreak != null)
                {
                    totalBreakDuration += (lastBreak.EndTime ?? DateTime.UtcNow) - lastBreak.StartTime;
                }
            }

            return totalBreakDuration;
        }

        private void NavButton_Checked(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("NavButton_Checked called");
            if (sender is RadioButton radioButton)
            {
                switch (radioButton.Tag.ToString())
                {
                    case "Users":
                        ShowUsersView();
                        break;
                    case "Reports":
                        ShowReportsView();
                        break;
                    case "Live":
                        ShowLiveView();
                        break;
                    case "Settings":
                        ShowSettingsView();
                        break;
                }
            }
        }

        private void ShowUsersView()
        {
            _timer.Stop();
            UpdateHeaderText("User Management");
            UpdateContentHeaderText("User List");
            if (MainContent != null)
            {
                MainContent.Content = new UsersView(_supabase, _currentUser);
            }
            else
            {
                Debug.WriteLine("MainContent is null in ShowUsersView");
            }
        }

        private void ShowReportsView()
        {
            _timer.Stop();
            Debug.WriteLine("ShowReportsView called");
            UpdateHeaderText("Reports");
            UpdateContentHeaderText("Report Summary");
            if (MainContent != null)
            {
                MainContent.Content = new ReportsView(_supabase, _currentUser);
            }
            else
            {
                Debug.WriteLine("MainContent is null in ShowReportsView");
            }
        }

        private void ShowLiveView()
        {
            Debug.WriteLine("ShowLiveView called");
            Dispatcher.Invoke(() =>
            {
                UpdateHeaderText("Live Status Dashboard");
                UpdateContentHeaderText("User Status Terminal");
                if (MainContent != null)
                {
                    MainContent.Content = _liveView;
                    _timer.Start();
                    RefreshLiveData();
                }
                else
                {
                    Debug.WriteLine("MainContent is null in ShowLiveView");
                }
            });
        }

        private void ShowSettingsView()
        {
            _timer.Stop();
            Debug.WriteLine("ShowSettingsView called");
            UpdateHeaderText("Settings");
            UpdateContentHeaderText("Application Settings");
            if (MainContent != null)
            {
                MainContent.Content = new SettingsView(_supabase, _currentUser);
            }
            else
            {
                Debug.WriteLine("MainContent is null in ShowSettingsView");
            }
        }

        private void UpdateHeaderText(string text)
        {
            if (HeaderTextBlock != null)
            {
                HeaderTextBlock.Text = text;
            }
            else
            {
                Debug.WriteLine($"HeaderTextBlock is null when trying to set text: {text}");
            }
        }

        private void UpdateContentHeaderText(string text)
        {
            if (ContentHeaderTextBlock != null)
            {
                ContentHeaderTextBlock.Text = text;
            }
            else
            {
                Debug.WriteLine($"ContentHeaderTextBlock is null when trying to set text: {text}");
            }
        }
    }

    // Placeholder UserControl classes for other view

    public class SettingsView : UserControl
    {
        public SettingsView(Client supabase, User currentUser)
        {
            // Implement Settings view
            var textBlock = new TextBlock
            {
                Text = "Settings View",
                FontSize = 24,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            this.Content = textBlock;
        }
    }
}