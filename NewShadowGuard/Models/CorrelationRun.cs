using NewShadowGuard.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NewShadowGuard.Models
{
    public class CorrelationRun
    {
        [Key]
        [Column("run_id")]
        public int RunId { get; set; }

        [Column("run_at")]
        public DateTime RunAt { get; set; }

        [Column("rules_checked")]
        public int RulesChecked { get; set; }

        [Column("incidents_created")]
        public int IncidentsCreated { get; set; }

        [Column("run_by")]
        public int? RunBy { get; set; }

        [Column("duration_seconds")]
        public int DurationSeconds { get; set; }

        [ForeignKey("RunBy")]
        public User? RunByUser { get; set; }
    }
}