using System.ComponentModel.DataAnnotations;

namespace InventoryTrackingAPI.Models
{
    public class Location
    {
        [Key]
        public int LocationId { get; set; }
        public string LabName { get; set; } = string.Empty;
        public string BuildingBlock { get; set; } = string.Empty;
    }
}