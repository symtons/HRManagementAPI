using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HRManagementAPI.Services;
using System.Security.Claims;

namespace HRManagementAPI.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class BulkImportController : ControllerBase
    {
        private readonly BulkImportService _service;
        private readonly IWebHostEnvironment _env;

        public BulkImportController(BulkImportService service, IWebHostEnvironment env)
        {
            _service = service;
            _env = env;
        }

        // Upload Excel file
        [HttpPost("Upload")]
        public async Task<IActionResult> Upload(IFormFile file)
        {
            if (file == null) return BadRequest("No file uploaded");

            // Save file
            var folder = Path.Combine(_env.ContentRootPath, "Uploads");
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, file.FileName);

            using (var stream = new FileStream(path, FileMode.Create))
                await file.CopyToAsync(stream);

            // Parse Excel
            var rows = _service.ParseExcel(path);

            return Ok(new
            {
                totalRecords = rows.Count,
                fileName = file.FileName,
                rows
            });
        }

        // Import employees
        [HttpPost("Import")]
        public async Task<IActionResult> Import([FromBody] ImportRequest request)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            var result = await _service.ImportEmployees(request.Rows, userId, request.FileName);
            return Ok(result);
        }

        // Get history
        [HttpGet("History")]
        public async Task<IActionResult> GetHistory()
        {
            var history = await _service.GetHistory();
            return Ok(history);
        }
    }

    public class ImportRequest
    {
        public string FileName { get; set; } = "";
        public List<EmployeeExcelRow> Rows { get; set; } = new();
    }
}