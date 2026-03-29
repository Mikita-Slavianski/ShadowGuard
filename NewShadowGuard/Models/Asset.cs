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

        [Required(ErrorMessage = "Название обязательно")]
        public string Name { get; set; }

        [Required(ErrorMessage = "Тип обязателен")]
        public string Type { get; set; }

        [Column("ip_address")]
        public string? IpAddress { get; set; }

        public string? Os { get; set; }

        [Required(ErrorMessage = "Критичность обязательна")]
        public string Criticality { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [ForeignKey("TenantId")]
        public Tenant? Tenant { get; set; }
    }
}
