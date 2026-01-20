using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SPT.Models
{
    public class Capstone
    {
        [Key]
        public int Id { get; set; }

        public int StudentId { get; set; }
        [ForeignKey("StudentId")]
        public Student Student { get; set; }

        public string Title { get; set; }
        public string Description { get; set; }
        public string GitHubUrl { get; set; }
        public string LiveUrl { get; set; }

        public string Status { get; set; } = "Submitted"; // Submitted, Under Review, Changes Requested, Approved

        // Approvals (Simulating 2 approvals required)
        public int ApprovalCount { get; set; } = 0;
        public string MentorFeedback { get; set; }

        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    }
}