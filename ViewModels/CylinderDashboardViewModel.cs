namespace EMMS.ViewModels
{
    /// <summary>One cylinder with where it is now (vw_AssetCurrentLocation) and where it is heading (vw_AssetInTransit).</summary>
    public class CylinderStockRow
    {
        public Guid AssetId { get; set; }
        public string? Tag { get; set; }
        public string? Serial { get; set; }
        public string? Item { get; set; }
        public int SizeId { get; set; }
        public string? Size { get; set; }
        public int ContentId { get; set; }
        public string? Content { get; set; }

        public int? FacilityId { get; set; }          // null = never deployed (no location)
        public string? FacilityName { get; set; }
        public bool AtCentralStore { get; set; }
        public string? ServicePoint { get; set; }
        public bool WithPatient { get; set; }         // service point flagged RequiresPatient
        public string? PatientNumber { get; set; }
        public DateTime? Since { get; set; }          // date received at the current location

        public int? InTransitToFacilityId { get; set; }
        public string? InTransitTo { get; set; }
        public bool Lost { get; set; }                // stolen/missing or decommissioned
    }

    /// <summary>A Size / Content combination. Slot is its fixed palette position (color follows the combination).</summary>
    public class StockColumn
    {
        public string Key { get; set; } = "";
        public string Label { get; set; } = "";
        public int Slot { get; set; }
    }

    public class StockFacilityRow
    {
        public int FacilityId { get; set; }
        public string Facility { get; set; } = "";
        public bool IsCentralStore { get; set; }
        public Dictionary<string, int> Counts { get; set; } = new();
        public int Incoming { get; set; }            // in transit towards this facility (not counted in Total)
        public int Total => Counts.Values.Sum();
    }

    public class CylinderKpis
    {
        public int Total { get; set; }
        public int AtCentralStore { get; set; }
        public int AtFacilities { get; set; }        // facility stores and wards, excluding patients
        public int WithPatients { get; set; }
        public int InTransit { get; set; }
        public int NotAvailable { get; set; }        // stolen/missing, decommissioned, or never deployed
        public int Overdue { get; set; }
    }

    public class OverdueRow
    {
        public string? Tag { get; set; }
        public string? Serial { get; set; }
        public string? Item { get; set; }
        public string? Facility { get; set; }
        public string? ServicePoint { get; set; }
        public string? PatientNumber { get; set; }
        public DateTime Since { get; set; }
        public int Days { get; set; }
    }

    public class StockSummary
    {
        public CylinderKpis Kpis { get; set; } = new();
        public List<StockColumn> Columns { get; set; } = new();    // only combinations that have stock
        public List<StockFacilityRow> Rows { get; set; } = new();  // central store first, then by total
        public List<OverdueRow> Overdue { get; set; } = new();
    }

    /// <summary>One request with its order and dispatch progress (input to the request summary).</summary>
    public class RequestFact
    {
        public string Facility { get; set; } = "";
        public EMMS.Models.Enumerators.CylinderRequestStatus Status { get; set; }
        public int Quantity { get; set; }
        public bool Ordered { get; set; }
        public int OrderQuantity { get; set; }
        public int Dispatched { get; set; }   // items whose movement was not rejected
        public int Received { get; set; }
    }

    public class RequestSummaryRow
    {
        public string Facility { get; set; } = "";
        public int Requests { get; set; }
        public int PendingApproval { get; set; }
        public int Rejected { get; set; }
        public int Requested { get; set; }    // cylinders on approved requests
        public int Ordered { get; set; }
        public int Dispatched { get; set; }
        public int Received { get; set; }
        public int Outstanding => Math.Max(0, Ordered - Received);
    }

    public class CylinderDashboardViewModel
    {
        public StockSummary Stock { get; set; } = new();
        public List<StockColumn> ChartSeries { get; set; } = new();   // top combinations network-wide, slots 0..2; the rest is "Other"
        public List<RequestSummaryRow> Requests { get; set; } = new();
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public int OverdueDays { get; set; }
        public bool IsNetworkView { get; set; }   // admin / CMS: every facility; others: their own facility
        public string? FacilityName { get; set; }
        public bool CanSeePatient { get; set; }
    }
}
