using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using HRManagementAPI.Data;
using HRManagementAPI.Models;

namespace HRManagementAPI.Services
{
    public class BulkImportService
    {
        private readonly ApplicationDbContext _context;

        public BulkImportService(ApplicationDbContext context)
        {
            _context = context;
        }

        // =============================================
        // UPDATED: Parse Excel file with new columns
        // Now supports 18 columns (added Role and Phone Number)
        // =============================================
        public List<EmployeeExcelRow> ParseExcel(string filePath)
        {
            var rows = new List<EmployeeExcelRow>();
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            using var package = new ExcelPackage(new System.IO.FileInfo(filePath));
            var sheet = package.Workbook.Worksheets[0];
            int rowCount = sheet.Dimension.Rows;

            for (int row = 2; row <= rowCount; row++)
            {
                // Skip empty rows - check if at least name or email exists
                var firstName = sheet.Cells[row, 2].Text;
                var lastName = sheet.Cells[row, 1].Text;
                var email = sheet.Cells[row, 6].Text;

                if (string.IsNullOrWhiteSpace(firstName) &&
                    string.IsNullOrWhiteSpace(lastName) &&
                    string.IsNullOrWhiteSpace(email))
                {
                    continue; // Skip this empty row
                }

                rows.Add(new EmployeeExcelRow
                {
                    RowNumber = row,
                    LastName = lastName,
                    FirstName = firstName,
                    DateOfHire = sheet.Cells[row, 3].GetValue<DateTime?>(),
                    DateOfBirth = sheet.Cells[row, 4].GetValue<DateTime?>(),
                    SSN = sheet.Cells[row, 5].Text,
                    Email = email,
                    Address = sheet.Cells[row, 7].Text,
                    Status = sheet.Cells[row, 8].Text,
                    Hours = sheet.Cells[row, 9].Text,
                    Department = sheet.Cells[row, 10].Text,
                    Title = sheet.Cells[row, 11].Text,
                    Supervisor = sheet.Cells[row, 12].Text,
                    Benefits = sheet.Cells[row, 13].Text,
                    PTOTracker = sheet.Cells[row, 14].Text,
                    DLExpiration = sheet.Cells[row, 15].GetValue<DateTime?>(),
                    NursingLicenseExpiration = sheet.Cells[row, 16].GetValue<DateTime?>(),
                    // ✨ NEW COLUMNS
                    Role = sheet.Cells[row, 17].Text,  // Column 17: Role
                    PhoneNumber = sheet.Cells[row, 18].Text  // Column 18: Phone Number
                });
            }

            return rows;
        }

        // =============================================
        // Validate Department Code (unchanged)
        // =============================================
        private async Task<(bool isValid, int? departmentId, string? departmentName, string errorMessage)>
            ValidateDepartmentCode(string? departmentCode)
        {
            if (string.IsNullOrWhiteSpace(departmentCode))
            {
                return (false, null, null, "Department code is required");
            }

            var department = await _context.Departments
                .FirstOrDefaultAsync(d => d.DepartmentCode == departmentCode.Trim().ToUpper());

            if (department == null)
            {
                return (false, null, null, $"Invalid department code: '{departmentCode}'. " +
                    "Valid codes: NUR, EVENT, HR, IT, FIN, PROG");
            }

            return (true, department.DepartmentId, department.DepartmentName, string.Empty);
        }

        // =============================================
        // CORRECTED: Map Role string to RoleId
        // Admin = 2, Field = 6 (per user specification)
        // =============================================
        private (bool isValid, int roleId, string errorMessage) MapRoleToRoleId(string? roleString)
        {
            // Default to FieldOperator (6) if not specified
            if (string.IsNullOrWhiteSpace(roleString))
            {
                return (true, 6, string.Empty);
            }

            // Map role string to RoleId (CORRECTED MAPPING)
            int roleId = roleString.Trim().ToLower() switch
            {
                "admin" => 2,           // Admin = RoleId 2 ✅
                "executive" => 2,       // Executive = RoleId 2
                "director" => 3,        // Director = RoleId 3
                "coordinator" => 4,     // Coordinator = RoleId 4
                "manager" => 5,         // Manager = RoleId 5
                "field" => 6,           // Field = RoleId 6
                _ => -1                 // Invalid
            };

            if (roleId == -1)
            {
                return (false, 0, $"Invalid role: '{roleString}'. " +
                    "Valid values: Admin, Executive, Director, Coordinator, Manager, Field");
            }

            return (true, roleId, string.Empty);
        }

        // =============================================
        // UPDATED: Import employees with Role and Phone Number
        // =============================================
        public async Task<ImportResult> ImportEmployees(List<EmployeeExcelRow> rows, int userId, string fileName)
        {
            var log = new BulkImportLog
            {
                ImportDate = DateTime.UtcNow,
                ImportedBy = userId,
                FileName = fileName,
                TotalRecords = rows.Count,
                ImportStatus = "InProgress"
            };
            _context.BulkImportLog.Add(log);
            await _context.SaveChangesAsync();

            var result = new ImportResult { ImportLogId = log.ImportLogId, TotalRecords = rows.Count };

            foreach (var row in rows)
            {
                try
                {
                    // Basic validation
                    if (string.IsNullOrWhiteSpace(row.Email) ||
                        string.IsNullOrWhiteSpace(row.FirstName) ||
                        string.IsNullOrWhiteSpace(row.LastName))
                    {
                        result.FailedRecords++;
                        SaveDetail(log.ImportLogId, row, "Failed", "Missing required fields", null, null);
                        continue;
                    }

                    // Validate department code
                    var (isDeptValid, departmentId, departmentName, deptError) =
                        await ValidateDepartmentCode(row.Department);

                    if (!isDeptValid)
                    {
                        result.FailedRecords++;
                        SaveDetail(log.ImportLogId, row, "Failed", deptError, null, null);
                        continue;
                    }

                    // ✨ Map role to RoleId
                    var (isRoleValid, roleId, roleError) = MapRoleToRoleId(row.Role);

                    if (!isRoleValid)
                    {
                        result.FailedRecords++;
                        SaveDetail(log.ImportLogId, row, "Failed", roleError, null, null);
                        continue;
                    }

                    // Check duplicate email
                    if (await _context.Users.AnyAsync(u => u.Email == row.Email))
                    {
                        result.FailedRecords++;
                        SaveDetail(log.ImportLogId, row, "Failed", "Email already exists", null, null);
                        continue;
                    }

                    // Create User
                    var user = new User
                    {
                        Email = row.Email,
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword("TPA2025!"),
                        RoleId = roleId,  // ✨ Use mapped RoleId (Admin = 2)
                        IsActive = true,
                        OnboardingStatus = "Completed",
                        AccountStatus = "Active",
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.Users.Add(user);
                    await _context.SaveChangesAsync();

                    // Create Employee
                    var employee = new Employee
                    {
                        UserId = user.UserId,
                        FirstName = row.FirstName,
                        LastName = row.LastName,
                        DateOfBirth = row.DateOfBirth,
                        SSN = row.SSN,
                        SSNLast4 = row.SSN?.Length >= 4 ? row.SSN.Substring(row.SSN.Length - 4) : null,
                        PersonalEmail = row.Email,
                        PhoneNumber = row.PhoneNumber,  // ✨ NEW: Set phone number
                        Address = row.Address,
                        HireDate = row.DateOfHire ?? DateTime.UtcNow,
                        EmploymentType = MapEmploymentType(row.Status),
                        EmploymentStatus = row.Status?.ToUpper() == "INACTIVE" ? "Inactive" : "Active",
                        EmployeeType = row.Department?.Contains("Admin") == true ? "AdminStaff" : "FieldStaff",
                        WorkHoursCategory = row.Hours,
                        JobTitle = row.Title,
                        DepartmentId = departmentId.Value,
                        DriversLicenseExpiration = row.DLExpiration,
                        NursingLicenseExpiration = row.NursingLicenseExpiration,
                        IsEligibleForPTO = row.PTOTracker?.ToUpper() == "YES",
                        PTOBalance = 0,
                        EmployeeCode = $"TPA-BULK-{user.UserId:D6}",
                        IsActive = row.Status?.ToUpper() != "INACTIVE",
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    // Set benefits
                    if (row.Benefits?.ToLower().Contains("health") == true) employee.IsEligibleForInsurance = true;
                    if (row.Benefits?.ToLower().Contains("dental") == true) employee.IsEligibleForDental = true;
                    if (row.Benefits?.ToLower().Contains("vision") == true) employee.IsEligibleForVision = true;
                    if (row.Benefits?.ToLower().Contains("life") == true) employee.IsEligibleForLife = true;
                    if (row.Benefits?.ToLower().Contains("403b") == true) employee.IsEligibleFor403B = true;

                    _context.Employees.Add(employee);
                    await _context.SaveChangesAsync();

                    result.SuccessfulRecords++;
                    SaveDetail(log.ImportLogId, row, "Success", null, employee.EmployeeId, user.UserId);
                }
                catch (Exception ex)
                {
                    result.FailedRecords++;
                    SaveDetail(log.ImportLogId, row, "Failed", ex.Message, null, null);
                }
            }

            // Update log
            log.SuccessfulRecords = result.SuccessfulRecords;
            log.FailedRecords = result.FailedRecords;
            log.ImportStatus = result.FailedRecords == 0 ? "Completed" : "Partial";
            await _context.SaveChangesAsync();

            return result;
        }

        // Save import detail (unchanged)
        private void SaveDetail(int logId, EmployeeExcelRow row, string status, string? error, int? empId, int? userId)
        {
            _context.BulkImportDetails.Add(new BulkImportDetail
            {
                ImportLogId = logId,
                RowNumber = row.RowNumber,
                FirstName = row.FirstName,
                LastName = row.LastName,
                Email = row.Email,
                ImportStatus = status,
                ErrorMessage = error,
                EmployeeId = empId,
                UserId = userId
            });
            _context.SaveChanges();
        }

        // Map employment type (unchanged)
        private string MapEmploymentType(string? status)
        {
            return status?.ToUpper() switch
            {
                "FT" => "Full-Time",
                "PT" => "Part-Time",
                "PRN" => "PRN",
                _ => "Full-Time"
            };
        }

        // Get import history (unchanged)
        public async Task<List<BulkImportLog>> GetHistory()
        {
            return await _context.BulkImportLog
                .OrderByDescending(l => l.ImportDate)
                .ToListAsync();
        }
    }

    // =============================================
    // UPDATED: EmployeeExcelRow with new properties
    // =============================================
    public class EmployeeExcelRow
    {
        public int RowNumber { get; set; }
        public string? LastName { get; set; }
        public string? FirstName { get; set; }
        public DateTime? DateOfHire { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? SSN { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }
        public string? Status { get; set; }
        public string? Hours { get; set; }
        public string? Department { get; set; }
        public string? Title { get; set; }
        public string? Supervisor { get; set; }
        public string? Benefits { get; set; }
        public string? PTOTracker { get; set; }
        public DateTime? DLExpiration { get; set; }
        public DateTime? NursingLicenseExpiration { get; set; }

        // ✨ NEW PROPERTIES
        public string? Role { get; set; }  // Column 17
        public string? PhoneNumber { get; set; }  // Column 18
    }

    public class ImportResult
    {
        public int ImportLogId { get; set; }
        public int TotalRecords { get; set; }
        public int SuccessfulRecords { get; set; }
        public int FailedRecords { get; set; }
    }
}