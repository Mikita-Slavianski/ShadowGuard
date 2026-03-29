using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NewShadowGuard.Models
{
    public class User
    {
        [Key]
        [Column("user_id")]
        public int UserId { get; set; }

        [Column("tenant_id")]
        public int? TenantId { get; set; }

        [Required(ErrorMessage = "Email обязателен")]
        [EmailAddress(ErrorMessage = "Неверный формат Email")]
        public string Email { get; set; }

        [Column("password_hash")]
        public string? PasswordHash { get; set; }

        [Required(ErrorMessage = "Имя обязательно")]
        [Column("full_name")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Роль обязательна")]
        public string Role { get; set; }

        [Column("mfa_enabled")]
        public bool MfaEnabled { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [ForeignKey("TenantId")]
        public Tenant? Tenant { get; set; }
    }
}