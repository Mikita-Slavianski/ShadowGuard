using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NewShadowGuard.Models
{
    public class IncidentTag
    {
        [Key]
        [Column("incident_tag_id")]
        public int IncidentTagId { get; set; }

        [Column("incident_id")]
        public int IncidentId { get; set; }

        [Required]
        [Column("tag_name")]
        public string TagName { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("created_by")]
        public int? CreatedBy { get; set; }

        [ForeignKey("IncidentId")]
        public Incident? Incident { get; set; }

        [ForeignKey("CreatedBy")]
        public User? CreatedByUser { get; set; }
    }
}