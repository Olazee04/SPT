namespace SPT.Models.ViewModels
{
    public class AdminDashboardViewModel
    {
        // Top Cards
        public int ActiveStudents { get; set; }
        public int PendingLogs { get; set; }
        public int OpenTickets { get; set; }
        public decimal AvgConsistency { get; set; }
        public int TotalStudents { get; set; } 
        public int TotalMentors { get; set; }  

        // The Big Table Data
        public List<StudentPerformanceDTO> StudentPerformance { get; set; } = new List<StudentPerformanceDTO>();

        // 📊 NEW: Chart Data
        public string[] TrackLabels { get; set; }
        public int[] TrackCounts { get; set; }

        public string[] ActivityDates { get; set; }
        public int[] ActivityCounts { get; set; }
    }

    public class StudentPerformanceDTO
    {
        public int StudentId { get; set; }
        public string FullName { get; set; }
        public string Track { get; set; }
        public string ProfilePicture { get; set; }
        public decimal WeeklyHours { get; set; }
        public int WeeklyCheckIns { get; set; }
        public int CompletedModules { get; set; }
        public int TotalModules { get; set; }
        public int SyllabusProgress => TotalModules == 0 ? 0 : (int)((double)CompletedModules / TotalModules * 100);
        public double AverageMentorScore { get; set; }
        public string Status => WeeklyCheckIns >= 3 ? "Active" : "Inactive";
        public int ConsistencyScore => (int)((double)WeeklyCheckIns / 7 * 100);
    }
}