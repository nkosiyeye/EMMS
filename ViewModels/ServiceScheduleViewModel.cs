using EMMS.Service;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace EMMS.ViewModels
{
    public class ServiceScheduleViewModel
    {
        public string Show { get; set; } = ServiceScheduleService.ShowDue;
        public int? FacilityId { get; set; }
        public DateTime Today { get; set; }
        public bool CanRaise { get; set; }
        public SelectList? Facilities { get; set; }     // admins only
        public List<StatTile> Stats { get; } = new();
        public List<ServiceScheduleRow> Rows { get; set; } = new();
    }

    public class ServiceScheduleRow
    {
        public Guid AssetId { get; set; }
        public string Tag { get; set; } = "";
        public string? Name { get; set; }
        public int? FacilityId { get; set; }
        public string? Facility { get; set; }
        public string? ServicePoint { get; set; }
        public DateTime? Due { get; set; }
        public int? IntervalMonths { get; set; }
        public DateTime? LastServiced { get; set; }
        public OpenRequestInfo? OpenRequest { get; set; }
        public ServiceWindow Window { get; set; }

        /// <summary>A request can be raised only when none is open (one unfinished request per asset).</summary>
        public bool CanRaise => OpenRequest == null;
    }

    public class ServiceCalendarViewModel
    {
        public DateTime Month { get; set; }             // first day of the month shown
        public DateTime Today { get; set; }
        public int? FacilityId { get; set; }
        public SelectList? Facilities { get; set; }     // admins only
        public int Overdue { get; set; }
        public int Unscheduled { get; set; }
        public List<ServiceCalendarDay> Days { get; } = new();   // whole weeks, Monday first

        public string MonthKey(int offset) => Month.AddMonths(offset).ToString("yyyy-MM");
    }

    public class ServiceCalendarDay
    {
        public DateTime Date { get; set; }
        public bool InMonth { get; set; }
        public List<ServiceCalendarItem> Items { get; set; } = new();
    }

    public class ServiceCalendarItem
    {
        public required ServiceScheduleRow Row { get; set; }
        public DateTime Date { get; set; }
        /// <summary>A later visit worked out from the interval, not a date stored on the asset.</summary>
        public bool Projected { get; set; }
    }

    public class OpenRequestInfo
    {
        public bool IsService { get; set; }
        public string? Status { get; set; }
        public Guid? JobId { get; set; }
        public string? JobNumber { get; set; }
        public bool? IsExternal { get; set; }
    }
}
