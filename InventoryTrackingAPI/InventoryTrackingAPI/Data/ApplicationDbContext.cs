using InventoryTrackingAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace InventoryTrackingAPI.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        // These DbSets link your C# models to your SQL tables
        public DbSet<User> Users { get; set; }
        public DbSet<Location> Locations { get; set; }
        public DbSet<Asset> Assets { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
    }
}