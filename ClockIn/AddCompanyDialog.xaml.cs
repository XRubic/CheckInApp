using System.Windows;

namespace ClockIn
{
    public partial class AddCompanyDialog : Window
    {
        public string CompanyId { get; private set; }
        public string CompanyName { get; private set; }
        public string AdminUsername { get; private set; }
        public string AdminEmail { get; private set; }
        public string AdminPassword { get; private set; }

        public AddCompanyDialog(string existingId = "", string existingName = "")
        {
            InitializeComponent();
            CompanyIdTextBox.Text = existingId;
            CompanyNameTextBox.Text = existingName;

            // If we're editing, disable the admin fields
            if (!string.IsNullOrEmpty(existingId))
            {
                AdminUsernameTextBox.IsEnabled = false;
                AdminEmailTextBox.IsEnabled = false;
                AdminPasswordBox.IsEnabled = false;
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            CompanyId = CompanyIdTextBox.Text;
            CompanyName = CompanyNameTextBox.Text;
            AdminUsername = AdminUsernameTextBox.Text;
            AdminEmail = AdminEmailTextBox.Text;
            AdminPassword = AdminPasswordBox.Password;

            if (string.IsNullOrWhiteSpace(CompanyId) || string.IsNullOrWhiteSpace(CompanyName))
            {
                MessageBox.Show("Company ID and Name are required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Only validate admin fields if we're adding a new company
            if (string.IsNullOrEmpty(CompanyIdTextBox.Text) &&
                (string.IsNullOrWhiteSpace(AdminUsername) || string.IsNullOrWhiteSpace(AdminEmail) || string.IsNullOrWhiteSpace(AdminPassword)))
            {
                MessageBox.Show("All admin fields are required when creating a new company.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}