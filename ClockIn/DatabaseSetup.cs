using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Supabase;

namespace ClockIn
{
    public class DatabaseSetup
    {
        private readonly Client _supabase;

        public DatabaseSetup(Client supabase)
        {
            _supabase = supabase;
        }

        public async Task EnsureAdminUserExists()
        {
            const string adminUsername = "admin";
            const string adminPassword = "admin123"; // You might want to generate this randomly or prompt for it
            const string adminEmail = "admin@example.com";

            try
            {
                var existingUser = await _supabase.From<User>().Where(u => u.Username == adminUsername).Single();

                if (existingUser == null)
                {
                    var newAdminUser = new User
                    {
                        CompanyId = "ADMIN",
                        Username = adminUsername,
                        Email = adminEmail,
                        IsAdmin = true,
                        CreatedAt = DateTime.UtcNow
                    };
                    newAdminUser.SetPassword(adminPassword);

                    await _supabase.From<User>().Insert(newAdminUser);
                    Debug.WriteLine("Admin user created successfully.");
                }
                else
                {
                    // Update the existing admin user's password
                    existingUser.SetPassword(adminPassword);
                    await _supabase.From<User>().Update(existingUser);
                    Debug.WriteLine("Admin user password updated.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error ensuring admin user exists: {ex.Message}");
            }
        }
    }
}