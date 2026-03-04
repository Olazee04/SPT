namespace SPT.Models.ViewModels
{
    public class LeaderboardDashboardViewModel
    {
        public List<LeaderboardRow> CompletedModules { get; set; } = new();
        public List<LeaderboardRow> ActiveThisWeek { get; set; } = new();
        public List<LeaderboardRow> ActiveToday { get; set; } = new();
        public List<LeaderboardRow> ActiveThisMonth { get; set; } = new();
        public List<LeaderboardRow> Consistency { get; set; } = new();
        public List<LeaderboardRow> TopPerCohort { get; set; } = new();

        public List<TrackLeaderboardGroup> CompletedByTrack { get; set; } = new();
        public List<TrackLeaderboardGroup> ActiveWeekByTrack { get; set; } = new();
        public List<TrackLeaderboardGroup> ActiveMonthByTrack { get; set; } = new();
    }

    public class LeaderboardRow
    {
        public int Rank { get; set; }
        public int StudentId { get; set; }

        public string FullName { get; set; } = string.Empty;
        public string? ProfilePicture { get; set; }

        public string Track { get; set; } = string.Empty;
        public string Cohort { get; set; } = string.Empty;

        public string TrackCode { get; set; } = "";
        public int Score { get; set; }
    }

    public class TrackLeaderboardGroup
    {
        public string TrackCode { get; set; } = "";
        public List<LeaderboardRow> Rows { get; set; } = new();
    }
}
