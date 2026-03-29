using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NewShadowGuard.Models
{
    public class Asset
    {
        [Key]
        [Column("asset_id")]
        public int AssetId { get; set; }

        [Column("tenant_id")]
        public int? TenantId { get; set; }

        [Column("name")]
        public string Name { get; set; }
        [Column("type")]
        public string Type { get; set; }

        [Column("ip_address")]
        public string IpAddress { get; set; }

        [Column("os")]
        public string Os { get; set; }
        [Column("criticality")]
        public string Criticality { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        // Навигационное свойство
        [ForeignKey("TenantId")]
        public Tenant Tenant { get; set; }
    }
}
