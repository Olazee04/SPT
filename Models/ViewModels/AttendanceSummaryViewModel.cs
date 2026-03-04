namespace SPT.Models.ViewModels
{
    public class AttendanceSummaryViewModel
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public string CohortName { get; set; } = string.Empty;
        public string TrackCode { get; set; } = "";
        public int PresentDays { get; set; }
        public int AbsentDays { get; set; }

        public int OfficeDays { get; set; }
        public int RemoteDays { get; set; }

        public int TotalLogs { get; set; }
        public decimal TotalHours { get; set; }

        public double AttendanceRate =>
            (PresentDays + AbsentDays) > 0
                ? Math.Round((double)PresentDays / (PresentDays + AbsentDays) * 100, 1)
                : 0;
    }
}
