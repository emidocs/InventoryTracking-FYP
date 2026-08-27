namespace InventoryTrackingAPI.DTOs
{
    public class AssetVerificationRequest
    {
        public int AssetId { get; set; }
        public string SerialNumber { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Specifications { get; set; } = string.Empty;
    }
}