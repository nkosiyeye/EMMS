using ClosedXML.Excel;
using EMMS.Utility;
using EMMS.Constants;
using EMMS.Data;
using EMMS.Models.Admin;
using EMMS.ViewModels;
using Microsoft.EntityFrameworkCore;
using static EMMS.Models.Enumerators;

namespace EMMS.Service
{
    /// <summary>
    /// Cylinder stock by facility and size/content (from the location views), cylinders with patients for too long,
    /// and requested vs ordered vs dispatched vs received.
    /// </summary>
    public class CylinderReportService
    {
        public const int PaletteSlots = 8;  // table: combinations past the 8th fold into "Other"
        public const int ChartSlots = 3;    // chart: only the first three palette slots stay distinguishable in every pairing
        public const string OtherKey = "other";

        private readonly ApplicationDbContext _context;
        private readonly CylinderOrderService _orderService;

        public CylinderReportService(ApplicationDbContext context, CylinderOrderService orderService)
        {
            _context = context;
            _orderService = orderService;
        }

        public static string ComboKey(int sizeId, int contentId) => $"{sizeId}-{contentId}";

        // ------------------------------------------------------------------------------------------
        // Pure aggregation (unit-tested)
        // ------------------------------------------------------------------------------------------

        /// <param name="comboOrder">Every Size/Content combination in its fixed order (sizes by SortIndex, then contents).</param>
        public static StockSummary BuildStock(IEnumerable<CylinderStockRow> rows, IReadOnlyList<StockColumn> comboOrder, int overdueDays, DateTime today)
        {
            var summary = new StockSummary();
            var slotByKey = comboOrder.ToDictionary(c => c.Key, c => c.Slot);
            var facilities = new Dictionary<int, StockFacilityRow>();
            var usedKeys = new HashSet<string>();

            StockFacilityRow RowFor(int id, string? name, bool central)
            {
                if (!facilities.TryGetValue(id, out var row))
                    facilities[id] = row = new StockFacilityRow { FacilityId = id, Facility = name ?? "?", IsCentralStore = central };
                return row;
            }

            foreach (var c in rows)
            {
                summary.Kpis.Total++;
                if (c.Lost || c.FacilityId == null)
                {
                    summary.Kpis.NotAvailable++;
                    continue;
                }
                if (c.InTransitToFacilityId != null)
                {
                    // Location stays the source until received; show it as incoming at the destination only
                    summary.Kpis.InTransit++;
                    RowFor(c.InTransitToFacilityId.Value, c.InTransitTo, false).Incoming++;
                    continue;
                }

                var key = ComboKey(c.SizeId, c.ContentId);
                if (!slotByKey.TryGetValue(key, out var slot) || slot >= PaletteSlots) key = OtherKey;
                usedKeys.Add(key);

                var facilityRow = RowFor(c.FacilityId.Value, c.FacilityName, c.AtCentralStore);
                facilityRow.IsCentralStore |= c.AtCentralStore;
                facilityRow.Counts[key] = facilityRow.Counts.GetValueOrDefault(key) + 1;

                if (c.AtCentralStore) summary.Kpis.AtCentralStore++;
                else if (c.WithPatient) summary.Kpis.WithPatients++;
                else summary.Kpis.AtFacilities++;

                if (c.WithPatient && c.Since != null && (today.Date - c.Since.Value.Date).Days >= overdueDays)
                {
                    summary.Overdue.Add(new OverdueRow
                    {
                        Tag = c.Tag, Serial = c.Serial, Item = $"{c.Size} {c.Content}".Trim(),
                        Facility = c.FacilityName, ServicePoint = c.ServicePoint, PatientNumber = c.PatientNumber,
                        Since = c.Since.Value, Days = (today.Date - c.Since.Value.Date).Days
                    });
                }
            }

            summary.Kpis.Overdue = summary.Overdue.Count;
            summary.Overdue = summary.Overdue.OrderByDescending(o => o.Days).ToList();
            summary.Columns = comboOrder.Where(c => c.Slot < PaletteSlots && usedKeys.Contains(c.Key)).ToList();
            if (usedKeys.Contains(OtherKey)) summary.Columns.Add(new StockColumn { Key = OtherKey, Label = "Other", Slot = PaletteSlots });
            summary.Rows = facilities.Values
                .OrderByDescending(r => r.IsCentralStore)
                .ThenByDescending(r => r.Total)
                .ThenBy(r => r.Facility)
                .ToList();
            return summary;
        }

        /// <summary>
        /// The chart's colored series: the combinations with the most located cylinders across the whole network
        /// (ties in lookup order), so every viewer sees the same color for the same combination.
        /// </summary>
        public static List<StockColumn> PickChartSeries(IEnumerable<CylinderStockRow> networkRows, IReadOnlyList<StockColumn> comboOrder, int max = ChartSlots)
        {
            var counts = networkRows
                .Where(r => !r.Lost && r.FacilityId != null && r.InTransitToFacilityId == null)
                .GroupBy(r => ComboKey(r.SizeId, r.ContentId))
                .ToDictionary(g => g.Key, g => g.Count());

            return comboOrder
                .Select((c, index) => (c, index, count: counts.GetValueOrDefault(c.Key)))
                .Where(x => x.count > 0)
                .OrderByDescending(x => x.count).ThenBy(x => x.index)
                .Take(max)
                .Select((x, slot) => new StockColumn { Key = x.c.Key, Label = x.c.Label, Slot = slot })
                .ToList();
        }

        /// <summary>A facility sees what is at it and what is on its way to it.</summary>
        public static List<CylinderStockRow> ScopeToFacility(IEnumerable<CylinderStockRow> rows, int facilityId) =>
            rows.Where(r => (r.FacilityId == facilityId && r.InTransitToFacilityId == null) || r.InTransitToFacilityId == facilityId).ToList();

        public static List<RequestSummaryRow> BuildRequestSummary(IEnumerable<RequestFact> facts)
        {
            return facts.GroupBy(f => f.Facility)
                .Select(g => new RequestSummaryRow
                {
                    Facility = g.Key,
                    Requests = g.Count(),
                    PendingApproval = g.Count(f => f.Status == CylinderRequestStatus.Requested),
                    Rejected = g.Count(f => f.Status == CylinderRequestStatus.Rejected),
                    Requested = g.Where(f => f.Status == CylinderRequestStatus.Approved).Sum(f => f.Quantity),
                    Ordered = g.Where(f => f.Ordered).Sum(f => f.OrderQuantity),
                    Dispatched = g.Sum(f => f.Dispatched),
                    Received = g.Sum(f => f.Received),
                })
                .OrderBy(r => r.Facility)
                .ToList();
        }

        // ------------------------------------------------------------------------------------------
        // Queries
        // ------------------------------------------------------------------------------------------
        public async Task<List<StockColumn>> GetComboOrderAsync()
        {
            var sizes = await _orderService.GetSizesAsync();
            var contents = await _orderService.GetContentsAsync();
            int slot = 0;
            return sizes.SelectMany(s => contents.Select(c => new StockColumn { Key = ComboKey(s.Id, c.Id), Label = $"{s.Name} {c.Name}", Slot = slot++ }))
                .ToList();
        }

        public async Task<List<CylinderStockRow>> GetStockRowsAsync()
        {
            var raw = await (
                from a in _context.Assets
                where a.RowState == RowStatus.Active
                join d in _context.AssetCylinderDetails on a.AssetId equals d.AssetId
                join l in _context.AssetCurrentLocations on a.AssetId equals l.AssetId into locs
                from l in locs.DefaultIfEmpty()
                join t in _context.AssetsInTransit on a.AssetId equals t.AssetId into transit
                from t in transit.DefaultIfEmpty()
                select new
                {
                    a.AssetId, a.AssetTagNumber, a.SerialNumber, Item = a.SubCategory!.Name,
                    d.SizeId, Size = d.Size!.Name, d.ContentId, Content = d.Content!.Name,
                    FacilityId = (int?)l.FacilityId, ServicePointId = (int?)l.ServicePointId, PatientId = (Guid?)l.PatientId,
                    Reason = (MovementReason?)l.Reason, l.DateReceived,
                    TransitTo = (int?)t.FacilityId
                })
                .AsNoTracking()
                .ToListAsync();

            var facilities = await _context.Facilities.AsNoTracking()
                .Select(f => new { f.FacilityId, f.FacilityName, f.IsCentralStore })
                .ToDictionaryAsync(f => f.FacilityId);
            var servicePoints = (await _context.LookupItems.AsNoTracking()
                    .Where(l => l.LookupList!.Name == LookupListNames.ServicePoint)
                    .ToListAsync())
                .ToDictionary(l => l.Id, l => (l.Name, RequiresPatient: l.GetFlag(LookupFlags.RequiresPatient)));
            var patientIds = raw.Where(r => r.PatientId != null).Select(r => r.PatientId!.Value).Distinct().ToList();
            var patients = await _context.HomeBasedCarePatients.AsNoTracking()
                .Where(p => patientIds.Contains(p.PatientId))
                .ToDictionaryAsync(p => p.PatientId, p => p.Number);

            return raw.Select(r =>
            {
                var facility = r.FacilityId != null ? facilities.GetValueOrDefault(r.FacilityId.Value) : null;
                var sp = r.ServicePointId != null && servicePoints.TryGetValue(r.ServicePointId.Value, out var spv) ? spv : default;
                return new CylinderStockRow
                {
                    AssetId = r.AssetId, Tag = r.AssetTagNumber, Item = r.Item,
                    Serial = r.SerialNumber != r.AssetTagNumber ? r.SerialNumber : null,
                    SizeId = r.SizeId, Size = r.Size, ContentId = r.ContentId, Content = r.Content,
                    FacilityId = r.FacilityId, FacilityName = facility?.FacilityName, AtCentralStore = facility?.IsCentralStore == true,
                    ServicePoint = sp.Name, WithPatient = sp.RequiresPatient,
                    PatientNumber = r.PatientId != null ? patients.GetValueOrDefault(r.PatientId.Value) : null,
                    Since = r.DateReceived,
                    InTransitToFacilityId = r.TransitTo,
                    InTransitTo = r.TransitTo != null ? facilities.GetValueOrDefault(r.TransitTo.Value)?.FacilityName : null,
                    Lost = r.Reason is MovementReason.StolenorMissing or MovementReason.Decommission
                };
            }).ToList();
        }

        public async Task<List<RequestFact>> GetRequestFactsAsync(DateTime from, DateTime to, int? facilityScope)
        {
            var end = to.Date.AddDays(1);
            var requests = await _context.CylinderRequests.AsNoTracking()
                .Where(r => r.RowState == RowStatus.Active && r.DateCreated >= from.Date && r.DateCreated < end)
                .Where(r => facilityScope == null || r.FacilityId == facilityScope)
                .Select(r => new { r.RequestId, Facility = r.Facility!.FacilityName, r.Status, r.Quantity })
                .ToListAsync();
            var requestIds = requests.Select(r => r.RequestId).ToList();
            var orders = await _context.CylinderOrders.AsNoTracking()
                .Where(o => requestIds.Contains(o.RequestId) && o.Status != CylinderOrderStatus.PlantDown && o.RowState == RowStatus.Active)
                .Select(o => new
                {
                    o.RequestId, o.Quantity,
                    Dispatched = o.Items.Count(i => i.Movement != null && i.Movement.DateRejected == null),
                    Received = o.Items.Count(i => i.Movement != null && i.Movement.DateRejected == null && i.Movement.DateReceived != null)
                })
                .ToListAsync();
            var byRequest = orders.GroupBy(o => o.RequestId).ToDictionary(g => g.Key, g => g.ToList());

            return requests.Select(r =>
            {
                var o = byRequest.GetValueOrDefault(r.RequestId) ?? new();
                return new RequestFact
                {
                    Facility = r.Facility ?? "?", Status = r.Status, Quantity = r.Quantity,
                    Ordered = o.Any(), OrderQuantity = o.Sum(x => x.Quantity),
                    Dispatched = o.Sum(x => x.Dispatched), Received = o.Sum(x => x.Received)
                };
            }).ToList();
        }

        public async Task<CylinderDashboardViewModel> LoadDashboardAsync(User user, DateTime? from, DateTime? to, int overdueDays)
        {
            bool network = CylinderOrderService.IsAdmin(user) || await _orderService.IsCmsUserAsync(user);
            int? scope = network ? null : user.FacilityId;
            var toDate = (to ?? DateTime.Today).Date;
            var fromDate = (from ?? toDate.AddDays(-90)).Date;
            overdueDays = Math.Clamp(overdueDays, 1, 3650);

            var comboOrder = await GetComboOrderAsync();
            var networkRows = await GetStockRowsAsync();
            var rows = scope == null ? networkRows : ScopeToFacility(networkRows, scope.Value);
            var stock = BuildStock(rows, comboOrder, overdueDays, DateTime.Today);
            if (scope != null) stock.Rows = stock.Rows.Where(r => r.FacilityId == scope).ToList();

            // Patient numbers only for admins and the patient's own facility (not the CMS network view)
            bool canSeePatient = CylinderOrderService.IsAdmin(user) || !network;
            if (!canSeePatient) stock.Overdue.ForEach(o => o.PatientNumber = null);

            return new CylinderDashboardViewModel
            {
                Stock = stock,
                ChartSeries = PickChartSeries(networkRows, comboOrder),
                Requests = BuildRequestSummary(await GetRequestFactsAsync(fromDate, toDate, scope)),
                From = fromDate,
                To = toDate,
                OverdueDays = overdueDays,
                IsNetworkView = network,
                FacilityName = network ? null : await _context.Facilities.Where(f => f.FacilityId == user.FacilityId).Select(f => f.FacilityName).FirstOrDefaultAsync(),
                CanSeePatient = canSeePatient
            };
        }

        // ------------------------------------------------------------------------------------------
        // Excel export (same data as the dashboard)
        // ------------------------------------------------------------------------------------------
        public static byte[] ToExcel(CylinderDashboardViewModel vm)
        {
            using var workbook = new XLWorkbook();

            var stock = workbook.Worksheets.Add("Stock");
            var headers = new List<string> { "Facility" };
            headers.AddRange(vm.Stock.Columns.Select(c => c.Label));
            headers.AddRange(new[] { "Total", "Incoming (in transit)" });
            WriteHeader(stock, headers);
            int row = 2;
            foreach (var r in vm.Stock.Rows)
            {
                int col = 1;
                stock.Cell(row, col++).Value = r.Facility + (r.IsCentralStore ? " (CMS)" : "");
                foreach (var c in vm.Stock.Columns) stock.Cell(row, col++).Value = r.Counts.GetValueOrDefault(c.Key);
                stock.Cell(row, col++).Value = r.Total;
                stock.Cell(row, col).Value = r.Incoming;
                row++;
            }
            stock.Columns().AdjustToContents();

            var requests = workbook.Worksheets.Add("Requests");
            WriteHeader(requests, new List<string> { "Facility", "Requests", "Pending approval", "Rejected", "Requested (approved)", "Ordered", "Dispatched", "Received", "Outstanding" });
            row = 2;
            foreach (var r in vm.Requests)
            {
                var values = new object[] { r.Facility, r.Requests, r.PendingApproval, r.Rejected, r.Requested, r.Ordered, r.Dispatched, r.Received, r.Outstanding };
                for (int i = 0; i < values.Length; i++) requests.Cell(row, i + 1).Value = XLCellValue.FromObject(values[i]);
                row++;
            }
            requests.Columns().AdjustToContents();

            var overdue = workbook.Worksheets.Add($"With patients {vm.OverdueDays}+ days");
            var overdueHeaders = new List<string> { "Tag", "Serial", "Size / Content", "Facility", "Service Point" };
            if (vm.CanSeePatient) overdueHeaders.Add("Patient No.");
            overdueHeaders.AddRange(new[] { "Since", "Days" });
            WriteHeader(overdue, overdueHeaders);
            row = 2;
            foreach (var o in vm.Stock.Overdue)
            {
                var values = new List<object?> { o.Tag, o.Serial, o.Item, o.Facility, o.ServicePoint };
                if (vm.CanSeePatient) values.Add(o.PatientNumber);
                values.Add(o.Since.ToString("yyyy-MM-dd"));
                values.Add(o.Days);
                for (int i = 0; i < values.Count; i++) overdue.Cell(row, i + 1).Value = XLCellValue.FromObject(values[i] ?? "");
                row++;
            }
            overdue.Columns().AdjustToContents();
            ExcelLetterhead.Apply(workbook);

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        private static void WriteHeader(IXLWorksheet sheet, List<string> headers)
        {
            for (int i = 0; i < headers.Count; i++)
            {
                sheet.Cell(1, i + 1).Value = headers[i];
                sheet.Cell(1, i + 1).Style.Font.Bold = true;
                sheet.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
            }
        }
    }
}
