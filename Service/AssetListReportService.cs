using ClosedXML.Excel;
using EMMS.Data;
using EMMS.Models.Admin;
using EMMS.Utility;
using Microsoft.EntityFrameworkCore;
using static EMMS.Models.Enumerators;

namespace EMMS.Service
{
    /// <summary>The flat asset list reports on the Reports page.</summary>
    public enum AssetListReport
    {
        AllAssets,
        DueService,
    }

    /// <summary>One asset with its current location, flattened for the list report sheet.</summary>
    public class AssetListRow
    {
        public string Tag { get; set; } = "";
        public string? Name { get; set; }
        public string? SerialNumber { get; set; }
        public string? Category { get; set; }
        public string? Department { get; set; }
        public decimal? Cost { get; set; }
        public ProcurementStatus ProcurementStatus { get; set; }
        public string? Facility { get; set; }
        public string? ServicePoint { get; set; }
        public FunctionalStatus FunctionalStatus { get; set; }
        public MovementReason? Reason { get; set; }        // reason of the movement that put it where it is now
        public string? CreatedBy { get; set; }
        public DateTime? DeploymentDate { get; set; }      // date of that movement
        public DateTime? DateCreated { get; set; }
        public DateTime? NextServiceDate { get; set; }
    }

    /// <summary>
    /// Builds the asset list reports (all assets, assets due service) as a one-sheet Excel workbook. Scoping, date
    /// filtering and ordering all run in SQL. Admins may pick a facility (or all); everyone else is pinned to their
    /// own facility and sees only assets currently located there.
    /// </summary>
    public class AssetListReportService
    {
        private readonly ApplicationDbContext _context;

        public AssetListReportService(ApplicationDbContext context)
        {
            _context = context;
        }

        // ------------------------------------------------------------------------------------------
        // Rules (pure, unit-tested)
        // ------------------------------------------------------------------------------------------
        public static readonly IReadOnlyDictionary<string, AssetListReport> ReportTypes = new Dictionary<string, AssetListReport>
        {
            ["all-assets"] = AssetListReport.AllAssets,
            ["due-service"] = AssetListReport.DueService,
        };

        public static string SheetTitle(AssetListReport report) =>
            report == AssetListReport.AllAssets ? "All Assets" : "Assets Due Service";

        // The date range (when given) is part of the name, e.g. All_Assets_Report_20260101_20260131_20260201_093000.xlsx
        public static string FileName(AssetListReport report, DateTime? from, DateTime? to, DateTime now)
        {
            var name = report == AssetListReport.AllAssets ? "All_Assets_Report" : "Assets_Due_Service_Report";
            if (from.HasValue || to.HasValue)
                name += $"_{from:yyyyMMdd}_{to:yyyyMMdd}";
            return $"{name}_{now:yyyyMMdd_HHmmss}.xlsx";
        }

        // ------------------------------------------------------------------------------------------
        // Build
        // ------------------------------------------------------------------------------------------
        public async Task<(byte[] Content, string FileName)> BuildAsync(User user, AssetListReport report, DateTime? from, DateTime? to, int? requestedFacilityId)
        {
            var rows = await LoadRowsAsync(report, from, to, DashboardService.ScopeFacility(user, requestedFacilityId));
            return (Write(rows, SheetTitle(report)), FileName(report, from, to, DateTime.Now));
        }

        // All assets filter on the registration date, due service on the next service date; "to" is inclusive.
        private async Task<List<AssetListRow>> LoadRowsAsync(AssetListReport report, DateTime? from, DateTime? to, int? facilityId)
        {
            var assets = _context.Assets.AsNoTracking().Where(a => a.RowState == RowStatus.Active);
            var toExclusive = to?.AddDays(1);

            if (report == AssetListReport.AllAssets)
            {
                if (from.HasValue) assets = assets.Where(a => a.DateCreated >= from);
                if (toExclusive.HasValue) assets = assets.Where(a => a.DateCreated < toExclusive);
            }
            else
            {
                assets = assets.Where(a => a.NextServiceDate != null);
                if (from.HasValue) assets = assets.Where(a => a.NextServiceDate >= from);
                if (toExclusive.HasValue) assets = assets.Where(a => a.NextServiceDate < toExclusive);
            }

            // vw_AssetCurrentLocation is keyless, so its columns are read through nullable casts
            var query =
                from a in assets
                join l in _context.AssetCurrentLocations on a.AssetId equals l.AssetId into locations
                from l in locations.DefaultIfEmpty()
                let locationFacilityId = (int?)l.FacilityId
                let servicePointId = (int?)l.ServicePointId
                let movementId = (Guid?)l.MovementId
                where facilityId == null || locationFacilityId == facilityId
                select new AssetListRow
                {
                    Tag = a.AssetTagNumber,
                    Name = a.SubCategory!.Name,
                    SerialNumber = a.SerialNumber,
                    Category = a.Category!.Name,
                    Department = a.Department!.Name,
                    Cost = a.Cost,
                    ProcurementStatus = (ProcurementStatus)a.StatusId,
                    Facility = _context.Facilities.Where(f => f.FacilityId == locationFacilityId).Select(f => f.FacilityName).FirstOrDefault(),
                    ServicePoint = _context.LookupItems.Where(i => i.Id == servicePointId).Select(i => i.Name).FirstOrDefault(),
                    FunctionalStatus = a.FunctionalStatus,
                    Reason = (MovementReason?)l.Reason,
                    CreatedBy = a.User!.FirstName,
                    DeploymentDate = _context.AssetMovement.Where(m => m.MovementId == movementId).Select(m => (DateTime?)m.MovementDate).FirstOrDefault(),
                    DateCreated = a.DateCreated,
                    NextServiceDate = a.NextServiceDate,
                };

            query = report == AssetListReport.AllAssets
                ? query.OrderByDescending(r => r.DateCreated)
                : query.OrderBy(r => r.NextServiceDate);

            return await query.ToListAsync();
        }

        private static readonly string[] Headers =
        {
            "Asset Code", "Asset Name", "S/N", "Category", "Department", "Cost", "Status", "Facility", "Service Point",
            "Functional Status", "Movement Reason", "Created By", "Deployment Date", "Date Created", "Next Service Date"
        };

        private static byte[] Write(List<AssetListRow> rows, string sheetName)
        {
            using var workbook = new XLWorkbook();
            var sheet = workbook.Worksheets.Add(sheetName);

            for (var i = 0; i < Headers.Length; i++)
            {
                var cell = sheet.Cell(1, i + 1);
                cell.Value = Headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.LightGray;
            }

            var r = 2;
            foreach (var a in rows)
            {
                sheet.Cell(r, 1).Value = a.Tag;
                sheet.Cell(r, 2).Value = a.Name ?? "";
                sheet.Cell(r, 3).Value = a.SerialNumber ?? "";
                sheet.Cell(r, 4).Value = a.Category ?? "";
                sheet.Cell(r, 5).Value = a.Department ?? "";
                sheet.Cell(r, 6).Value = a.Cost?.ToString() ?? "";
                sheet.Cell(r, 7).Value = a.ProcurementStatus.ToString();
                sheet.Cell(r, 8).Value = a.Facility ?? "";
                sheet.Cell(r, 9).Value = a.ServicePoint ?? "";
                sheet.Cell(r, 10).Value = a.FunctionalStatus.ToString();
                sheet.Cell(r, 11).Value = a.Reason?.ToString() ?? "";
                sheet.Cell(r, 12).Value = a.CreatedBy ?? "";
                sheet.Cell(r, 13).Value = a.DeploymentDate?.ToString("yyyy-MM-dd HH:mm:ss") ?? "";
                sheet.Cell(r, 14).Value = a.DateCreated?.ToString("yyyy-MM-dd HH:mm:ss") ?? "";
                sheet.Cell(r, 15).Value = a.NextServiceDate?.ToString("yyyy-MM-dd") ?? "";
                r++;
            }

            sheet.Columns().AdjustToContents();
            ExcelLetterhead.Apply(workbook);

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }
    }
}
