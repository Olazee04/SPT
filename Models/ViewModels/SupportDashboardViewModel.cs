namespace SPT.Models
{
    public class SupportDashboardViewModel
    {
        public List<SupportTicket> Tickets { get; set; } = new();
        public List<StudentReflection> Reflections { get; set; } = new();
    }
}