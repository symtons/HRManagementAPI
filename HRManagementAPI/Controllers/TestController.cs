using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace HRManagementAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TestController : ControllerBase
    {
        // Public endpoint - anyone can access
        [HttpGet("Public")]
        public IActionResult Public()
        {
            return Ok(new { message = "This is a public endpoint - no token needed!" });
        }

        // Protected endpoint - requires valid JWT token
        [Authorize]
        [HttpGet("Protected")]
        public IActionResult Protected()
        {
            // Get user info from the token
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            var firstName = User.FindFirst(ClaimTypes.GivenName)?.Value;
            var lastName = User.FindFirst(ClaimTypes.Surname)?.Value;

            return Ok(new
            {
                message = "This is a protected endpoint - token is valid!",
                userId = userId,
                email = email,
                firstName = firstName,
                lastName = lastName,
                role = role
            });
        }

        // Admin only endpoint
        [Authorize(Roles = "Admin")]
        [HttpGet("AdminOnly")]
        public IActionResult AdminOnly()
        {
            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            var role = User.FindFirst(ClaimTypes.Role)?.Value;

            return Ok(new
            {
                message = "This endpoint is only for Admins!",
                email = email,
                role = role
            });
        }

        // HR Manager or Admin endpoint
        [Authorize(Roles = "Admin,HRManager")]
        [HttpGet("HROnly")]
        public IActionResult HROnly()
        {
            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            var role = User.FindFirst(ClaimTypes.Role)?.Value;

            return Ok(new
            {
                message = "This endpoint is for HR Managers and Admins only!",
                email = email,
                role = role
            });
        }
    }
}