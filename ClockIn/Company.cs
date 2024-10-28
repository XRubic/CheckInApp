using System;
using Postgrest.Attributes;
using Postgrest.Models;

namespace ClockIn
{
    [Table("companies")]
    public class Company : BaseModel
    {
        [Column("id")]
        public string Id { get; set; } = string.Empty;

        [Column("company_name")]
        public string Name { get; set; } = string.Empty;

        [Column("registration_date")]
        public DateTime CreatedAt { get; set; }
    }
}