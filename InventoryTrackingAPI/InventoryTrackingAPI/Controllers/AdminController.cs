using InventoryTrackingAPI.Data;
using InventoryTrackingAPI.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace InventoryTrackingAPI.Controllers
{
    // 1. Lock down the entire dashboard
    [Authorize(Roles = "Admin", AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ---------------------------------------------------
        // AUTHENTICATION & LOGIN
        // ---------------------------------------------------
        [AllowAnonymous]
        [HttpGet]
        public IActionResult Login()
        {
            // If they are already logged in, route them automatically!
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                if (User.IsInRole("Admin")) return RedirectToAction("Dashboard");
                if (User.IsInRole("Technician")) return RedirectToAction("Portal", "Technician");
            }

            return View();
        }

        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> Login(string username, string password)
        {
            // 1. Fetch user by username 
            var user = _context.Users.FirstOrDefault(u => u.Username == username && u.IsActive);

            if (user != null && BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            {
                // 2. THE CORE OF RBAC: Embed their Role directly into their session cookie
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, user.Username),
                    new Claim(ClaimTypes.Role, user.Role) // <-- This tells .NET exactly who they are
                };

                var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var principal = new ClaimsPrincipal(identity);
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

                // 3. THE ROUTER: Send Admins to the Dashboard, send Technicians to their Portal
                if (user.Role == "Admin")
                {
                    return RedirectToAction("Dashboard");
                }
                else
                {
                    return RedirectToAction("Portal", "Technician");
                }
            }

            ViewBag.Error = "Invalid username or password.";
            return View();
        }

        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }


        // ---------------------------------------------------
        // DASHBOARD & INVENTORY ACTIONS
        // ---------------------------------------------------
        public IActionResult Dashboard()
        {
            var logs = _context.AuditLogs
                               .OrderByDescending(log => log.ScanTimestamp)
                               .Take(50)
                               .ToList();

            ViewBag.AssetDirectory = _context.Assets.ToDictionary(a => a.AssetId, a => a);
            return View(logs);
        }

        public IActionResult Inventory()
        {
            var assets = _context.Assets.OrderBy(a => a.AssetId).ToList();
            return View(assets);
        }

        public IActionResult RestoreAsset(int id)
        {
            var asset = _context.Assets.Find(id);
            if (asset != null)
            {
                asset.Status = "Active";
                _context.SaveChanges();
            }
            return RedirectToAction("Inventory");
        }

        // GET: Loads the edit page and fills it with the existing asset data
        [HttpGet]
        public async Task<IActionResult> EditAsset(int id)
        {
            var asset = await _context.Assets.FindAsync(id);
            if (asset == null)
            {
                return NotFound();
            }
            return View(asset);
        }

        // POST: Receives the updated data and pushes it to SQL Server
        [HttpPost]
        [ValidateAntiForgeryToken] // Security measure to prevent Cross-Site Request Forgery
        public async Task<IActionResult> EditAsset(Asset updatedAsset)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(updatedAsset);
                    await _context.SaveChangesAsync();

                    // Send them back to the master table after saving
                    return RedirectToAction(nameof(Inventory));
                }
                catch (DbUpdateException)
                {
                    ModelState.AddModelError("", "Unable to save changes. Try again.");
                }
            }

            // If validation fails, reload the page with the errors
            return View(updatedAsset);
        }

        // ---------------------------------------------------
        // ASSET REGISTRATION & QR GENERATION
        // ---------------------------------------------------
        [HttpGet]
        public IActionResult AddAsset()
        {
            return View();
        }

        [HttpPost]
        public IActionResult AddAsset(string category, string serialNumber, string specifications, int locationId)
        {
            if (string.IsNullOrEmpty(serialNumber))
            {
                return BadRequest("Error: The Serial Number (Barcode) cannot be empty.");
            }

            string rawData = serialNumber;
            string generatedHash = "";

            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawData));
                StringBuilder builder = new StringBuilder();
                foreach (byte b in bytes)
                {
                    builder.Append(b.ToString("x2"));
                }
                generatedHash = builder.ToString();
            }

            var newAsset = new InventoryTrackingAPI.Models.Asset
            {
                AssetName = $"{category} - {serialNumber}",

                Category = category,
                SerialNumber = serialNumber,
                Specifications = specifications,
                BaselineHash = generatedHash,
                Status = "Active"
            };

            _context.Assets.Add(newAsset);
            _context.SaveChanges();

            return RedirectToAction("GenerateQR", new { assetId = newAsset.AssetId.ToString() });
        }

        public IActionResult GenerateQR(string assetId)
        {
            ViewBag.QRCodeBase64 = null;
            ViewBag.AssetId = assetId;

            if (!string.IsNullOrEmpty(assetId))
            {
                string qrText = $"ASSET_ID:{assetId}";

                using (QRCodeGenerator qrGenerator = new QRCodeGenerator())
                {
                    QRCodeData qrCodeData = qrGenerator.CreateQrCode(qrText, QRCodeGenerator.ECCLevel.Q);
                    using (PngByteQRCode qrCode = new PngByteQRCode(qrCodeData))
                    {
                        byte[] qrCodeImage = qrCode.GetGraphic(20);
                        ViewBag.QRCodeBase64 = Convert.ToBase64String(qrCodeImage);
                    }
                }
            }
            return View();
        }

        // ---------------------------------------------------
        // USER PROVISIONING
        // ---------------------------------------------------
        [HttpGet]
        public IActionResult RegisterUser()
        {
            return View();
        }

        [HttpPost]
        public IActionResult RegisterUser(string username, string password, string role)
        {
            // 1. Prevent duplicate usernames
            if (_context.Users.Any(u => u.Username == username))
            {
                ViewBag.Error = "Error: That username already exists in the system.";
                return View();
            }

            // 2. Hash the password exactly how the API expects it
            string hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);

            // 3. Create the new user
            var newUser = new InventoryTrackingAPI.Models.User
            {
                Username = username,
                PasswordHash = hashedPassword,
                Role = role,
                IsActive = true // Active by default
            };

            _context.Users.Add(newUser);
            _context.SaveChanges();

            // 4. Redirect back to the dashboard upon success
            return RedirectToAction("Dashboard");
        }
        // GET: /Admin/UserDirectory
        public IActionResult UserDirectory()
        {
            var users = _context.Users.OrderBy(u => u.UserId).ToList();
            return View(users);
        }

        // GET: /Admin/ToggleUserStatus
        public IActionResult ToggleUserStatus(int id)
        {
            var userToToggle = _context.Users.Find(id);
            var currentLoggedInUser = User.Identity?.Name;

            // 1. Ensure the user exists
            // 2. SAFETY CHECK: Prevent the Admin from accidentally locking themselves out!
            if (userToToggle != null && userToToggle.Username != currentLoggedInUser)
            {
                // Flip the status (Active becomes Inactive, Inactive becomes Active)
                userToToggle.IsActive = !userToToggle.IsActive;
                _context.SaveChanges();
            }

            return RedirectToAction("UserDirectory");
        }

        // GET: Loads the user data into the edit form
        [HttpGet]
        public async Task<IActionResult> EditUser(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound();
            }
            return View(user);
        }

        [HttpGet]
        public async Task<IActionResult> ExportTamperedReport()
        {
            // Fetch all tampered items from the database
            var tamperedAssets = await _context.Assets
                .Where(a => a.Status == "Tampered")
                .ToListAsync();

            var builder = new System.Text.StringBuilder();

            // Create the CSV Column Headers
            builder.AppendLine("Asset ID,Asset Name,Category,Serial Number,Status");

            // Loop through the data and add rows
            foreach (var asset in tamperedAssets)
            {
                // Replace any commas in the data to avoid breaking the CSV format
                var safeName = asset.AssetName?.Replace(",", "") ?? "Unknown";
                builder.AppendLine($"{asset.AssetId},{safeName},{asset.Category},{asset.SerialNumber},{asset.Status}");
            }

            // Return the file to the browser
            byte[] fileBytes = System.Text.Encoding.UTF8.GetBytes(builder.ToString());
            return File(fileBytes, "text/csv", $"Tampered_Assets_Report_{DateTime.Now:yyyyMMdd}.csv");
        }

        // POST: Saves the updated user details
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUser(User updatedUser)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(updatedUser);
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(UserDirectory));
                }
                catch (DbUpdateException)
                {
                    ModelState.AddModelError("", "Unable to save changes. Try again.");
                }
            }
            return View(updatedUser);
        }
    }


}