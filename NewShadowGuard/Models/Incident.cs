using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NewShadowGuard.Models
{
    public class Incident
    {
        [Key]
        [Column("incident_id")]
        public int IncidentId { get; set; }

        [Column("tenant_id")]
        public int? TenantId { get; set; }

        [Column("log_id")]
        public int? LogId { get; set; }

        [Column("title")]
        public string Title { get; set; }
        [Column("description")]
        public string Description { get; set; }
        [Column("severity")]
        public string Severity { get; set; }
        [Column("status")]
        public string Status { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }

        [ForeignKey("TenantId")]
        public Tenant Tenant { get; set; }

        [ForeignKey("LogId")]
        public Log Log { get; set; }

        public ICollection<Comment> Comments { get; set; }
    }
}
