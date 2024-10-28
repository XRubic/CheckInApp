using System;

namespace ClockIn
{
    public class UserStatus
    {
        public Guid UserId { get; set; }
        public string Username { get; set; }
        public string Status { get; set; } // "Checked In", "On Break", "Offline"
        public DateTime? CheckInTime { get; set; }
        public DateTime? CheckOutTime { get; set; }
        public int BreakCount { get; set; }
        public TimeSpan TotalBreakDuration { get; set; }
        public TimeSpan TotalWorkedHours { get; set; }
        public TimeSpan OvertimeHours { get; set; }
    }
}