using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NewShadowGuard.Models
{
    public class CorrelationRule
    {
        [Key]
        [Column("rule_id")]
        public int RuleId { get; set; }

        [Required]
        [Column("rule_name")]
        public string RuleName { get; set; }

        [Column("description")]
        public string? Description { get; set; }

        [Column("event_type")]
        public string? EventType { get; set; }

        [Column("threshold")]
        public int Threshold { get; set; }

        [Column("time_window_minutes")]
        public int TimeWindowMinutes { get; set; }

        [Column("severity")]
        public string Severity { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("last_run_at")]
        public DateTime? LastRunAt { get; set; }
    }
}