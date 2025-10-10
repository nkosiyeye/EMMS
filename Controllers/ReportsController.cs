using EMMS.Models.Admin;
using System.Drawing;
using Microsoft.AspNetCore.Mvc;
using EMMS.Data;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using EMMS.Data.Repository;
using EMMS.Models;
using EMMS.ViewModels;
using static EMMS.Models.Enumerators;

namespace EMMS.Controllers
{
    public class ReportsController : BaseController
    {
        private readonly ApplicationDbContext _context;
        private readonly AssetManagementRepo _assetManagementRepo;

        public ReportsController(ApplicationDbContext context, AssetManagementRepo assetManagementRepo)
        {
            _context = context;
            _assetManagementRepo = assetManagementRepo;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> GenerateReport(string reportType, DateTime? fromDate, DateTime? toDate)
        {
            try
            {
                List<AssetViewModel> assetVMs;
                string fileName;
                string sheetName;

                var repo = _assetManagementRepo;
                var assets = isAdmin ? await repo.GetAssetsWithMovementDb() 
                    : repo.GetAssetsWithMovementDb().Result.Where(a => a.LastMovement?.FacilityId == CurrentUser.FacilityId);

                switch (reportType.ToLower())
                {
                    case "all-assets":
                        assetVMs = (assets)
                            .OrderByDescending(a => a.Asset.DateCreated)
                            .ToList();
                        fileName = "All_Assets_Report";
                        sheetName = "All Assets";
                        break;

                    case "due-service":
                        assetVMs = (assets)
                            .Where(a => a.Asset.NextServiceDate.HasValue)
                            .OrderBy(a => a.Asset.NextServiceDate)
                            .ToList();
                        fileName = "Assets_Due_Service_Report";
                        sheetName = "Assets Due Service";
                        break;

                    default:
                        return BadRequest("Invalid report type");
                }

                // Apply filters
                if (fromDate.HasValue || toDate.HasValue)
                {
                    if (reportType.ToLower() == "all-assets")
                    {
                        assetVMs = assetVMs.Where(a =>
                        {
                            if (fromDate.HasValue && a.Asset.DateCreated < fromDate.Value) return false;
                            if (toDate.HasValue && a.Asset.DateCreated > toDate.Value.AddDays(1).AddTicks(-1)) return false;
                            return true;
                        }).ToList();

                        fileName += $"_{fromDate?.ToString("yyyyMMdd")}_{toDate?.ToString("yyyyMMdd")}";
                    }
                    else if (reportType.ToLower() == "due-service")
                    {
                        assetVMs = assetVMs.Where(a =>
                        {
                            if (!a.Asset.NextServiceDate.HasValue) return false;
                            if (fromDate.HasValue && a.Asset.NextServiceDate.Value < fromDate.Value) return false;
                            if (toDate.HasValue && a.Asset.NextServiceDate.Value > toDate.Value.AddDays(1).AddTicks(-1)) return false;
                            return true;
                        }).ToList();

                        fileName += $"_{fromDate?.ToString("yyyyMMdd")}_{toDate?.ToString("yyyyMMdd")}";
                    }
                }

                var excelFile = GenerateExcelFile(assetVMs, sheetName);

                return File(excelFile,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"{fileName}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error generating report: {ex.Message}");
            }
        }


        private byte[] GenerateExcelFile(List<AssetViewModel> assetVMs, string sheetName)
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add(sheetName);

            // Headers
            var headers = new[]
            {
            "Asset Code", "Asset Name", "Category","Cost","Status", "Facility",
            "Functional Status","Movement Reason", "Created By", "Date Created",
            "Last Service Date", "Next Service Date"
        };

            for (int i = 0; i < headers.Length; i++)
            {
                worksheet.Cell(1, i + 1).Value = headers[i];
                worksheet.Cell(1, i + 1).Style.Font.Bold = true;
                worksheet.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
            }

            // Data
            for (int i = 0; i < assetVMs.Count; i++)
            {
                var vm = assetVMs[i];
                var asset = vm.Asset;
                var movement = vm.LastMovement;
                var row = i + 2;

                worksheet.Cell(row, 1).Value = asset.AssetTagNumber ?? "";
                worksheet.Cell(row, 2).Value = asset.ItemName ?? "";
                worksheet.Cell(row, 3).Value = asset.Category?.Name ?? "";
                worksheet.Cell(row, 4).Value = asset.Cost.ToString() ?? "";
                worksheet.Cell(row, 5).Value = ((ProcurementStatus)asset.StatusId).ToString() ?? "";
                worksheet.Cell(row, 6).Value = movement?.Facility?.FacilityName ?? "";
                worksheet.Cell(row, 7).Value = movement?.FunctionalStatus.ToString() ?? ""; // adjust if you have Lookup for Status
                worksheet.Cell(row, 8).Value = movement?.Reason.ToString() ?? ""; // adjust if you have Lookup for Status
                worksheet.Cell(row, 9).Value = asset.User?.FirstName ?? "";
                worksheet.Cell(row, 10).Value = asset.DateCreated?.ToString("yyyy-MM-dd HH:mm:ss") ?? "";
                worksheet.Cell(row, 11).Value = ""; // last service date - add if available
                worksheet.Cell(row, 12).Value = asset.NextServiceDate?.ToString("yyyy-MM-dd") ?? "";
            }

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }


    }
}
