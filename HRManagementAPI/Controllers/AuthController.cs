using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using HRManagementAPI.Data;
using HRManagementAPI.Models;
using BCrypt.Net;

namespace HRManagementAPI.Controllers
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

        // POST: api/Auth/Register
        [HttpPost("Register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            // Check if email already exists
            if (await _context.Users.AnyAsync(u => u.Email == request.Email))
            {
                return BadRequest(new { message = "Email already exists" });
            }

            // Check if employee code already exists
            if (await _context.Employees.AnyAsync(e => e.EmployeeCode == request.EmployeeCode))
            {
                return BadRequest(new { message = "Employee code already exists" });
            }

            // Check if role exists
            var role = await _context.Roles.FindAsync(request.RoleId);
            if (role == null)
            {
                return BadRequest(new { message = "Invalid role" });
            }

            // Check if department exists (optional)
            if (request.DepartmentId.HasValue)
            {
                var department = await _context.Departments.FindAsync(request.DepartmentId.Value);
                if (department == null)
                {
                    return BadRequest(new { message = "Invalid department" });
                }
            }

            // Hash the password using BCrypt
            string hashedPassword = BCrypt.Net.BCrypt.HashPassword(request.Password);

            // Create User
            var user = new User
            {
                Email = request.Email,
                PasswordHash = hashedPassword,
                RoleId = request.RoleId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Create Employee linked to User
            var employee = new Employee
            {
                UserId = user.UserId,
                FirstName = request.FirstName,
                LastName = request.LastName,
                EmployeeCode = request.EmployeeCode,
                DepartmentId = request.DepartmentId,
                EmployeeType = request.EmployeeType,
                JobTitle = request.JobTitle,
                HireDate = request.HireDate ?? DateTime.UtcNow,
                EmploymentStatus = "Active",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            // Set PTO eligibility based on EmployeeType
            if (request.EmployeeType == "AdminStaff")
            {
                employee.IsEligibleForPTO = true;
                employee.PTOBalance = 0;
                employee.IsEligibleForInsurance = true;
            }

            _context.Employees.Add(employee);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "User registered successfully",
                userId = user.UserId,
                employeeId = employee.EmployeeId,
                email = user.Email,
                employeeCode = employee.EmployeeCode,
                role = role.RoleName
            });
        }

        // POST: api/Auth/Login
        [HttpPost("Login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            // Find user by email with related data
            var user = await _context.Users
                .Include(u => u.Role)
                .Include(u => u.Employee)
                    .ThenInclude(e => e.Department)
                .FirstOrDefaultAsync(u => u.Email == request.Email);

            if (user == null)
            {
                return Unauthorized(new { message = "Invalid email or password" });
            }

            // Check if user is active
            if (!user.IsActive)
            {
                return Unauthorized(new { message = "Account is deactivated" });
            }

            // Verify password using BCrypt
            bool isPasswordValid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);

            if (!isPasswordValid)
            {
                return Unauthorized(new { message = "Invalid email or password" });
            }

            // Update last login
            user.LastLoginAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // Generate JWT Token
            var token = GenerateJwtToken(user);

            // Return user info with token
            return Ok(new
            {
                message = "Login successful",
                token = token,
                user = new
                {
                    userId = user.UserId,
                    email = user.Email,
                    role = user.Role.RoleName,
                    roleLevel = user.Role.RoleLevel
                },
                employee = user.Employee != null ? new
                {
                    employeeId = user.Employee.EmployeeId,
                    firstName = user.Employee.FirstName,
                    lastName = user.Employee.LastName,
                    employeeCode = user.Employee.EmployeeCode,
                    department = user.Employee.Department?.DepartmentName,
                    jobTitle = user.Employee.JobTitle,
                    employeeType = user.Employee.EmployeeType
                } : null
            });
        }

        // GET: api/Auth/Roles
        [HttpGet("Roles")]
        public async Task<IActionResult> GetRoles()
        {
            var roles = await _context.Roles
                .OrderBy(r => r.RoleLevel)
                .ToListAsync();
            return Ok(roles);
        }

        // GET: api/Auth/Departments
        [HttpGet("Departments")]
        public async Task<IActionResult> GetDepartments()
        {
            var departments = await _context.Departments
                .Where(d => d.IsActive)
                .OrderBy(d => d.DepartmentName)
                .ToListAsync();
            return Ok(departments);
        }

        // Private method to generate JWT token
        private string GenerateJwtToken(User user)
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var secretKey = jwtSettings["SecretKey"];
            var issuer = jwtSettings["Issuer"];
            var audience = jwtSettings["Audience"];
            var expiryMinutes = int.Parse(jwtSettings["ExpiryMinutes"]);

            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            // Create claims
            var claims = new List<Claim>
    {
        new Claim(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),  // ✅ CHANGE TO UserId
        new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        new Claim(ClaimTypes.Email, user.Email),
        new Claim(ClaimTypes.Role, user.Role.RoleName),
        new Claim("RoleLevel", user.Role.RoleLevel.ToString())
    };

            // Add employee claims if employee exists
            if (user.Employee != null)
            {
                claims.Add(new Claim("EmployeeId", user.Employee.EmployeeId.ToString()));
                claims.Add(new Claim(ClaimTypes.GivenName, user.Employee.FirstName));
                claims.Add(new Claim(ClaimTypes.Surname, user.Employee.LastName));
                claims.Add(new Claim("EmployeeType", user.Employee.EmployeeType));

                if (user.Employee.DepartmentId.HasValue)
                {
                    claims.Add(new Claim("DepartmentId", user.Employee.DepartmentId.Value.ToString()));
                }
            }

            // Create token
            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }

    // Request models
    public class RegisterRequest
    {
        public string Email { get; set; }
        public string Password { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public int RoleId { get; set; }
        public string EmployeeCode { get; set; }
        public int? DepartmentId { get; set; }
        public string EmployeeType { get; set; } // AdminStaff or FieldStaff
        public string? JobTitle { get; set; }
        public DateTime? HireDate { get; set; }
    }

    public class LoginRequest
    {
        public string Email { get; set; }
        public string Password { get; set; }
    }
}