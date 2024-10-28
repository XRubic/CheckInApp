using System;
using Postgrest.Attributes;
using Postgrest.Models;
using BC = BCrypt.Net.BCrypt;

namespace ClockIn
{
    [Table("users")]
    public class User : BaseModel
    {
        [PrimaryKey("id", false)]
        public Guid Id { get; set; }

        [Column("company_id")]
        public string CompanyId { get; set; }

        [Column("username")]
        public string Username { get; set; }

        [Column("email")]
        public string Email { get; set; }

        [Column("password")]
        public string Password { get; set; }

        [Column("is_admin")]
        public bool IsAdmin { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        public void SetPassword(string password)
        {
            Password = BC.HashPassword(password);
        }

        public bool VerifyPassword(string password)
        {
            return BC.Verify(password, Password);
        }
    }
}