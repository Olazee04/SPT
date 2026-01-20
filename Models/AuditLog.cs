using System;
using System.ComponentModel.DataAnnotations;

namespace SPT.Models
{
    public class AuditLog
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string TableName { get; set; } = string.Empty;

        [Required]
        public int RecordId { get; set; }

        [Required]
        public string FieldName { get; set; } = string.Empty;

        public string? OldValue { get; set; }

        public string? NewValue { get; set; }

        [Required]
        public string EditedBy { get; set; } = string.Empty;

        [Required]
        public string EditedByUserId { get; set; } = string.Empty;

        public string Action { get; set; }      
        public string Details { get; set; }     
        public string PerformedBy { get; set; } 
        public string IpAddress { get; set; }  

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}