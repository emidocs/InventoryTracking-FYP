using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;

namespace InventoryTrackingAPI.Controllers
{
    // RBAC: Only Technicians can access this area
    [Authorize(Roles = "Technician", AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)]
    public class TechnicianController : Controller
    {
        public IActionResult Portal()
        {
            return View();
        }

        // Shared logout mechanism
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login", "Admin"); // Send back to the main login page
        }
    }
}