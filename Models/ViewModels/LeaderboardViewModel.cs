using System;

namespace SPT.Models.ViewModels
{
    public class LeaderboardViewModel
    {
        public int Rank { get; set; }
        public string FullName { get; set; }
        public int StudentId { get; set; }  
        public string CohortName { get; set; }  

        // Or keep both just in case:
        public string Track { get; set; }
        public string Cohort { get; set; }
        public string TrackCode { get; set; } = string.Empty; 
        public string ProfilePicture { get; set; } = string.Empty;
        public decimal TotalHours { get; set; }
        public int LogCount { get; set; }
        public int ConsistencyScore { get; set; }
        public bool IsCurrentUser { get; set; } 
    }
}