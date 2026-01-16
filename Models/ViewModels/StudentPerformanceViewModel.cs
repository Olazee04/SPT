using System;

namespace SPT.Models.ViewModels
{
    public class StudentPerformanceViewModel
    {
        public int StudentId { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string ProfilePicture { get; set; }
        public string CohortName { get; set; }
        public string TrackCode { get; set; }
        public string MentorName { get; set; }

        // 📊 Stats for the Table
        public int TargetHoursPerWeek { get; set; }
        public decimal HoursLast7Days { get; set; }
        public int CheckInsLast7Days { get; set; }
        public int CompletedModules { get; set; }
        public int TotalModules { get; set; }
        public int ConsistencyScore { get; set; }
        public string Status { get; set; } // Active, At Risk, Inactive
    }
}