using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Supabase;

namespace ClockIn
{
    public class AuthService
    {
        private readonly Client _supabase;

        public AuthService(Client supabase)
        {
            _supabase = supabase;
        }

        public async Task<(User user, string error)> Login(string companyId, string username, string password)
        {
            try
            {
                var users = await _supabase.From<User>()
                    .Where(u => u.CompanyId == companyId && u.Username == username)
                    .Get();

                foreach (var user in users.Models)
                {
                    if (user.VerifyPassword(password))
                    {
                        return (user, null);
                    }
                }

                return (null, "Invalid username or password");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Login error: {ex.Message}");
                return (null, "An error occurred during login");
            }
        }
    }
}