using System;
using System.ComponentModel.DataAnnotations;

namespace InventoryTrackingAPI.Models
{
    public class AuditLog
    {
        [Key]
        public int LogId { get; set; }
        public int AssetId { get; set; }
        public int ScannedBy { get; set; }
        public DateTime ScanTimestamp { get; set; }
        public string Result { get; set; } = string.Empty;
    }
}