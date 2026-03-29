// Models/Tenant.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NewShadowGuard.Models
{
    public class Tenant
    {
        [Key]
        [Column("tenant_id")]
        public int TenantId { get; set; }

        [Column("name")]
        public string Name { get; set; }
        [Column("country")]
        public string Country { get; set; }
        [Column("subscription")]
        public string Subscription { get; set; }
        [Column("status")]
        public string Status { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }
}