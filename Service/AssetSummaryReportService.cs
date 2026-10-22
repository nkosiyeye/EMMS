using System.Globalization;
using EMMS.Utility;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using EMMS.Data;
using EMMS.Models.Admin;
using Microsoft.EntityFrameworkCore;
using static EMMS.Models.Enumerators;

namespace EMMS.Service
{
    /// <summary>The asset summary reports that replaced the old dashboard charts, one per chart.</summary>
    public enum AssetSummaryReport
    {
        FunctionalStatus,
        ProcurementStatus,
        MovementReason,
        NonFunctionalByFacility,
        AssetsByServicePoint,
    }

    /// <summary>One asset with its current location, flattened for the report sheets.</summary>
    public class AssetReportRow
    {
        public string Tag { get; set; } = "";
        public string Name { get; set; } = "";
        public string? SerialNumber { get; set; }
        public string? Category { get; set; }
        public string? Department { get; set; }
        public bool HasLocation { get; set; }
        public string? Facility { get; set; }
        public string? ServicePoint { get; set; }
        public FunctionalStatus FunctionalStatus { get; set; }
        public ProcurementStatus ProcurementStatus { get; set; }
        public MovementReason? Reason { get; set; }    // reason of the movement that put it where it is now
        public DateTime? NextServiceDate { get; set; }
        public DateTime? DateCreated { get; set; }
    }

    public class ReportGroup
    {
        public string Name { get; set; } = "";
        public List<AssetReportRow> Rows { get; set; } = new();
    }

    /// <summary>
    /// Builds the asset summary reports as Excel workbooks: a Summary sheet (count per group) followed by one sheet
    /// per group listing its assets. Groups follow the old dashboard charts exactly. Admins may pick a facility (or
    /// all); everyone else is pinned to their own facility.
    /// </summary>
    public class AssetSummaryReportService
    {
        private readonly ApplicationDbContext _context;

        public AssetSummaryReportService(ApplicationDbContext context)
        {
            _context = context;
        }

        // ------------------------------------------------------------------------------------------
        // Rules (pure, unit-tested)
        // ------------------------------------------------------------------------------------------
        public static readonly IReadOnlyDictionary<string, AssetSummaryReport> ReportTypes = new Dictionary<string, AssetSummaryReport>
        {
            ["functional-status"] = AssetSummaryReport.FunctionalStatus,
            ["procurement-status"] = AssetSummaryReport.ProcurementStatus,
            ["movement-reason"] = AssetSummaryReport.MovementReason,
            ["non-functional-by-facility"] = AssetSummaryReport.NonFunctionalByFacility,
            ["assets-by-service-point"] = AssetSummaryReport.AssetsByServicePoint,
        };

        public static string Title(AssetSummaryReport report) => report switch
        {
            AssetSummaryReport.FunctionalStatus => "Functional Status",
            AssetSummaryReport.ProcurementStatus => "Procurement Status",
            AssetSummaryReport.MovementReason => "Movement Reason",
            AssetSummaryReport.NonFunctionalByFacility => "Non-Functional Assets by Facility",
            _ => "Assets by Service Point",
        };

        public static string ReasonLabel(MovementReason reason) => reason switch
        {
            MovementReason.RoutineTransfer => "Routine transfer",
            MovementReason.StolenorMissing => "Stolen or missing",
            _ => reason.ToString(),
        };

        /// <summary>
        /// Splits the rows into the same groups the dashboard chart showed. Status-like groups keep their natural
        /// order; location-like groups are largest first.
        /// </summary>
        public static List<ReportGroup> Group(AssetSummaryReport report, IEnumerable<AssetReportRow> rows)
        {
            var located = rows.Where(r => r.HasLocation);
            return report switch
            {
                AssetSummaryReport.FunctionalStatus => located
                    .GroupBy(r => r.FunctionalStatus)
                    .OrderBy(g => g.Key == FunctionalStatus.Unknown ? int.MaxValue : (int)g.Key)
                    .Select(g => Make(DashboardService.FunctionalLabel(g.Key), g))
                    .ToList(),

                AssetSummaryReport.ProcurementStatus => rows
                    .GroupBy(r => r.ProcurementStatus)
                    .OrderBy(g => g.Key)
                    .Select(g => Make(g.Key.ToString(), g))
                    .ToList(),

                AssetSummaryReport.MovementReason => located
                    .Where(r => r.Reason != null)
                    .GroupBy(r => r.Reason!.Value)
                    .OrderByDescending(g => g.Count()).ThenBy(g => g.Key)
                    .Select(g => Make(ReasonLabel(g.Key), g))
                    .ToList(),

                // Same rule as the old chart: non-functional, or moved out as decommissioned.
                AssetSummaryReport.NonFunctionalByFacility => located
                    .Where(r => r.FunctionalStatus == FunctionalStatus.NonFunctional || r.Reason == MovementReason.Decommission)
                    .GroupBy(r => r.Facility ?? "Unknown facility")
                    .OrderByDescending(g => g.Count()).ThenBy(g => g.Key)
                    .Select(g => Make(g.Key, g))
                    .ToList(),

                _ => located
                    .Where(r => !string.IsNullOrWhiteSpace(r.ServicePoint))
                    .GroupBy(r => r.ServicePoint!)
                    .OrderByDescending(g => g.Count()).ThenBy(g => g.Key)
                    .Select(g => Make(g.Key, g))
                    .ToList(),
            };

            static ReportGroup Make(string name, IEnumerable<AssetReportRow> rows) =>
                new() { Name = name, Rows = rows.OrderBy(r => r.Facility).ThenBy(r => r.Tag).ToList() };
        }

        /// <summary>Excel sheet names: max 31 chars, none of : \ / ? * [ ], not blank, unique (case-insensitive).</summary>
        public static string SheetName(string name, ISet<string> used)
        {
            var clean = Regex.Replace(name ?? "", @"[:\\/?*\[\]]", " ").Trim().Trim('\'');
            if (clean.Length == 0) clean = "Sheet";
            if (clean.Length > 31) clean = clean[..31].TrimEnd();

            var candidate = clean;
            for (var n = 2; used.Contains(candidate); n++)
            {
                var suffix = $" ({n})";
                candidate = (clean.Length + suffix.Length > 31 ? clean[..(31 - suffix.Length)].TrimEnd() : clean) + suffix;
            }
            used.Add(candidate);
            return candidate;
        }

        public static string FileName(AssetSummaryReport report, string? facilityName, DateTime now)
        {
            var scope = string.IsNullOrWhiteSpace(facilityName) ? "All_Facilities" : Regex.Replace(facilityName.Trim(), @"[^A-Za-z0-9]+", "_").Trim('_');
            return $"{Title(report).Replace(" ", "_")}_{scope}_{now:yyyyMMdd_HHmmss}.xlsx";
        }

        // ------------------------------------------------------------------------------------------
        // Build
        // ------------------------------------------------------------------------------------------
        public async Task<(byte[] Content, string FileName)> BuildAsync(User user, AssetSummaryReport report, int? requestedFacilityId)
        {
            var facilityId = DashboardService.ScopeFacility(user, requestedFacilityId);
            var facilityName = facilityId == null ? null
                : await _context.Facilities.Where(f => f.FacilityId == facilityId).Select(f => f.FacilityName).FirstOrDefaultAsync();

            var rows = await LoadRowsAsync(facilityId);
            var groups = Group(report, rows);
            var now = DateTime.Now;
            return (Write(report, facilityName, groups, now), FileName(report, facilityName, now));
        }

        // Active assets with their current (received) location; scoped to a facility = currently located there.
        // vw_AssetCurrentLocation is keyless, so "has a location" is tested on a column (MovementId), not the row.
        private async Task<List<AssetReportRow>> LoadRowsAsync(int? facilityId)
        {
            var query =
                from a in _context.Assets.AsNoTracking()
                where a.RowState == RowStatus.Active
                join l in _context.AssetCurrentLocations on a.AssetId equals l.AssetId into locations
                from l in locations.DefaultIfEmpty()
                let locationFacilityId = (int?)l.FacilityId
                let servicePointId = (int?)l.ServicePointId
                where facilityId == null || locationFacilityId == facilityId
                select new AssetReportRow
                {
                    Tag = a.AssetTagNumber,
                    Name = a.ItemName != null && a.ItemName != "" ? a.ItemName : a.SubCategory!.Name ?? "",
                    SerialNumber = a.SerialNumber,
                    Category = a.Category!.Name,
                    Department = a.Department!.Name,
                    HasLocation = (Guid?)l.MovementId != null,
                    Facility = _context.Facilities.Where(f => f.FacilityId == locationFacilityId).Select(f => f.FacilityName).FirstOrDefault(),
                    ServicePoint = _context.LookupItems.Where(i => i.Id == servicePointId).Select(i => i.Name).FirstOrDefault(),
                    FunctionalStatus = a.FunctionalStatus,
                    ProcurementStatus = (ProcurementStatus)a.StatusId,
                    Reason = (MovementReason?)l.Reason,
                    NextServiceDate = a.NextServiceDate,
                    DateCreated = a.DateCreated,
                };
            return await query.ToListAsync();
        }

        private static readonly string[] DetailHeaders =
        {
            "Asset Code", "Asset Name", "S/N", "Category", "Department", "Facility", "Service Point",
            "Functional Status", "Procurement Status", "Last Movement Reason", "Next Service Date", "Date Registered"
        };

        private static byte[] Write(AssetSummaryReport report, string? facilityName, List<ReportGroup> groups, DateTime now)
        {
            using var workbook = new XLWorkbook();
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var total = groups.Sum(g => g.Rows.Count);

            // Summary: what the chart showed, as numbers
            var summary = workbook.Worksheets.Add(SheetName("Summary", used));
            summary.Cell(1, 1).Value = Title(report);
            summary.Cell(1, 1).Style.Font.Bold = true;
            summary.Cell(1, 1).Style.Font.FontSize = 14;
            summary.Cell(2, 1).Value = "Facility";
            summary.Cell(2, 2).Value = facilityName ?? "All facilities";
            summary.Cell(3, 1).Value = "Generated";
            summary.Cell(3, 2).Value = now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
            summary.Range(2, 1, 3, 1).Style.Font.Bold = true;

            var header = 5;
            string[] summaryHeaders = { report switch
            {
                AssetSummaryReport.NonFunctionalByFacility => "Facility",
                AssetSummaryReport.AssetsByServicePoint => "Service Point",
                AssetSummaryReport.MovementReason => "Movement Reason",
                _ => "Status",
            }, "Assets", "% of Total" };
            WriteHeader(summary, header, summaryHeaders);

            var row = header + 1;
            if (groups.Count == 0)
            {
                summary.Cell(row, 1).Value = "No assets match this report.";
                summary.Cell(row, 1).Style.Font.Italic = true;
            }
            foreach (var group in groups)
            {
                summary.Cell(row, 1).Value = group.Name;
                summary.Cell(row, 2).Value = group.Rows.Count;
                summary.Cell(row, 3).Value = total == 0 ? 0 : (double)group.Rows.Count / total;
                summary.Cell(row, 3).Style.NumberFormat.Format = "0%";
                row++;
            }
            if (groups.Count > 0)
            {
                summary.Cell(row, 1).Value = "Total";
                summary.Cell(row, 2).Value = total;
                summary.Range(row, 1, row, 3).Style.Font.Bold = true;
                summary.Range(row, 1, row, 3).Style.Border.TopBorder = XLBorderStyleValues.Thin;
            }
            summary.Columns().AdjustToContents();

            // One sheet per group
            foreach (var group in groups)
            {
                var sheet = workbook.Worksheets.Add(SheetName(group.Name, used));
                WriteHeader(sheet, 1, DetailHeaders);
                var r = 2;
                foreach (var a in group.Rows)
                {
                    sheet.Cell(r, 1).Value = a.Tag;
                    sheet.Cell(r, 2).Value = a.Name;
                    sheet.Cell(r, 3).Value = a.SerialNumber ?? "";
                    sheet.Cell(r, 4).Value = a.Category ?? "";
                    sheet.Cell(r, 5).Value = a.Department ?? "";
                    sheet.Cell(r, 6).Value = a.Facility ?? "Not deployed";
                    sheet.Cell(r, 7).Value = a.ServicePoint ?? "";
                    sheet.Cell(r, 8).Value = DashboardService.FunctionalLabel(a.FunctionalStatus);
                    sheet.Cell(r, 9).Value = a.ProcurementStatus.ToString();
                    sheet.Cell(r, 10).Value = a.Reason != null ? ReasonLabel(a.Reason.Value) : "";
                    sheet.Cell(r, 11).Value = a.NextServiceDate?.ToString("yyyy-MM-dd") ?? "";
                    sheet.Cell(r, 12).Value = a.DateCreated?.ToString("yyyy-MM-dd") ?? "";
                    r++;
                }
                sheet.SheetView.FreezeRows(1);
                sheet.Range(1, 1, Math.Max(1, r - 1), DetailHeaders.Length).SetAutoFilter();
                sheet.Columns().AdjustToContents();
            }
            ExcelLetterhead.Apply(workbook);

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        private static void WriteHeader(IXLWorksheet sheet, int row, string[] headers)
        {
            for (var i = 0; i < headers.Length; i++)
            {
                var cell = sheet.Cell(row, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.LightGray;
            }
        }
    }
}
