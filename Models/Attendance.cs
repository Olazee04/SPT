using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SPT.Models
{
    public class Attendance
    {
        [Key]
        public int Id { get; set; }

        public int StudentId { get; set; }
        [ForeignKey("StudentId")]
        public Student Student { get; set; }

        [Required]
        public DateTime Date { get; set; }

        // Status: Present, Absent, Excused, Late
        [Required]
        public string Status { get; set; }

        public string Remarks { get; set; } // e.g., "Sick leave", "Internet issues"
    }
}