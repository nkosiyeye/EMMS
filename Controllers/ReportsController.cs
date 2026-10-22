using Microsoft.AspNetCore.Mvc;
using EMMS.Data.Repository;
using EMMS.Service;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace EMMS.Controllers
{
    public class ReportsController : BaseController
    {
        private const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        private readonly AssetManagementRepo _assetManagementRepo;
        private readonly AssetSummaryReportService _summaryReports;
        private readonly AssetListReportService _listReports;

        public ReportsController(AssetManagementRepo assetManagementRepo, AssetSummaryReportService summaryReports, AssetListReportService listReports)
        {
            _assetManagementRepo = assetManagementRepo;
            _summaryReports = summaryReports;
            _listReports = listReports;
        }

        public async Task<IActionResult> Index()
        {
            // Only admins choose a facility; everyone else always gets their own.
            if (isAdmin)
                ViewData["FacilityId"] = new SelectList(await _assetManagementRepo.GetActiveFacilitiesAsync(), "FacilityId", "FacilityName");
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> GenerateReport(string reportType, DateTime? fromDate, DateTime? toDate, int? facilityId)
        {
            var key = reportType?.ToLower() ?? "";
            try
            {
                // Asset summary reports (the old dashboard charts): Summary sheet + one sheet per group
                if (AssetSummaryReportService.ReportTypes.TryGetValue(key, out var summaryReport))
                {
                    var (content, name) = await _summaryReports.BuildAsync(CurrentUser!, summaryReport, facilityId);
                    return File(content, ExcelContentType, name);
                }

                // Asset list reports: one sheet, filtered by date range
                if (AssetListReportService.ReportTypes.TryGetValue(key, out var listReport))
                {
                    var (content, name) = await _listReports.BuildAsync(CurrentUser!, listReport, fromDate, toDate, facilityId);
                    return File(content, ExcelContentType, name);
                }

                return BadRequest("Invalid report type");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error generating report: {ex.Message}");
            }
        }
    }
}
