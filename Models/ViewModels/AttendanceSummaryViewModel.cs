namespace SPT.Models.ViewModels
{
    public class AttendanceSummaryViewModel
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public string CohortName { get; set; } = string.Empty;

        public int PresentDays { get; set; }
        public int AbsentDays { get; set; }

        public int OfficeDays { get; set; }
        public int RemoteDays { get; set; }

        public int TotalLogs { get; set; } // total activity count
    }
}
