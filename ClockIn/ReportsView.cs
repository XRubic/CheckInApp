using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Supabase;
using System.Threading.Tasks;
using System.Diagnostics;
using Postgrest.Models;

namespace ClockIn
{
    public class ReportsView : UserControl
    {
        private Client _supabase;
        private User _currentUser;
        private ObservableCollection<UserReport> _userReports;
        private DatePicker _startDatePicker;
        private DatePicker _endDatePicker;
        private ComboBox _userSelector;
        private DataGrid _reportDataGrid;
        private TextBlock _summaryTextBlock;


        public ReportsView(Client supabase, User currentUser)
        {
            _supabase = supabase;
            _currentUser = currentUser;
            _userReports = new ObservableCollection<UserReport>();

            InitializeComponent();
            LoadUsers();
            SetDefaultDateRange();
        }

        private void SetDefaultDateRange()
        {
            _endDatePicker.SelectedDate = DateTime.Today;
            _startDatePicker.SelectedDate = DateTime.Today.AddDays(-7);
        }

        private void InitializeComponent()
        {
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Controls panel
            var controlsPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };

            _startDatePicker = new DatePicker { Margin = new Thickness(0, 0, 10, 0) };
            _endDatePicker = new DatePicker { Margin = new Thickness(0, 0, 10, 0) };
            _userSelector = new ComboBox { Margin = new Thickness(0, 0, 10, 0), MinWidth = 100 };
            var generateReportButton = new Button { Content = "Generate Report", Padding = new Thickness(10, 5, 10, 5) };
            generateReportButton.Click += GenerateReport_Click;

            controlsPanel.Children.Add(new TextBlock { Text = "Start Date:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 5, 0) });
            controlsPanel.Children.Add(_startDatePicker);
            controlsPanel.Children.Add(new TextBlock { Text = "End Date:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 5, 0) });
            controlsPanel.Children.Add(_endDatePicker);
            controlsPanel.Children.Add(new TextBlock { Text = "User:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 5, 0) });
            controlsPanel.Children.Add(_userSelector);
            controlsPanel.Children.Add(generateReportButton);

            Grid.SetRow(controlsPanel, 0);
            grid.Children.Add(controlsPanel);

            // Summary text block
            _summaryTextBlock = new TextBlock { Margin = new Thickness(0, 0, 0, 10) };
            Grid.SetRow(_summaryTextBlock, 1);
            grid.Children.Add(_summaryTextBlock);

            // Report data grid
            _reportDataGrid = new DataGrid
            {
                AutoGenerateColumns = false,
                IsReadOnly = true,
                ItemsSource = _userReports
            };
            _reportDataGrid.Columns.Add(new DataGridTextColumn { Header = "Date", Binding = new System.Windows.Data.Binding("CheckInTime") { StringFormat = "MM/dd/yyyy" } });
            _reportDataGrid.Columns.Add(new DataGridTextColumn { Header = "Check In", Binding = new System.Windows.Data.Binding("CheckInTime") { StringFormat = "HH:mm:ss" } });
            _reportDataGrid.Columns.Add(new DataGridTextColumn { Header = "Check Out", Binding = new System.Windows.Data.Binding("CheckOutTime") { StringFormat = "HH:mm:ss" } });
            _reportDataGrid.Columns.Add(new DataGridTextColumn { Header = "Total Breaks", Binding = new System.Windows.Data.Binding("TotalBreaks") });
            _reportDataGrid.Columns.Add(new DataGridTextColumn { Header = "Break Duration", Binding = new System.Windows.Data.Binding("TotalBreakDuration") { StringFormat = @"hh\:mm\:ss" } });
            _reportDataGrid.Columns.Add(new DataGridTextColumn { Header = "Work Duration", Binding = new System.Windows.Data.Binding("WorkDuration") { StringFormat = @"hh\:mm\:ss" } });
            _reportDataGrid.Columns.Add(new DataGridTextColumn { Header = "Overtime", Binding = new System.Windows.Data.Binding("Overtime") { StringFormat = @"hh\:mm\:ss" } });


            Grid.SetRow(_reportDataGrid, 2);
            grid.Children.Add(_reportDataGrid);

            this.Content = grid;

            LoadUsers();
        }

        private async void LoadUsers()
        {
            try
            {
                Debug.WriteLine("Starting to load users");
                var users = await _supabase
                    .From<User>()
                    .Where(u => u.CompanyId == _currentUser.CompanyId && u.IsAdmin == false)
                    .Get();

                Debug.WriteLine($"Retrieved {users.Models.Count} non-admin users");

                _userSelector.Items.Clear();
                foreach (var user in users.Models)
                {
                    _userSelector.Items.Add(new ComboBoxItem { Content = user.Username, Tag = user.Id });
                }

                if (_userSelector.Items.Count > 0)
                {
                    _userSelector.SelectedIndex = 0;
                }

                Debug.WriteLine("Users loaded successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading users: {ex.Message}");
                MessageBox.Show($"Error loading users: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void GenerateReport_Click(object sender, RoutedEventArgs e)
        {
            if (!_startDatePicker.SelectedDate.HasValue || !_endDatePicker.SelectedDate.HasValue)
            {
                MessageBox.Show("Please select both start and end dates.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var startDate = _startDatePicker.SelectedDate.Value.Date;
            var endDate = _endDatePicker.SelectedDate.Value.Date.AddDays(1).AddSeconds(-1);
            var selectedUser = _userSelector.SelectedItem as ComboBoxItem;

            if (selectedUser == null)
            {
                MessageBox.Show("Please select a user.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                Debug.WriteLine($"Generating report for date range: {startDate} to {endDate}");
                Debug.WriteLine($"Selected user: {selectedUser.Content}");
                Debug.WriteLine($"Current system time: {DateTime.Now}, UTC time: {DateTime.UtcNow}");

                // Fetch time entries for the selected user
                var userId = (Guid)selectedUser.Tag;
                var timeEntriesQuery = _supabase.From<TimeEntry>()
                    .Filter("date", Postgrest.Constants.Operator.GreaterThanOrEqual, startDate.ToString("yyyy-MM-dd"))
                    .Filter("date", Postgrest.Constants.Operator.LessThanOrEqual, endDate.ToString("yyyy-MM-dd"))
                    .Filter("user_id", Postgrest.Constants.Operator.Equals, userId.ToString());

                Debug.WriteLine("Executing time entries query");
                var timeEntries = await timeEntriesQuery.Get();
                Debug.WriteLine($"Retrieved {timeEntries.Models.Count} time entries");

                // Fetch breaks for the retrieved time entries
                var breakEntries = await _supabase.From<Break>()
                    .Filter("entry_id", Postgrest.Constants.Operator.In, timeEntries.Models.Select(te => te.Id.ToString()).ToList())
                    .Get();

                // Process time entries
                _userReports.Clear();
                foreach (var entry in timeEntries.Models)
                {
                    Debug.WriteLine($"Processing entry - Database Date: {entry.Date}, Clock In: {entry.ClockInTime}, Clock Out: {entry.ClockOutTime}");
                    var entryBreaks = breakEntries.Models.Where(b => b.EntryId == entry.Id).ToList();
                    var breakDuration = CalculateTotalBreakDuration(entryBreaks);
                    var breakCount = entryBreaks.Count;

                    var workDuration = entry.ClockOutTime.HasValue
                        ? (entry.ClockOutTime.Value - entry.ClockInTime) - breakDuration
                        : TimeSpan.Zero;
                    var overtime = workDuration > TimeSpan.FromHours(7) ? workDuration - TimeSpan.FromHours(7) : TimeSpan.Zero;

                    var userReport = new UserReport
                    {
                        Date = entry.Date,
                        CheckInTime = entry.ClockInTime,
                        CheckOutTime = entry.ClockOutTime,
                        TotalBreaks = breakCount,
                        TotalBreakDuration = breakDuration,
                        WorkDuration = workDuration,
                        Overtime = overtime
                    };

                    Debug.WriteLine($"Created UserReport - Date: {userReport.Date}, Check In: {userReport.CheckInTime}, Check Out: {userReport.CheckOutTime}");

                    _userReports.Add(userReport);

                }

                Debug.WriteLine($"Generated {_userReports.Count} user reports");

                UpdateSummary();
                Debug.WriteLine("Report generation completed successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error generating report: {ex.Message}");
                Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
                MessageBox.Show($"Error generating report: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }



        private TimeSpan CalculateTotalBreakDuration(List<Break> breaks)
        {
            return breaks.Where(b => b.EndTime.HasValue)
                         .Aggregate(TimeSpan.Zero, (total, b) => total + (b.EndTime.Value - b.StartTime));
        }

        private void UpdateSummary()
        {
            if (_userReports.Count == 0)
            {
                _summaryTextBlock.Text = "No data available for the selected period.";
                Debug.WriteLine("No data available for summary");
                return;
            }

            var totalWorkDuration = _userReports.Aggregate(TimeSpan.Zero, (total, report) => total + report.WorkDuration);
            var totalBreakDuration = _userReports.Aggregate(TimeSpan.Zero, (total, report) => total + report.TotalBreakDuration);
            var totalOvertime = _userReports.Aggregate(TimeSpan.Zero, (total, report) => total + report.Overtime);
            var totalBreaks = _userReports.Sum(report => report.TotalBreaks);

            var averageCheckIn = _userReports.Average(report => report.CheckInTime.TimeOfDay.TotalMinutes);
            var averageCheckOut = _userReports
                .Where(report => report.CheckOutTime.HasValue)
                .Average(report => report.CheckOutTime.Value.TimeOfDay.TotalMinutes);

            _summaryTextBlock.Text = $"Summary: " +
                $"Total Work: {totalWorkDuration:hh\\:mm\\:ss}, " +
                $"Total Overtime: {totalOvertime:hh\\:mm\\:ss}, " +
                $"Avg Check In: {TimeSpan.FromMinutes(averageCheckIn):hh\\:mm}, " +
                $"Avg Check Out: {TimeSpan.FromMinutes(averageCheckOut):hh\\:mm}, " +
                $"Total Breaks: {totalBreaks}, " +
                $"Total Break Duration: {totalBreakDuration:hh\\:mm\\:ss}";

            Debug.WriteLine($"Summary updated: {_summaryTextBlock.Text}");
        }

    }

    public class UserReport
    {
        public DateTime Date { get; set; }
        public DateTime CheckInTime { get; set; }
        public DateTime? CheckOutTime { get; set; }
        public int TotalBreaks { get; set; }
        public TimeSpan TotalBreakDuration { get; set; }
        public TimeSpan WorkDuration { get; set; }
        public TimeSpan Overtime { get; set; }
    }
}