using System;
using Postgrest.Attributes;
using Postgrest.Models;

namespace ClockIn
{
    [Table("time_entries")]
    public class TimeEntry : BaseModel
    {
        [PrimaryKey("id", false)]
        public Guid Id { get; set; }

        [Column("user_id")]
        public Guid UserId { get; set; }

        [Column("clock_in_time")]
        public DateTime ClockInTime { get; set; }

        [Column("clock_out_time")]
        public DateTime? ClockOutTime { get; set; }

        [Column("total_break_duration")]
        public TimeSpan TotalBreakDuration { get; set; }

        [Column("date")]
        public DateTime Date { get; set; }

        // Add this navigation property
        [Reference(typeof(User))]
        public User User { get; set; }
    }
}