using InventoryTrackingAPI.Data;
using InventoryTrackingAPI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventoryTrackingAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LocationsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        // Injecting the database context
        public LocationsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/Locations
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Location>>> GetLocations()
        {
            // Retrieves all locations from the SQL database
            var locations = await _context.Locations.ToListAsync();

            if (locations == null || !locations.Any())
            {
                return NotFound("No locations found in the database.");
            }

            return Ok(locations);
        }
    }
}