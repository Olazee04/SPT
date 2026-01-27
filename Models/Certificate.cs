using SPT.Models;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SPT.Models
{
    public class Certificate
    {
        [Key]
        public int Id { get; set; }

        public int StudentId { get; set; }
        [ForeignKey("StudentId")]
        public Student Student { get; set; }

        public string CertificateId { get; set; } = Guid.NewGuid().ToString().Substring(0, 8).ToUpper(); // Unique Code

        public string TrackName { get; set; } // e.g., "Fullstack Development"

        public DateTime DateIssued { get; set; } = DateTime.UtcNow;

        public string IssuedBy { get; set; } = "Admin";

        public string CertificateCode { get; set; } = string.Empty;
        public DateTime IssuedAt { get; set; }
    }
}

    
