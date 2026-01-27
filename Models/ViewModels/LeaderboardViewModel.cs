namespace SPT.Models.ViewModels
{
    public class LeaderboardViewModel
    {
        public int StudentId { get; set; }

        public string FullName { get; set; } = string.Empty;

        public string? ProfilePicture { get; set; }

        public string TrackCode { get; set; } = "N/A";

        public string CohortName { get; set; } = "N/A";

        // Generic score (NOT hours)
        public int Score { get; set; }

        public int Rank { get; set; }
    }
}