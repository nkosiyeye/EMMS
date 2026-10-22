using System.Globalization;
using System.Linq.Expressions;
using EMMS.Constants;
using EMMS.Data;
using EMMS.Models;
using EMMS.Models.Admin;
using EMMS.ViewModels;
using Microsoft.EntityFrameworkCore;
using static EMMS.Models.Enumerators;

namespace EMMS.Service
{
    /// <summary>
    /// Builds the home dashboard for the signed-in user's role: up to four headline numbers, the role's work queue
    /// and (for admins and facility managers) two breakdowns. Facility name and notifications are already in the
    /// top bar, so they are not repeated here. Everything is scoped on the server: admins see every facility (or
    /// the one they filter to), everyone else only their own facility or their own work.
    /// </summary>
    public class DashboardService
    {
        public const int ServiceHorizonDays = 30;  // the due-for-service table looks this far ahead
        public const int DueSoonDays = 7;          // due within this many days = amber
        public const int StaleDays = 14;           // jobs/requests older than this = amber
        public const int TableLimit = 200;         // rows rendered per table (DataTables pages them)
        public const int BreakdownLimit = 8;
        public const string ServiceDueAnchor = "due-for-service";

        private readonly ApplicationDbContext _context;
        private readonly CylinderOrderService _cylinderService;

        public DashboardService(ApplicationDbContext context, CylinderOrderService cylinderService)
        {
            _context = context;
            _cylinderService = cylinderService;
        }

        // ------------------------------------------------------------------------------------------
        // Rules (pure, unit-tested)
        // ------------------------------------------------------------------------------------------

        /// <summary>Admins may pick a facility (null = all); everyone else is pinned to their own.</summary>
        public static int? ScopeFacility(User user, int? requestedFacilityId) =>
            user.UserRole?.UserType == UserType.Administrator ? requestedFacilityId : user.FacilityId;

        /// <summary>
        /// A movement waiting for the facility manager: still active, not approved, and not already rejected or
        /// received at the destination (rejection stamps DateRejected; deleting a movement removes the row).
        /// </summary>
        public static readonly Expression<Func<MoveAsset, bool>> AwaitingApproval = m =>
            m.RowState == RowStatus.Active && !m.IsApproved && m.DateRejected == null && m.DateReceived == null;

        public static int Percent(int part, int whole) => whole <= 0 ? 0 : (int)Math.Round(part * 100.0 / whole);

        // Calm unless it is a problem: no green for "fine".
        public static DashTone FunctionalTone(int percent, int total) =>
            total == 0 ? DashTone.Neutral : percent < 75 ? DashTone.Danger : percent < 90 ? DashTone.Warning : DashTone.Neutral;

        public static DashTone CountTone(int count, DashTone whenAny) => count > 0 ? whenAny : DashTone.Neutral;

        public static DashTone ServiceDueTone(DateTime due, DateTime today) =>
            due.Date < today.Date ? DashTone.Danger
            : due.Date <= today.Date.AddDays(DueSoonDays) ? DashTone.Warning
            : DashTone.Neutral;

        public static DashTone AgeTone(DateTime since, DateTime now) =>
            since < now.AddDays(-StaleDays) ? DashTone.Warning : DashTone.Neutral;

        public static string AgeText(DateTime since, DateTime now)
        {
            var days = (now.Date - since.Date).Days;
            return days switch
            {
                <= 0 => "today",
                1 => "1 day ago",
                _ => $"{days} days ago"
            };
        }

        public static string OverdueText(DateTime due, DateTime today)
        {
            var days = (today.Date - due.Date).Days;
            return days switch
            {
                < 0 => $"in {-days} day{(days == -1 ? "" : "s")}",
                0 => "due today",
                1 => "1 day overdue",
                _ => $"{days} days overdue"
            };
        }

        public static string FunctionalLabel(FunctionalStatus s) => s switch
        {
            FunctionalStatus.Functional => "Functional",
            FunctionalStatus.NonFunctional => "Non-functional",
            FunctionalStatus.UnderMaintenance => "Under maintenance",
            FunctionalStatus.Idle => "Idle",
            _ => "Unknown",
        };

        public static DashTone FunctionalStatusTone(FunctionalStatus s) => s switch
        {
            FunctionalStatus.Functional => DashTone.Success,
            FunctionalStatus.NonFunctional => DashTone.Danger,
            FunctionalStatus.UnderMaintenance => DashTone.Warning,
            _ => DashTone.Neutral,
        };

        private static string Date(DateTime d) => d.ToString("d MMM yyyy", CultureInfo.InvariantCulture);
        private static string Plural(int n, string word) => $"{n} {word}{(n == 1 ? "" : "s")}";

        // ------------------------------------------------------------------------------------------
        // Entry point
        // ------------------------------------------------------------------------------------------
        public async Task<HomeDashboardViewModel> BuildAsync(User user, int? facilityId = null)
        {
            var role = user.UserRole?.UserType ?? UserType.GeneralUser;
            var vm = new HomeDashboardViewModel { Role = role, FacilityId = ScopeFacility(user, facilityId) };
            var now = DateTime.Now;

            switch (role)
            {
                case UserType.Administrator:
                    await BuildAdminAsync(vm, now);
                    break;
                case UserType.FacilityManager:
                    await BuildFacilityManagerAsync(vm, now);
                    break;
                case UserType.Biomed:
                    await BuildBiomedAsync(user, vm, now);
                    break;
                case UserType.DataCollector:
                    await BuildDataCollectorAsync(user, vm, now);
                    break;
                case UserType.Pharmacist:
                    await BuildPharmacistAsync(user, vm, now);
                    break;
                case UserType.CmsOfficer when await _cylinderService.IsCmsUserAsync(user):
                    await BuildCmsAsync(vm, now);
                    break;
                default: // GeneralUser, and a CMS officer not posted at the central store
                    await BuildGeneralUserAsync(user, vm, now);
                    break;
            }
            return vm;
        }

        // ------------------------------------------------------------------------------------------
        // Roles
        // ------------------------------------------------------------------------------------------
        private async Task BuildAdminAsync(HomeDashboardViewModel vm, DateTime now)
        {
            var pendingUsers = await _context.User.CountAsync(u => u.RowState == RowStatus.Inactive);
            if (pendingUsers > 0)
                vm.Notices.Add(new DashNotice
                {
                    Text = $"{Plural(pendingUsers, "user account")} {(pendingUsers == 1 ? "is" : "are")} waiting for activation.",
                    LinkText = "Review",
                    // The Users list with its Status filter set to the same accounts
                    Link = new DashLink("Users").With("Status", nameof(RowStatus.Inactive)),
                });

            await AddEquipmentOverviewAsync(vm, now);
        }

        private async Task BuildFacilityManagerAsync(HomeDashboardViewModel vm, DateTime now)
        {
            var pendingApprovals = await _context.AssetMovement.Where(AwaitingApproval).CountAsync(m => m.FromId == vm.FacilityId);
            if (pendingApprovals > 0)
            {
                vm.Notices.Add(new DashNotice
                {
                    Text = $"{Plural(pendingApprovals, "movement")} {(pendingApprovals == 1 ? "is" : "are")} waiting for approval.",
                    LinkText = "Review",
                    // The Movements list filtered to the same rows (its "Approved: Pending" and "Received: Pending" filters)
                    Link = new DashLink("AssetMovement").With("Approved", "false").With("Received", "pending"),
                });
            }
            await AddEquipmentOverviewAsync(vm, now);
        } 

        // Admin + facility manager: health numbers, status breakdown, out-of-service by location, due-for-service table.
        private async Task AddEquipmentOverviewAsync(HomeDashboardViewModel vm, DateTime now)
        {
            var facilityId = vm.FacilityId;
            var assets = InServiceAssets(facilityId);

            var byStatus = await assets.GroupBy(a => a.FunctionalStatus)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();
            var total = byStatus.Sum(s => s.Count);
            var functional = byStatus.Where(s => s.Status == FunctionalStatus.Functional).Sum(s => s.Count);
            var pct = Percent(functional, total);

            var openRequests = await OpenAssetRequests(facilityId).CountAsync() + await OpenInfraRequests(facilityId).CountAsync();
            var noJob = await OpenAssetRequests(facilityId).CountAsync(w => w.JobId == null)
                      + await OpenInfraRequests(facilityId).CountAsync(w => w.JobId == null);
            var overdue = await ServiceableAssets(facilityId).CountAsync(a => a.NextServiceDate < now.Date);

            vm.Stats.Add(new StatTile { Label = "Equipment in service", Icon = "fa-solid fa-box-open", Value = total.ToString("N0", CultureInfo.InvariantCulture), Link = new DashLink("AssetManagement") });
            vm.Stats.Add(new StatTile { Label = "Functional", Icon = "fa-solid fa-heart-pulse", Value = $"{pct}%", Context = $"{functional:N0} of {total:N0}", Tone = FunctionalTone(pct, total), Link = new DashLink("AssetManagement") });
            vm.Stats.Add(new StatTile { Label = "Open work requests", Icon = "fa-solid fa-clipboard-list", Value = openRequests.ToString(), Context = noJob > 0 ? $"{noJob} without a job yet" : null, Tone = CountTone(noJob, DashTone.Warning), Link = new DashLink("JobManagement") });
            vm.Stats.Add(new StatTile { Label = "Overdue for service", Icon = "fa-solid fa-calendar-xmark", Value = overdue.ToString(), Tone = CountTone(overdue, DashTone.Danger), Link = DashLink.Anchor(ServiceDueAnchor) });

            vm.Breakdowns.Add(new Breakdown
            {
                Title = "Equipment status",
                Rows = byStatus.OrderBy(s => s.Status == FunctionalStatus.Unknown ? int.MaxValue : (int)s.Status)
                    .Select(s => new BreakdownRow
                    {
                        Label = FunctionalLabel(s.Status),
                        Count = s.Count,
                        Tone = FunctionalStatusTone(s.Status),
                        // Unknown is mostly assets with no location yet, which the asset list cannot filter by status
                        Link = s.Status == FunctionalStatus.Unknown ? null : AssetList(facilityId).With("Status", (int)s.Status),
                    })
                    .ToList(),
                EmptyText = "No equipment recorded yet.",
            });
            vm.Breakdowns.Add(await OutOfServiceBreakdownAsync(facilityId));

            vm.Tables.Add(await ServiceDueTableAsync(facilityId, now));
        }

        private async Task BuildBiomedAsync(User user, HomeDashboardViewModel vm, DateTime now)
        {
            var facilityId = vm.FacilityId;
            var monthStart = new DateTime(now.Year, now.Month, 1);
            var myJobs = _context.Job.AsNoTracking().Where(j => j.RowState == RowStatus.Active && j.AssignedTo == user.UserId);
            var inProgress = myJobs.Where(j => j.Status!.Name == WorkStatusNames.InProgress && j.EndDate == null);

            var inProgressCount = await inProgress.CountAsync();
            var completed = await myJobs.CountAsync(j => j.EndDate != null && j.EndDate >= monthStart);
            var unassigned = await OpenAssetRequests(facilityId).CountAsync(w => w.JobId == null)
                           + await OpenInfraRequests(facilityId).CountAsync(w => w.JobId == null);
            var overdue = await ServiceableAssets(facilityId).CountAsync(a => a.NextServiceDate < now.Date);

            vm.Stats.Add(new StatTile { Label = "My jobs in progress", Icon = "fa-solid fa-screwdriver-wrench", Value = inProgressCount.ToString(), Link = new DashLink("JobManagement", "manageJobs") });
            vm.Stats.Add(new StatTile { Label = "Completed this month", Icon = "fa-solid fa-circle-check", Value = completed.ToString(), Link = new DashLink("JobManagement", "manageJobs") });
            vm.Stats.Add(new StatTile { Label = "Requests without a job", Icon = "fa-solid fa-inbox", Value = unassigned.ToString(), Tone = CountTone(unassigned, DashTone.Warning), Link = new DashLink("JobManagement") });
            vm.Stats.Add(new StatTile { Label = "Overdue for service", Icon = "fa-solid fa-calendar-xmark", Value = overdue.ToString(), Tone = CountTone(overdue, DashTone.Danger), Link = DashLink.Anchor(ServiceDueAnchor) });

            var jobs = await inProgress
                .Include(j => j.Asset!).ThenInclude(a => a.SubCategory)
                .Include(j => j.InfraWorkRequest)
                .Include(j => j.Facility)
                .OrderBy(j => j.StartDate)
                .Take(TableLimit)
                .ToListAsync();
            vm.Tables.Add(new WorkTable
            {
                Title = "My jobs in progress",
                Columns = { "Job", "Equipment", "Facility", "Started" },
                EmptyText = "You have no jobs in progress.",
                ViewAll = new DashLink("JobManagement", "manageJobs"),
                Rows = jobs.Select(j => new TableRow
                {
                    Cells =
                    {
                        new TableCell { Text = j.JobNumber ?? "—" },
                        j.Asset != null
                            ? new TableCell { Text = AssetName(j.Asset), Sub = j.Asset.AssetTagNumber }
                            : new TableCell { Text = j.InfraWorkRequest?.Description ?? "Infrastructure", Sub = "Infrastructure" },
                        new TableCell { Text = j.Facility?.FacilityName ?? "—" },
                        new TableCell { Text = Date(j.StartDate), Sub = AgeText(j.StartDate, now), SubTone = AgeTone(j.StartDate, now) },
                    },
                    Action = new RowAction { Label = "View job", Link = new DashLink("JobManagement", "jobCard", j.JobId) },
                }).ToList(),
            });

            vm.Tables.Add(await ServiceDueTableAsync(facilityId, now));
        }

        private async Task BuildGeneralUserAsync(User user, HomeDashboardViewModel vm, DateTime now)
        {
            var facilityId = vm.FacilityId!.Value;
            var mine = OpenAssetRequests(null).Where(w => w.RequestedBy == user.UserId);
            var mineInfra = OpenInfraRequests(null).Where(w => w.RequestedBy == user.UserId);

            var myOpen = await mine.CountAsync() + await mineInfra.CountAsync();
            var toClose = await mine.CountAsync(w => w.WorkStatus!.Name == WorkStatusNames.Completed)
                        + await mineInfra.CountAsync(w => w.WorkStatus!.Name == WorkStatusNames.Completed);
            var incoming = await _context.AssetsInTransit.CountAsync(t => t.FacilityId == facilityId && t.IsApproved);
            var nonFunctional = await InServiceAssets(facilityId).CountAsync(a => a.FunctionalStatus == FunctionalStatus.NonFunctional);

            vm.Stats.Add(new StatTile { Label = "My open requests", Icon = "fa-solid fa-clipboard-list", Value = myOpen.ToString(), Link = new DashLink("JobManagement") });
            vm.Stats.Add(new StatTile { Label = "Ready for you to close", Icon = "fa-solid fa-flag-checkered", Value = toClose.ToString(), Tone = CountTone(toClose, DashTone.Warning), Link = new DashLink("JobManagement") });
            vm.Stats.Add(new StatTile { Label = "Assets to receive", Icon = "fa-solid fa-truck-ramp-box", Value = incoming.ToString(), Tone = CountTone(incoming, DashTone.Warning), Link = new DashLink("AssetMovement", "RecieveAsset") });
            vm.Stats.Add(new StatTile { Label = "Non-functional equipment", Icon = "fa-solid fa-plug-circle-xmark", Value = nonFunctional.ToString(), Context = "at your facility", Tone = CountTone(nonFunctional, DashTone.Danger), Link = new DashLink("AssetManagement") });

            var requests = await mine
                .Include(w => w.Asset!).ThenInclude(a => a.SubCategory)
                .Include(w => w.WorkStatus)
                .OrderBy(w => w.RequestDate)
                .Take(TableLimit)
                .ToListAsync();
            var infra = await mineInfra
                .Include(w => w.WorkStatus)
                .OrderBy(w => w.RequestDate)
                .Take(TableLimit)
                .ToListAsync();

            var rows = requests.Select(w => (w.RequestDate, Row: RequestRow(
                    w.WorkRequestId, w.Asset != null ? AssetName(w.Asset) : "Equipment", w.Asset?.AssetTagNumber, w.RequestDate, w.WorkStatus?.Name, now)))
                .Concat(infra.Select(w => (w.RequestDate, Row: RequestRow(
                    w.WorkRequestId, w.Description ?? "Infrastructure", "Infrastructure", w.RequestDate, w.WorkStatus?.Name, now))))
                .OrderBy(r => r.RequestDate)
                .Select(r => r.Row)
                .ToList();

            vm.Tables.Add(new WorkTable
            {
                Title = "My open requests",
                Columns = { "Equipment", "Raised", "Status" },
                EmptyText = "You have no open requests.",
                ViewAll = new DashLink("JobManagement"),
                Rows = rows,
            });
        }

        // Both kinds of request are listed on JobManagement/Index; the link opens it on this row (and its Close dialog).
        private static TableRow RequestRow(Guid requestId, string name, string? sub, DateTime raised, string? status, DateTime now)
        {
            var readyToClose = status == WorkStatusNames.Completed;
            var link = new DashLink("JobManagement").Focus(requestId, readyToClose ? "close" : null);
            return new TableRow
            {
                Cells =
                {
                    new TableCell { Text = name, Sub = sub },
                    new TableCell { Text = Date(raised), Sub = AgeText(raised, now), SubTone = AgeTone(raised, now) },
                    new TableCell { Text = status ?? "—", Sub = readyToClose ? "Job done, confirm the outcome" : null, SubTone = readyToClose ? DashTone.Warning : DashTone.Neutral },
                },
                Action = readyToClose
                    ? new RowAction { Label = "Close request", Icon = "fa-solid fa-circle-check", Style = "success", Link = link }
                    : new RowAction { Label = "View request", Link = link },
            };
        }

        private async Task BuildDataCollectorAsync(User user, HomeDashboardViewModel vm, DateTime now)
        {
            var today = now.Date;
            var weekStart = today.AddDays(-(((int)today.DayOfWeek + 6) % 7)); // Monday
            var mine = _context.Assets.AsNoTracking().Where(a => a.RowState == RowStatus.Active && a.CreatedBy == user.UserId);
            var located = _context.AssetCurrentLocations.Select(l => l.AssetId);
            var gaps = mine.Where(a => a.FunctionalStatus == FunctionalStatus.Unknown || !located.Contains(a.AssetId));

            var todayCount = await mine.CountAsync(a => a.DateCreated >= today);
            var weekCount = await mine.CountAsync(a => a.DateCreated >= weekStart);
            var total = await mine.CountAsync();
            var gapCount = await gaps.CountAsync();

            vm.Stats.Add(new StatTile { Label = "Registered today", Icon = "fa-solid fa-plus", Value = todayCount.ToString(), Link = new DashLink("AssetManagement") });
            vm.Stats.Add(new StatTile { Label = "Registered this week", Icon = "fa-solid fa-calendar-week", Value = weekCount.ToString(), Link = new DashLink("AssetManagement") });
            vm.Stats.Add(new StatTile { Label = "Total registered", Icon = "fa-solid fa-box-open", Value = total.ToString("N0", CultureInfo.InvariantCulture), Link = new DashLink("AssetManagement") });
            vm.Stats.Add(new StatTile { Label = "Records to complete", Icon = "fa-solid fa-pen-to-square", Value = gapCount.ToString(), Tone = CountTone(gapCount, DashTone.Warning) });

            var gapAssets = await gaps
                .Include(a => a.SubCategory)
                .OrderBy(a => a.DateCreated)
                .Take(TableLimit)
                .ToListAsync();
            var ids = gapAssets.Select(a => a.AssetId).ToList();
            var withLocation = (await located.Where(id => ids.Contains(id)).ToListAsync()).ToHashSet();

            vm.Tables.Add(new WorkTable
            {
                Title = "Records to complete",
                Columns = { "Equipment", "Registered", "Missing" },
                EmptyText = "All your records are complete.",
                Rows = gapAssets.Select(a => new TableRow
                {
                    Cells =
                    {
                        new TableCell { Text = AssetName(a), Sub = a.AssetTagNumber },
                        new TableCell { Text = a.DateCreated != null ? Date(a.DateCreated.Value) : "—" },
                        new TableCell { Text = withLocation.Contains(a.AssetId) ? "Functional status" : "Location" },
                    },
                    Action = new RowAction { Label = "View asset", Link = new DashLink("AssetManagement", "Detail", a.AssetId) },
                }).ToList(),
            });
        }

        private async Task BuildPharmacistAsync(User user, HomeDashboardViewModel vm, DateTime now)
        {
            var monthStart = new DateTime(now.Year, now.Month, 1);
            var requests = _context.CylinderRequests.AsNoTracking()
                .Where(r => r.RowState == RowStatus.Active && r.FacilityId == user.FacilityId);
            var pending = requests.Where(r => r.Status == CylinderRequestStatus.Requested);

            var pendingCount = await pending.CountAsync();
            var approved = await requests.CountAsync(r => r.Status == CylinderRequestStatus.Approved && r.DateApproved >= monthStart);
            var rejected = await requests.CountAsync(r => r.Status == CylinderRequestStatus.Rejected && r.DateApproved >= monthStart);

            vm.Stats.Add(new StatTile { Label = "Waiting for your approval", Icon = "fa-solid fa-hourglass-half", Value = pendingCount.ToString(), Tone = CountTone(pendingCount, DashTone.Warning), Link = new DashLink("Cylinder") });
            vm.Stats.Add(new StatTile { Label = "Approved this month", Icon = "fa-solid fa-circle-check", Value = approved.ToString(), Link = new DashLink("Cylinder") });
            vm.Stats.Add(new StatTile { Label = "Rejected this month", Icon = "fa-solid fa-circle-xmark", Value = rejected.ToString(), Link = new DashLink("Cylinder") });

            var items = await pending
                .Include(r => r.Size).Include(r => r.Content).Include(r => r.Requester)
                .OrderBy(r => r.DateCreated)
                .Take(TableLimit)
                .ToListAsync();
            vm.Tables.Add(new WorkTable
            {
                Title = "Cylinder requests waiting for approval",
                Columns = { "Requisition", "Cylinders", "Requested by", "Requested" },
                EmptyText = "No requests are waiting for approval.",
                ViewAll = new DashLink("Cylinder"),
                Rows = items.Select(r => new TableRow
                {
                    Cells =
                    {
                        new TableCell { Text = r.RequisitionNumber ?? "—", Sub = r.RequisitionName },
                        new TableCell { Text = $"{r.Quantity} × {r.Size?.Name} {r.Content?.Name}".Trim() },
                        new TableCell { Text = $"{r.Requester?.FirstName} {r.Requester?.LastName}".Trim() },
                        new TableCell { Text = r.DateCreated != null ? Date(r.DateCreated.Value) : "—", Sub = r.DateCreated != null ? AgeText(r.DateCreated.Value, now) : null },
                    },
                    Action = new RowAction { Label = "Review request", Icon = "fa-solid fa-clipboard-check", Style = "primary", Link = new DashLink("Cylinder").Focus(r.RequestId) },
                }).ToList(),
            });
        }

        private async Task BuildCmsAsync(HomeDashboardViewModel vm, DateTime now)
        {
            var monthStart = new DateTime(now.Year, now.Month, 1);
            var orders = _context.CylinderOrders.AsNoTracking().Where(o => o.RowState == RowStatus.Active);
            var orderedRequestIds = orders.Where(o => o.Status != CylinderOrderStatus.PlantDown).Select(o => o.RequestId);
            var toOrder = _context.CylinderRequests.AsNoTracking()
                .Where(r => r.RowState == RowStatus.Active && r.Status == CylinderRequestStatus.Approved && !orderedRequestIds.Contains(r.RequestId));
            var toProcess = orders.Where(o => o.Status == CylinderOrderStatus.Ordered || o.Status == CylinderOrderStatus.Modified);

            var toOrderCount = await toOrder.CountAsync();
            var toDispatch = await toProcess.CountAsync();
            var inTransit = await orders.CountAsync(o => o.Status == CylinderOrderStatus.Dispatched);
            var plantDown = await orders.CountAsync(o => o.Status == CylinderOrderStatus.PlantDown && o.DateModified >= monthStart);

            vm.Stats.Add(new StatTile { Label = "Approved, not yet ordered", Icon = "fa-solid fa-file-circle-plus", Value = toOrderCount.ToString(), Tone = CountTone(toOrderCount, DashTone.Warning), Link = new DashLink("Cylinder") });
            vm.Stats.Add(new StatTile { Label = "Orders to dispatch", Icon = "fa-solid fa-truck-fast", Value = toDispatch.ToString(), Tone = CountTone(toDispatch, DashTone.Warning), Link = new DashLink("Cylinder") });
            vm.Stats.Add(new StatTile { Label = "Dispatched, not received", Icon = "fa-solid fa-route", Value = inTransit.ToString(), Link = new DashLink("Cylinder") });
            vm.Stats.Add(new StatTile { Label = "Plant down this month", Icon = "fa-solid fa-industry", Value = plantDown.ToString(), Tone = CountTone(plantDown, DashTone.Danger), Link = new DashLink("Cylinder") });

            var requests = await toOrder
                .Include(r => r.Facility).Include(r => r.Size).Include(r => r.Content)
                .OrderBy(r => r.DateApproved)
                .Take(TableLimit)
                .ToListAsync();
            var pendingOrders = await toProcess
                .Include(o => o.Request!).ThenInclude(r => r.Facility)
                .Include(o => o.Request!).ThenInclude(r => r.Size)
                .Include(o => o.Request!).ThenInclude(r => r.Content)
                .OrderBy(o => o.DateCreated)
                .Take(TableLimit)
                .ToListAsync();

            TableRow Row(string facility, string cylinders, string stage, DateTime? since, RowAction action) => new()
            {
                Cells =
                {
                    new TableCell { Text = facility },
                    new TableCell { Text = cylinders },
                    new TableCell { Text = stage },
                    new TableCell { Text = since != null ? Date(since.Value) : "—", Sub = since != null ? AgeText(since.Value, now) : null },
                },
                Action = action,
            };

            var rows = requests.Select(r => (Since: r.DateApproved, Row: Row(
                    r.Facility?.FacilityName ?? "—", $"{r.Quantity} × {r.Size?.Name} {r.Content?.Name}".Trim(), "Approved — create order", r.DateApproved,
                    new RowAction { Label = "Create order", Icon = "fa-solid fa-plus", Style = "primary", Link = new DashLink("Cylinder").Focus(r.RequestId, "create") })))
                .Concat(pendingOrders.Select(o => (Since: o.DateCreated, Row: Row(
                    o.Request?.Facility?.FacilityName ?? "—", $"{o.Quantity} × {o.Request?.Size?.Name} {o.Request?.Content?.Name}".Trim(),
                    o.Status == CylinderOrderStatus.Modified ? $"{o.OrderNumber} — modified, re-submit" : $"{o.OrderNumber} — ready to dispatch",
                    o.DateCreated,
                    o.Status == CylinderOrderStatus.Ordered
                        ? new RowAction { Label = "Dispatch", Icon = "fa-solid fa-truck-fast", Style = "primary", Link = new DashLink("Cylinder", "Dispatch", o.OrderId) }
                        : new RowAction { Label = "View order", Link = new DashLink("Cylinder").Focus(o.OrderId) }))))
                .OrderBy(r => r.Since ?? DateTime.MaxValue)
                .Select(r => r.Row)
                .ToList();

            vm.Tables.Add(new WorkTable
            {
                Title = "Orders to process",
                Columns = { "Facility", "Cylinders", "Stage", "Since" },
                EmptyText = "Nothing to order or dispatch.",
                ViewAll = new DashLink("Cylinder"),
                Rows = rows,
            });
        }

        // ------------------------------------------------------------------------------------------
        // Shared pieces
        // ------------------------------------------------------------------------------------------

        // All facilities: out of service per facility. One facility: per service point.
        // Grouping runs in SQL; the groups (one per facility / service point) are few, so the top N is taken here
        // and the rest summed into the "+N more" line.
        private async Task<Breakdown> OutOfServiceBreakdownAsync(int? facilityId)
        {
            var down = from a in InServiceAssets(facilityId)
                       where a.FunctionalStatus == FunctionalStatus.NonFunctional || a.FunctionalStatus == FunctionalStatus.UnderMaintenance
                       join l in _context.AssetCurrentLocations on a.AssetId equals l.AssetId
                       select new { a.AssetId, l.FacilityId, l.ServicePointId };

            List<BreakdownRow> rows;
            string title, unit;
            if (facilityId == null)
            {
                title = "Out of service by facility";
                unit = "facility";
                var groups = await (from d in down
                                    join f in _context.Facilities on d.FacilityId equals f.FacilityId
                                    group d by new { f.FacilityId, f.FacilityName } into g
                                    select new { g.Key.FacilityId, g.Key.FacilityName, Count = g.Count() })
                    .ToListAsync();
                rows = groups.OrderByDescending(g => g.Count).ThenBy(g => g.FacilityName)
                    .Select(g => new BreakdownRow
                    {
                        Label = g.FacilityName ?? "Unknown facility",
                        Count = g.Count,
                        Tone = DashTone.Info,
                        Link = AssetList(g.FacilityId).With("Status", AssetService.OutOfServiceFilter),
                    })
                    .ToList();
            }
            else
            {
                title = "Out of service by service point";
                unit = "service point";
                var groups = await (from d in down
                                    join sp in _context.LookupItems on d.ServicePointId equals sp.Id into sps
                                    from sp in sps.DefaultIfEmpty()
                                    group d by new { Id = sp == null ? (int?)null : sp.Id, Name = sp == null ? null : sp.Name } into g
                                    select new { g.Key.Id, g.Key.Name, Count = g.Count() })
                    .ToListAsync();
                rows = groups.OrderByDescending(g => g.Count).ThenBy(g => g.Name)
                    .Select(g => new BreakdownRow
                    {
                        Label = g.Name ?? "No service point",
                        Count = g.Count,
                        Tone = DashTone.Info,
                        // The asset list cannot filter on "no service point", so that bar has no link
                        Link = g.Id == null ? null : AssetList(facilityId).With("ServicePoint", g.Id).With("Status", AssetService.OutOfServiceFilter),
                    })
                    .ToList();
            }

            return new Breakdown
            {
                Title = title,
                Rows = rows.Take(BreakdownLimit).ToList(),
                More = MoreText(rows.Skip(BreakdownLimit).ToList(), unit),
                EmptyText = "No equipment is out of service.",
            };
        }

        public static string? MoreText(IReadOnlyCollection<BreakdownRow> hidden, string unit)
        {
            if (hidden.Count == 0) return null;
            var units = hidden.Count == 1 ? unit : unit.EndsWith("y") ? unit[..^1] + "ies" : unit + "s";
            return $"+{hidden.Count} more {units} ({Plural(hidden.Sum(r => r.Count), "item")})";
        }

        // Overdue or due within ServiceHorizonDays, most overdue first. A row with an open request shows "Requested".
        private async Task<WorkTable> ServiceDueTableAsync(int? facilityId, DateTime now)
        {
            var horizon = now.Date.AddDays(ServiceHorizonDays);
            var due = await ServiceableAssets(facilityId)
                .Where(a => a.NextServiceDate <= horizon)
                .OrderBy(a => a.NextServiceDate)
                .Take(TableLimit)
                .Select(a => new
                {
                    a.AssetId,
                    a.AssetTagNumber,
                    a.ItemName,
                    SubCategory = a.SubCategory!.Name,
                    Due = a.NextServiceDate!.Value,
                    Location = _context.AssetCurrentLocations.Where(l => l.AssetId == a.AssetId)
                        .Select(l => new
                        {
                            Facility = _context.Facilities.Where(f => f.FacilityId == l.FacilityId).Select(f => f.FacilityName).FirstOrDefault(),
                            ServicePoint = _context.LookupItems.Where(i => i.Id == l.ServicePointId).Select(i => i.Name).FirstOrDefault(),
                        })
                        .FirstOrDefault(),
                    Requested = _context.WorkRequest.Any(w => w.AssetId == a.AssetId && w.CloseDate == null && w.RowState == RowStatus.Active),
                })
                .ToListAsync();

            return new WorkTable
            {
                Title = "Due for service",
                Anchor = ServiceDueAnchor,
                Columns = { "Equipment", "Location", "Service due" },
                EmptyText = $"Nothing is due for service in the next {ServiceHorizonDays} days.",
                Rows = due.Select(a => new TableRow
                {
                    Cells =
                    {
                        new TableCell
                        {
                            Text = string.IsNullOrWhiteSpace(a.ItemName) ? a.SubCategory ?? "—" : a.ItemName,
                            Sub = a.AssetTagNumber,
                            Link = new DashLink("AssetManagement", "Detail", a.AssetId),
                        },
                        new TableCell { Text = a.Location?.Facility ?? "Not deployed", Sub = a.Location?.ServicePoint },
                        new TableCell { Text = Date(a.Due), Sub = OverdueText(a.Due, now), SubTone = ServiceDueTone(a.Due, now) },
                    },
                    Action = a.Requested
                        ? new RowAction { Label = "Requested" }
                        : new RowAction { Label = "Request service", Icon = "fa-solid fa-toolbox", Style = "primary", ServiceRequestAssetId = a.AssetId },
                }).ToList(),
            };
        }

        // ------------------------------------------------------------------------------------------
        // Queries
        // ------------------------------------------------------------------------------------------

        // Active assets that are not decommissioned or lost; at a facility = its current (received) location is there.
        private IQueryable<Asset> InServiceAssets(int? facilityId)
        {
            var lost = _context.AssetCurrentLocations
                .Where(l => l.Reason == MovementReason.Decommission || l.Reason == MovementReason.StolenorMissing)
                .Select(l => l.AssetId);
            var assets = _context.Assets.AsNoTracking()
                .Where(a => a.RowState == RowStatus.Active
                         && a.StatusId != (int)ProcurementStatus.Decommissioned
                         && !lost.Contains(a.AssetId));
            if (facilityId == null) return assets;

            var here = _context.AssetCurrentLocations.Where(l => l.FacilityId == facilityId).Select(l => l.AssetId);
            return assets.Where(a => here.Contains(a.AssetId));
        }

        private IQueryable<Asset> ServiceableAssets(int? facilityId) =>
            InServiceAssets(facilityId).Where(a => a.IsServiceable && a.NextServiceDate != null);

        // Closing and cancelling both stamp CloseDate, so open = no CloseDate.
        private IQueryable<WorkRequest> OpenAssetRequests(int? facilityId) =>
            _context.WorkRequest.AsNoTracking()
                .Where(w => w.RowState == RowStatus.Active && w.CloseDate == null && (facilityId == null || w.FacilityId == facilityId));

        private IQueryable<InfrustructureWorkRequest> OpenInfraRequests(int? facilityId) =>
            _context.InfrustructureWorkRequest.AsNoTracking()
                .Where(w => w.RowState == RowStatus.Active && w.CloseDate == null && (facilityId == null || w.FacilityId == facilityId));

        // The asset list (AssetManagement/Index) opened with its header filters preset from the query string
        // (site.js emmsColumnFilters); a null facility leaves that filter off.
        private static DashLink AssetList(int? facilityId) => new DashLink("AssetManagement").With("Facility", facilityId);

        private static string AssetName(Asset a) =>
            string.IsNullOrWhiteSpace(a.ItemName) ? a.SubCategory?.Name ?? a.AssetTagNumber : a.ItemName;
    }
}
