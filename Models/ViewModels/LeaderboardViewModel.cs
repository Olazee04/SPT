namespace SPT.Models
{
    public class LeaderboardViewModel
    {
        public int Rank { get; set; }
        public string FullName { get; set; }
        public int StudentId { get; set; }
        public string CohortName { get; set; }
        public string TrackCode { get; set; } = string.Empty; // e.g. "FS"
        public string ProfilePicture { get; set; } = string.Empty;
        public decimal TotalHours { get; set; }
        public int ConsistencyScore { get; set; }
        public bool IsCurrentUser { get; set; } // To highlight the row
    }
}