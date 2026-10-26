using System.Globalization;
using EMMS.Constants;
using EMMS.Data;
using EMMS.Models.Admin;
using EMMS.ViewModels;
using Microsoft.EntityFrameworkCore;
using static EMMS.Models.Enumerators;

namespace EMMS.Service
{
    /// <summary>Where an asset's next service date falls relative to today.</summary>
    public enum ServiceWindow { Overdue, ThisWeek, ThisMonth, Later, NotScheduled }

    /// <summary>
    /// Preventive maintenance: which serviceable assets are due, raising their service requests (one or many at once)
    /// and the daily reminder. Closing a successful service request moves the asset's next date on
    /// (<see cref="WorkRequestService.NextServiceDate"/>), so the schedule is the next date of every serviceable asset.
    /// </summary>
    public class ServiceScheduleService
    {
        public const int DueSoonDays = DashboardService.DueSoonDays;            // "this week"
        public const int HorizonDays = DashboardService.ServiceHorizonDays;    // "this month"

        // Values of the page's "show" filter
        public const string ShowDue = "due";                // overdue + due within HorizonDays (default)
        public const string ShowOverdue = "overdue";
        public const string ShowWeek = "week";
        public const string ShowMonth = "month";
        public const string ShowUnscheduled = "unscheduled";
        public const string ShowAll = "all";

        public static readonly (string Value, string Label)[] ShowOptions =
        [
            (ShowDue, $"Overdue or due in {HorizonDays} days"),
            (ShowOverdue, "Overdue"),
            (ShowWeek, $"Due in {DueSoonDays} days"),
            (ShowMonth, $"Due in {DueSoonDays + 1}–{HorizonDays} days"),
            (ShowUnscheduled, "No service date"),
            (ShowAll, "All serviceable equipment"),
        ];

        private readonly ApplicationDbContext _context;
        private readonly WorkRequestService _workRequests;
        private readonly INotificationService _notificationService;

        public ServiceScheduleService(ApplicationDbContext context, WorkRequestService workRequests, INotificationService notificationService)
        {
            _context = context;
            _workRequests = workRequests;
            _notificationService = notificationService;
        }

        // ------------------------------------------------------------------------------------------
        // Rules (pure, unit-tested)
        // ------------------------------------------------------------------------------------------
        public static ServiceWindow WindowFor(DateTime? due, DateTime today)
        {
            if (due == null) return ServiceWindow.NotScheduled;
            var days = (due.Value.Date - today.Date).Days;
            return days switch
            {
                < 0 => ServiceWindow.Overdue,
                <= DueSoonDays => ServiceWindow.ThisWeek,
                <= HorizonDays => ServiceWindow.ThisMonth,
                _ => ServiceWindow.Later,
            };
        }

        public static string NormalizeShow(string? show) =>
            ShowOptions.Any(o => o.Value == show) ? show! : ShowDue;

        public static bool Matches(ServiceWindow window, string show) => show switch
        {
            ShowOverdue => window == ServiceWindow.Overdue,
            ShowWeek => window == ServiceWindow.ThisWeek,
            ShowMonth => window == ServiceWindow.ThisMonth,
            ShowUnscheduled => window == ServiceWindow.NotScheduled,
            ShowAll => true,
            _ => window is ServiceWindow.Overdue or ServiceWindow.ThisWeek or ServiceWindow.ThisMonth,
        };

        /// <summary>The reminder text, or null when nothing needs a request.</summary>
        public static string? ReminderMessage(int overdue, int dueSoon)
        {
            if (overdue <= 0 && dueSoon <= 0) return null;
            var parts = new List<string>();
            if (overdue > 0) parts.Add($"{Assets(overdue)} overdue for service");
            if (dueSoon > 0) parts.Add($"{Assets(dueSoon)} due within {DueSoonDays} days");
            return $"Service schedule: {string.Join(" and ", parts)}, with no service request raised yet.";
        }

        /// <summary>
        /// Service dates of one asset falling in [from, toExclusive): its next service date, then projected visits every
        /// <paramref name="intervalMonths"/> after it. An overdue asset has none, because its next visit depends on when
        /// the overdue service is done (it is shown as overdue instead).
        /// </summary>
        public static IEnumerable<(DateTime Date, bool Projected)> Occurrences(DateTime? due, int? intervalMonths, DateTime today, DateTime from, DateTime toExclusive)
        {
            if (due == null || due.Value.Date < today.Date) yield break;
            var first = due.Value.Date;
            if (first >= from && first < toExclusive) yield return (first, false);
            if (intervalMonths is not > 0) yield break;

            // Count from the first date each time so month-end dates don't drift (31 Jan -> 28 Feb -> 31 Mar)
            for (int k = 1; ; k++)
            {
                var next = first.AddMonths(k * intervalMonths.Value);
                if (next >= toExclusive) yield break;
                if (next >= from) yield return (next, true);
            }
        }

        /// <summary>The weeks (Monday first) that cover <paramref name="month"/>.</summary>
        public static (DateTime Start, DateTime EndExclusive) MonthGrid(DateTime month)
        {
            var first = new DateTime(month.Year, month.Month, 1);
            var start = first.AddDays(-(((int)first.DayOfWeek + 6) % 7));
            var end = first.AddMonths(1);
            var endExclusive = end.AddDays((7 - ((int)end.DayOfWeek + 6) % 7) % 7);
            return (start, endExclusive);
        }

        /// <summary>"2026-11" -> 1 Nov 2026; anything else -> the current month.</summary>
        public static DateTime ParseMonth(string? month, DateTime today) =>
            DateTime.TryParseExact(month, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var m)
                ? m
                : new DateTime(today.Year, today.Month, 1);

        private static string Assets(int n) => n == 1 ? "1 asset" : $"{n.ToString(CultureInfo.InvariantCulture)} assets";

        // ------------------------------------------------------------------------------------------
        // Schedule
        // ------------------------------------------------------------------------------------------
        public async Task<ServiceScheduleViewModel> BuildAsync(int? facilityId, string? show, DateTime today)
        {
            show = NormalizeShow(show);
            var rows = await LoadRowsAsync(facilityId);
            foreach (var r in rows)
                r.Window = WindowFor(r.Due, today);

            int Count(ServiceWindow w) => rows.Count(r => r.Window == w);
            var overdue = Count(ServiceWindow.Overdue);
            var week = Count(ServiceWindow.ThisWeek);
            var month = Count(ServiceWindow.ThisMonth);
            var unscheduled = Count(ServiceWindow.NotScheduled);

            DashLink Filter(string value) => new DashLink("ServiceSchedule").With("show", value).With("facilityId", facilityId);

            return new ServiceScheduleViewModel
            {
                Show = show,
                FacilityId = facilityId,
                Today = today.Date,
                Rows = rows.Where(r => Matches(r.Window, show)).OrderBy(r => r.Due ?? DateTime.MaxValue).ThenBy(r => r.Tag).ToList(),
                Stats =
                {
                    new StatTile { Label = "Overdue", Icon = "fa-solid fa-calendar-xmark", Value = overdue.ToString(), Tone = overdue > 0 ? DashTone.Danger : DashTone.Neutral, Link = Filter(ShowOverdue) },
                    new StatTile { Label = $"Due in {DueSoonDays} days", Icon = "fa-solid fa-calendar-week", Value = week.ToString(), Tone = week > 0 ? DashTone.Warning : DashTone.Neutral, Link = Filter(ShowWeek) },
                    new StatTile { Label = $"Due in {DueSoonDays + 1}–{HorizonDays} days", Icon = "fa-solid fa-calendar-days", Value = month.ToString(), Link = Filter(ShowMonth) },
                    new StatTile { Label = "No service date", Icon = "fa-solid fa-calendar-plus", Value = unscheduled.ToString(), Context = unscheduled > 0 ? "Serviceable, never scheduled" : null, Tone = unscheduled > 0 ? DashTone.Warning : DashTone.Neutral, Link = Filter(ShowUnscheduled) },
                },
            };
        }

        public async Task<ServiceCalendarViewModel> BuildCalendarAsync(int? facilityId, DateTime month, DateTime today)
        {
            month = new DateTime(month.Year, month.Month, 1);
            var (start, endExclusive) = MonthGrid(month);
            var rows = await LoadRowsAsync(facilityId);

            var byDay = rows
                .SelectMany(r => Occurrences(r.Due, r.IntervalMonths, today, start, endExclusive)
                    .Select(o => new ServiceCalendarItem { Row = r, Projected = o.Projected, Date = o.Date }))
                .ToLookup(i => i.Date);

            var model = new ServiceCalendarViewModel
            {
                Month = month,
                Today = today.Date,
                FacilityId = facilityId,
                Overdue = rows.Count(r => WindowFor(r.Due, today) == ServiceWindow.Overdue),
                Unscheduled = rows.Count(r => r.Due == null),
            };
            for (var day = start; day < endExclusive; day = day.AddDays(1))
            {
                model.Days.Add(new ServiceCalendarDay
                {
                    Date = day,
                    InMonth = day.Month == month.Month,
                    // Stored dates before projected ones, still-to-request before requested, then by tag
                    Items = byDay[day].OrderBy(i => i.Projected).ThenBy(i => i.Row.OpenRequest != null).ThenBy(i => i.Row.Tag).ToList(),
                });
            }
            return model;
        }

        private Task<List<ServiceScheduleRow>> LoadRowsAsync(int? facilityId) =>
            AssetScopes.Serviceable(_context, facilityId).AsNoTracking()
                .Select(a => new ServiceScheduleRow
                {
                    AssetId = a.AssetId,
                    Tag = a.AssetTagNumber,
                    Name = string.IsNullOrEmpty(a.ItemName) ? a.SubCategory!.Name : a.ItemName,
                    Due = a.NextServiceDate,
                    IntervalMonths = a.ServiceInterval,
                    FacilityId = _context.AssetCurrentLocations.Where(l => l.AssetId == a.AssetId).Select(l => (int?)l.FacilityId).FirstOrDefault(),
                    Facility = _context.AssetCurrentLocations.Where(l => l.AssetId == a.AssetId)
                        .Select(l => _context.Facilities.Where(f => f.FacilityId == l.FacilityId).Select(f => f.FacilityName).FirstOrDefault())
                        .FirstOrDefault(),
                    ServicePoint = _context.AssetCurrentLocations.Where(l => l.AssetId == a.AssetId)
                        .Select(l => _context.LookupItems.Where(i => i.Id == l.ServicePointId).Select(i => i.Name).FirstOrDefault())
                        .FirstOrDefault(),
                    LastServiced = _context.WorkRequest
                        .Where(w => w.AssetId == a.AssetId && w.IsServiceRequest && w.CloseDate != null
                                 && w.Outcome!.Name == WorkOutcomeNames.Successful && w.RowState == RowStatus.Active)
                        .Max(w => w.CloseDate),
                    // The rule is one unfinished request per asset (WorkRequestService.HasOpenRequestAsync)
                    OpenRequest = _context.WorkRequest
                        .Where(w => w.AssetId == a.AssetId && w.CloseDate == null && w.RowState == RowStatus.Active)
                        .OrderByDescending(w => w.DateCreated)
                        .Select(w => new OpenRequestInfo
                        {
                            IsService = w.IsServiceRequest,
                            Status = w.WorkStatus!.Name,
                            JobId = w.JobId,
                            JobNumber = w.Job!.JobNumber,
                            IsExternal = (bool?)w.Job!.IsExternalProvider,
                        })
                        .FirstOrDefault(),
                })
                .ToListAsync();

        // ------------------------------------------------------------------------------------------
        // Raising service requests
        // ------------------------------------------------------------------------------------------

        /// <summary>
        /// Raises a service request for each asset the user may see on the schedule. Assets that already have an
        /// open request are skipped (and counted), as are ones outside the user's facility. One notification per
        /// facility summarises the batch.
        /// </summary>
        public async Task<(int raised, int skipped, string? error)> RaiseAsync(User user, IReadOnlyCollection<Guid> assetIds, bool isAdmin)
        {
            var ids = assetIds.Distinct().ToList();
            if (ids.Count == 0) return (0, 0, "Select at least one asset.");

            var scope = isAdmin ? (int?)null : user.FacilityId;
            var allowed = await AssetScopes.Serviceable(_context, scope)
                .Where(a => ids.Contains(a.AssetId))
                .Select(a => a.AssetId)
                .ToListAsync();

            // The request goes to the asset's current facility (as in CreateServiceRequestAsync)
            var facilityOf = await _context.AssetCurrentLocations.AsNoTracking()
                .Where(l => allowed.Contains(l.AssetId))
                .ToDictionaryAsync(l => l.AssetId, l => l.FacilityId);

            var raisedPerFacility = new Dictionary<int, int>();
            string? lastError = null;
            foreach (var id in allowed)
            {
                var error = await _workRequests.CreateServiceRequestAsync(user, id, notify: false);
                if (error != null) { lastError = error; continue; }
                var facility = facilityOf.GetValueOrDefault(id, user.FacilityId);
                raisedPerFacility[facility] = raisedPerFacility.GetValueOrDefault(facility) + 1;
            }
            int raised = raisedPerFacility.Values.Sum();
            int skipped = ids.Count - raised;

            foreach (var (facility, count) in raisedPerFacility)
                await _notificationService.CreateServiceRequestsRaisedNotification(facility, count, user.UserId);

            // A single asset that failed reports why; a batch reports counts.
            if (raised == 0 && ids.Count == 1)
                return (0, 1, lastError ?? "That asset is not on your service schedule.");
            return (raised, skipped, null);
        }

        // ------------------------------------------------------------------------------------------
        // Daily reminder
        // ------------------------------------------------------------------------------------------

        /// <summary>
        /// Tells each facility's Biomeds and managers how many assets are overdue or due within
        /// <see cref="DueSoonDays"/> days and still have no request. At most once per facility per day.
        /// </summary>
        /// <returns>Facilities notified.</returns>
        public async Task<int> SendRemindersAsync(DateTime today)
        {
            var soon = today.Date.AddDays(DueSoonDays);
            var open = _context.WorkRequest.Where(w => w.CloseDate == null && w.RowState == RowStatus.Active).Select(w => w.AssetId);

            var due = await AssetScopes.Scheduled(_context, null).AsNoTracking()
                .Where(a => a.NextServiceDate <= soon && !open.Contains(a.AssetId))
                .Select(a => new
                {
                    FacilityId = _context.AssetCurrentLocations.Where(l => l.AssetId == a.AssetId).Select(l => (int?)l.FacilityId).FirstOrDefault(),
                    Overdue = a.NextServiceDate < today.Date,
                })
                .Where(x => x.FacilityId != null)
                .ToListAsync();

            int notified = 0;
            foreach (var facility in due.GroupBy(x => x.FacilityId!.Value))
            {
                var message = ReminderMessage(facility.Count(x => x.Overdue), facility.Count(x => !x.Overdue));
                if (message != null && await _notificationService.CreateServiceDueReminderAsync(facility.Key, message, today))
                    notified++;
            }
            return notified;
        }
    }
}
