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
    }

    public class LeaderboardRow
    {
        public int Rank { get; set; }
        public int StudentId { get; set; }

        public string FullName { get; set; } = string.Empty;
        public string? ProfilePicture { get; set; }

        public string Track { get; set; } = string.Empty;
        public string Cohort { get; set; } = string.Empty;

        public int Score { get; set; }
    }
}
