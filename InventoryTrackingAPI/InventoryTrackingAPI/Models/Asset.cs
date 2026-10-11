using System.ComponentModel.DataAnnotations;

namespace InventoryTrackingAPI.Models
{
    public class Asset
    {
        [Key]
        public int AssetId { get; set; }

        public string AssetName { get; set; }
        public string SerialNumber { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string? Specifications { get; set; }
        public string BaselineHash { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }
}