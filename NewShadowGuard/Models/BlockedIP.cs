using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NewShadowGuard.Models
{
    public class BlockedIP
    {
        [Key]
        [Column("blocked_ip_id")]
        public int BlockedIpId { get; set; }

        [Required]
        [Column("ip_address")]
        public string IpAddress { get; set; }

        public string? Reason { get; set; }

        [Column("blocked_by")]
        public int? BlockedBy { get; set; }

        [Column("blocked_at")]
        public DateTime BlockedAt { get; set; }

        [Column("expires_at")]
        public DateTime? ExpiresAt { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; }

        [Column("incident_id")]
        public int? IncidentId { get; set; }

        [ForeignKey("BlockedBy")]
        public User? BlockedByUser { get; set; }

        [ForeignKey("IncidentId")]
        public Incident? Incident { get; set; }
    }
}