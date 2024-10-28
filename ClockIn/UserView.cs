using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Supabase;
using System.Threading.Tasks;
using System.Diagnostics;

namespace ClockIn
{
    public class UsersView : UserControl
    {
        private Client _supabase;
        private User _currentUser;
        private ObservableCollection<User> _users;
        private TextBox usernameTextBox;
        private TextBox emailTextBox;
        private PasswordBox passwordBox;

        public UsersView(Client supabase, User currentUser)
        {
            _supabase = supabase;
            _currentUser = currentUser;
            _users = new ObservableCollection<User>();

            InitializeComponent();
            LoadUsers();
        }

        private void InitializeComponent()
        {
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // User creation form
            var createUserPanel = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(0, 0, 0, 20) };

            createUserPanel.Children.Add(new TextBlock
            {
                Text = "Create New User",
                FontWeight = FontWeights.Bold,
                FontSize = 18,
                Margin = new Thickness(0, 0, 0, 15)
            });

            // Username field
            createUserPanel.Children.Add(CreateLabeledTextBox("Username", out usernameTextBox));

            // Email field
            createUserPanel.Children.Add(CreateLabeledTextBox("Email", out emailTextBox));

            // Password field
            var passwordStackPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
            passwordStackPanel.Children.Add(new TextBlock { Text = "Password", Margin = new Thickness(0, 0, 0, 5) });
            passwordBox = new PasswordBox { Margin = new Thickness(0, 0, 0, 5), Padding = new Thickness(5) };
            passwordStackPanel.Children.Add(passwordBox);
            createUserPanel.Children.Add(passwordStackPanel);

            var createButton = new Button
            {
                Content = "Create User",
                Margin = new Thickness(0, 10, 0, 0),
                Padding = new Thickness(10, 5, 10, 5),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#007bff")),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0)
            };
            createButton.Click += async (sender, e) => await CreateUser();
            createUserPanel.Children.Add(createButton);

            Grid.SetRow(createUserPanel, 0);
            grid.Children.Add(createUserPanel);

            // Users list
            var usersListLabel = new TextBlock
            {
                Text = "Existing Users",
                FontWeight = FontWeights.Bold,
                FontSize = 18,
                Margin = new Thickness(0, 0, 0, 10)
            };
            Grid.SetRow(usersListLabel, 1);
            grid.Children.Add(usersListLabel);

            var usersList = new ListView
            {
                ItemsSource = _users,
                Margin = new Thickness(0, 30, 0, 0)
            };
            usersList.View = new GridView
            {
                Columns =
                {
                    new GridViewColumn { Header = "Username", DisplayMemberBinding = new Binding("Username"), Width = 150 },
                    new GridViewColumn { Header = "Email", DisplayMemberBinding = new Binding("Email"), Width = 200 },
                    new GridViewColumn { Header = "Is Admin", DisplayMemberBinding = new Binding("IsAdmin"), Width = 100 }
                }
            };
            Grid.SetRow(usersList, 1);
            grid.Children.Add(usersList);

            this.Content = grid;
        }

        private StackPanel CreateLabeledTextBox(string labelText, out TextBox textBox)
        {
            var stackPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
            stackPanel.Children.Add(new TextBlock { Text = labelText, Margin = new Thickness(0, 0, 0, 5) });
            textBox = new TextBox { Margin = new Thickness(0, 0, 0, 5), Padding = new Thickness(5) };
            stackPanel.Children.Add(textBox);
            return stackPanel;
        }

        private async Task LoadUsers()
        {
            try
            {
                var response = await _supabase
                    .From<User>()
                    .Where(u => u.CompanyId == _currentUser.CompanyId)
                    .Get();

                _users.Clear();
                foreach (var user in response.Models)
                {
                    _users.Add(user);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading users: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task CreateUser()
        {
            string username = usernameTextBox?.Text ?? string.Empty;
            string email = emailTextBox?.Text ?? string.Empty;
            string password = passwordBox?.Password ?? string.Empty;

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("Please fill in all fields.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var newUser = new User
                {
                    Username = username,
                    Email = email,
                    CompanyId = _currentUser.CompanyId,
                    IsAdmin = false,
                    CreatedAt = DateTime.UtcNow
                };
                newUser.SetPassword(password);

                var response = await _supabase.From<User>().Insert(newUser);
                if (response.Models.Count > 0)
                {
                    MessageBox.Show("User created successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    await LoadUsers(); // Refresh the user list
                    ClearInputFields();
                }
                else
                {
                    MessageBox.Show("Failed to create user. Please try again.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating user: {ex.Message}");
                MessageBox.Show($"Error creating user: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearInputFields()
        {
            if (usernameTextBox != null) usernameTextBox.Text = string.Empty;
            if (emailTextBox != null) emailTextBox.Text = string.Empty;
            if (passwordBox != null) passwordBox.Password = string.Empty;
        }
    }
}