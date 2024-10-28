using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Supabase;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;

namespace ClockIn
{
    public partial class DevWindow : Window
    {
        private readonly Client _supabase;
        public ObservableCollection<CompanyWithUserCount> Companies { get; set; }

        public DevWindow(Client supabase)
        {
            InitializeComponent();
            _supabase = supabase;
            Companies = new ObservableCollection<CompanyWithUserCount>();
            CompaniesDataGrid.ItemsSource = Companies;
            LoadCompanies();
        }

        private async Task LoadCompanies()
        {
            try
            {
                var companiesResponse = await _supabase.From<CompanyWithUserCount>().Get();

                Companies.Clear();
                foreach (var company in companiesResponse.Models)
                {
                    Companies.Add(company);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading companies: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void AddCompany_Click(object sender, RoutedEventArgs e)
        {
            var addCompanyDialog = new AddCompanyDialog();
            if (addCompanyDialog.ShowDialog() == true)
            {
                if (string.IsNullOrWhiteSpace(addCompanyDialog.CompanyId))
                {
                    MessageBox.Show("Company ID cannot be empty.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                Debug.WriteLine($"Company ID: '{addCompanyDialog.CompanyId}'");
                Debug.WriteLine($"Company Name: '{addCompanyDialog.CompanyName}'");

                try
                {
                    var newCompany = new Company
                    {
                        Id = addCompanyDialog.CompanyId.Trim(),
                        Name = addCompanyDialog.CompanyName.Trim(),
                        CreatedAt = DateTime.UtcNow
                    };

                    var companyResponse = await _supabase.From<Company>().Insert(newCompany);

                    if (companyResponse.Models.Count > 0)
                    {
                        // Create admin user for the new company
                        var adminUser = new User
                        {
                            CompanyId = newCompany.Id,
                            Username = addCompanyDialog.AdminUsername,
                            Email = addCompanyDialog.AdminEmail,
                            IsAdmin = true,
                            CreatedAt = DateTime.UtcNow
                        };
                        adminUser.SetPassword(addCompanyDialog.AdminPassword);

                        var userResponse = await _supabase.From<User>().Insert(adminUser);
                        if (userResponse.Models.Count > 0)
                        {
                            MessageBox.Show($"Company '{newCompany.Name}' and admin user '{adminUser.Username}' created successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                            await LoadCompanies();
                        }
                        else
                        {
                            MessageBox.Show("Failed to create admin user.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    else
                    {
                        MessageBox.Show("Failed to create company.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Postgrest.Exceptions.PostgrestException pgEx)
                {
                    Debug.WriteLine($"Postgrest Exception: {pgEx.Message}");
                    MessageBox.Show($"Database error: {pgEx.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"General Exception: {ex.Message}");
                    MessageBox.Show($"Error creating company: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void EditCompany_Click(object sender, RoutedEventArgs e)
        {
            var company = ((Button)sender).DataContext as CompanyWithUserCount;
            if (company != null)
            {
                var editCompanyDialog = new AddCompanyDialog(company.Id, company.Name);
                if (editCompanyDialog.ShowDialog() == true)
                {
                    try
                    {
                        var updatedCompany = new Company
                        {
                            Id = company.Id, // ID should not change
                            Name = editCompanyDialog.CompanyName.Trim(),
                            CreatedAt = company.CreatedAt // CreatedAt should not change
                        };

                        var companyResponse = await _supabase
                            .From<Company>()
                            .Where(c => c.Id == company.Id)
                            .Set(c => c.Name, updatedCompany.Name)
                            .Update();

                        if (companyResponse.Models.Count > 0)
                        {
                            MessageBox.Show($"Company '{updatedCompany.Name}' updated successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                            await LoadCompanies();
                        }
                        else
                        {
                            MessageBox.Show("Failed to update company.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error updating company: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private async void DeleteCompany_Click(object sender, RoutedEventArgs e)
        {
            var company = ((Button)sender).DataContext as CompanyWithUserCount;
            if (company != null)
            {
                var result = MessageBox.Show($"Are you sure you want to delete {company.Name}? This will also delete all associated users and data.", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        // Delete all users associated with the company
                        await _supabase.From<User>().Where(u => u.CompanyId == company.Id).Delete();

                        // Delete the company
                        await _supabase.From<Company>().Where(c => c.Id == company.Id).Delete();

                        await LoadCompanies();
                        MessageBox.Show($"Company {company.Name} and all associated users have been deleted.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error deleting company: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }
    }
}