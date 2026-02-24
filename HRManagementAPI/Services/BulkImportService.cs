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

        // 
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
                    NursingLicenseExpiration = sheet.Cells[row, 16].GetValue<DateTime?>()
                });
            }

            return rows;
        }

        // Import employees
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
                        RoleId = 6, // Field Operator
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
                        Address = row.Address,
                        HireDate = row.DateOfHire ?? DateTime.UtcNow,
                        EmploymentType = MapEmploymentType(row.Status),
                        EmploymentStatus = row.Status?.ToUpper() == "INACTIVE" ? "Inactive" : "Active",
                        EmployeeType = row.Department?.Contains("Admin") == true ? "AdminStaff" : "FieldStaff",
                        WorkHoursCategory = row.Hours,
                        JobTitle = row.Title,
                        DepartmentId=6,
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

        // Save import detail
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

        // Map employment type
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

        // Get import history
        public async Task<List<BulkImportLog>> GetHistory()
        {
            return await _context.BulkImportLog
                .OrderByDescending(l => l.ImportDate)
                .ToListAsync();
        }
    }

    // =============================================
    // SIMPLE DTOs
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
    }

    public class ImportResult
    {
        public int ImportLogId { get; set; }
        public int TotalRecords { get; set; }
        public int SuccessfulRecords { get; set; }
        public int FailedRecords { get; set; }
    }
}