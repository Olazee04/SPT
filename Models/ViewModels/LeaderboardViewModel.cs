namespace SPT.Models
{
    public class LeaderboardViewModel
    {
        public int Rank { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public string TrackCode { get; set; } = string.Empty; // e.g. "FS"
        public string ProfilePicture { get; set; } = string.Empty;
        public decimal TotalHours { get; set; }
        public int ConsistencyScore { get; set; }
        public bool IsCurrentUser { get; set; } // To highlight the row
    }
}