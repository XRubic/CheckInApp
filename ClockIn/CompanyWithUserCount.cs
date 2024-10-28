using System;
using Postgrest.Attributes;
using Postgrest.Models;

namespace ClockIn
{
    [Table("companies_with_user_count")]
    public class CompanyWithUserCount : BaseModel
    {
        [PrimaryKey("id")]
        public string Id { get; set; }

        [Column("company_name")]
        public string Name { get; set; }

        [Column("registration_date")]
        public DateTime CreatedAt { get; set; }

        [Column("user_count")]
        public int UserCount { get; set; }
    }
}