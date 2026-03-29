using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NewShadowGuard.Models
{
    public class Comment
    {
        [Key]
        [Column("comment_id")]
        public int CommentId { get; set; }

        [Column("incident_id")]
        public int IncidentId { get; set; }

        [Column("user_id")]
        public int UserId { get; set; }

        [Column("text")]
        public string Text { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [ForeignKey("IncidentId")]
        public Incident Incident { get; set; }

        [ForeignKey("UserId")]
        public User User { get; set; }
    }
}
