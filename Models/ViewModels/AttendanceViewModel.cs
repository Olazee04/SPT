namespace SPT.Models
{
    public class AttendanceViewModel
    {
        // Weekly Stats (Resets Monday)
        public int DaysLoggedThisWeek { get; set; }
        public int WeeklyTarget { get; set; } = 5;
        public double WeeklyCompletionRate => WeeklyTarget == 0 ? 0 : (double)DaysLoggedThisWeek / WeeklyTarget * 100;

        // Location Stats (Total)
        public int TotalOfficeDays { get; set; }
        public int TotalRemoteDays { get; set; }

        // Graph Data
        public List<string> MonthLabels { get; set; } = new List<string>();
        public List<int> MonthlyAttendanceCounts { get; set; } = new List<int>();

        // History
        public List<ProgressLog> RecentLogs { get; set; } = new List<ProgressLog>();
    }
}