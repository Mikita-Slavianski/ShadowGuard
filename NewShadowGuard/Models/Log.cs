using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NewShadowGuard.Models
{
    public class Log
    {
        [Key]
        [Column("log_id")]
        public int LogId { get; set; }

        [Column("asset_id")]
        public int? AssetId { get; set; }

        [Column("timestamp")]
        public DateTime Timestamp { get; set; }

        [Column("event_type")]
        public string EventType { get; set; }

        [Column("raw_data")]
        public string RawData { get; set; }

        [Column("normalized_data")]
        public string NormalizedData { get; set; }

        [ForeignKey("AssetId")]
        public Asset Asset { get; set; }
    }
}
