namespace SPT.Models.ViewModels
{
    public class AtRiskStudentDTO
    {
        public int StudentId { get; set; }
        public string FullName { get; set; }
        public string? ProfilePicture { get; set; }
        public DateTime? LastLogDate { get; set; }

        public string DaysInactive => LastLogDate.HasValue
            ? $"{(int)(DateTime.UtcNow.Date - LastLogDate.Value.Date).TotalDays} days ago"
            : "Never logged";
    }
}