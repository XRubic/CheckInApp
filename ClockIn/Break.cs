using System;
using Postgrest.Attributes;
using Postgrest.Models;

namespace ClockIn
{
    [Table("breaks")]
    public class Break : BaseModel
    {
        [PrimaryKey("id", false)]
        public Guid Id { get; set; }

        [Column("entry_id")]
        public Guid EntryId { get; set; }

        [Column("start_time")]
        public DateTime StartTime { get; set; }

        [Column("end_time")]
        public DateTime? EndTime { get; set; }
    }
}