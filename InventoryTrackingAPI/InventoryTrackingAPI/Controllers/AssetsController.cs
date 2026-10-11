using InventoryTrackingAPI.Data;
using InventoryTrackingAPI.DTOs;
using InventoryTrackingAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Text;

namespace InventoryTrackingAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // Locks down the entire controller by default
    public class AssetsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public AssetsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpPost("register")]
        [Authorize(Roles = "Admin")] // Strictly limits this action to Admins
        public async Task<IActionResult> RegisterAsset([FromBody] AssetRegistrationRequest request)
        {
            // 1. Check if this specific physical asset is already in the system
            if (await _context.Assets.AnyAsync(a => a.SerialNumber == request.SerialNumber))
                return BadRequest("An asset with this serial number already exists.");

            // 2. Prepare the raw string for hashing
            // Hash the physical Serial Number (Barcode)
            string rawData = request.SerialNumber;

            // 3. Generate the SHA-256 Baseline Hash
            string baselineHash = ComputeSha256Hash(rawData);

            // 4. Create the new Asset record
            var newAsset = new Asset
            {
                SerialNumber = request.SerialNumber,
                Category = request.Category,
                Specifications = request.Specifications,
                BaselineHash = baselineHash,
                Status = "Active"
            };

            _context.Assets.Add(newAsset);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Asset registered securely.",
                assetId = newAsset.AssetId,
                baselineHash = newAsset.BaselineHash
            });
        }

        [HttpPost("verify")]
        [Authorize(Roles = "Technician")] // Strictly limits this action to Field Technicians
        public async Task<IActionResult> VerifyAsset([FromBody] AssetVerificationRequest request)
        {
            // 1. Find the asset in the database
            var asset = await _context.Assets.FindAsync(request.AssetId);
            if (asset == null)
                return NotFound("Asset not found in the system.");

            // 2. Re-create the raw string exactly as the Admin did during registration
            // FIXED: Only look at the incoming Serial Number
            string scannedData = request.SerialNumber;

            // 3. Generate a real-time hash of the newly scanned data
            string realTimeHash = ComputeSha256Hash(scannedData);

            // 4. Compare the hashes to check for tampering
            bool isMatch = (realTimeHash == asset.BaselineHash);

            // INJECT DEBUGGING INFO HERE:
            string auditResult = isMatch ? "Verified" : "Mismatch";

            // 5. Update the Asset's status if tampering is detected
            if (!isMatch)
            {
                asset.Status = "Tampered";
            }

            // 6. Extract the Technician's User ID from their JWT Token
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            int technicianId = int.Parse(userIdClaim?.Value ?? "0");

            // Convert to local 
            TimeZoneInfo mytZone = TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time");
            DateTime malaysiaTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, mytZone);


            // 7. Create the immutable Audit Log
            var auditLog = new AuditLog
            {
                AssetId = asset.AssetId,
                ScannedBy = technicianId,
                ScanTimestamp = malaysiaTime,
                Result = auditResult // This will now print the exact payload on your dashboard!
            };

            _context.AuditLogs.Add(auditLog);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                matchStatus = auditResult,
                message = isMatch ? "Integrity verified. No tampering detected." : "ALERT: Hardware mismatch detected! Asset flagged.",
                realTimeHash = realTimeHash,
                baselineHash = asset.BaselineHash
            });
        }

        // -------------------------------------------------------------
        // HELPER METHOD: The Cryptographic Engine
        // -------------------------------------------------------------
        private string ComputeSha256Hash(string rawData)
        {
            // Initializes the built-in .NET SHA-256 algorithm
            using (SHA256 sha256Hash = SHA256.Create())
            {
                // Compute the hash as a byte array
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));

                // Convert the byte array into a readable hex string
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }
    }
}