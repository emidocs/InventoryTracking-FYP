using InventoryTrackingAPI.Data;
using InventoryTrackingAPI.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace InventoryTrackingAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public AuthController(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            // 1. Find user in the database
            var user = await _context.Users.SingleOrDefaultAsync(u => u.Username == request.Username);
            if (user == null || !user.IsActive)
                return Unauthorized("Invalid credentials.");

            // 2. Verify password using BCrypt
            bool isPasswordValid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
            if (!isPasswordValid)
                return Unauthorized("Invalid credentials.");

            // 3. Generate the JWT Claims (Embed the Role)
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role)
            };

            // 4. Create the Token
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.Now.AddHours(8),
                signingCredentials: creds
            );

            return Ok(new
            {
                token = new JwtSecurityTokenHandler().WriteToken(token),
                role = user.Role
            });
        }

            [HttpPost("reset-password")]
            public async Task<IActionResult> ResetPassword(string username, string newPassword)
            {
                // Find the user in the database
                var user = await _context.Users.SingleOrDefaultAsync(u => u.Username == username);

                if (user == null)
                    return NotFound("User not found in the database.");

                // Generate a fresh, mathematically correct BCrypt hash for the new password
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);

                // Save the new hash to SQL Server
                await _context.SaveChangesAsync();

                return Ok($"Password for {username} has been successfully updated!");
            }
        }
    }
