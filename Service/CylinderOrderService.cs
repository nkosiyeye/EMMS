using System.Data;
using System.Globalization;
using EMMS.Constants;
using EMMS.Data;
using EMMS.Models;
using EMMS.Models.Admin;
using EMMS.Models.Entities;
using EMMS.ViewModels;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using static EMMS.Models.Enumerators;

namespace EMMS.Service
{
    /// <summary>
    /// Request (facility, approved by the pharmacist) -> Order (CMS) -> Dispatch (Movement Queue, pre-approved)
    /// -> Receive (facility). Order status is derived from its items' movements.
    /// </summary>
    public class CylinderOrderService
    {
        private readonly ApplicationDbContext _context;
        private readonly INotificationService _notificationService;

        public CylinderOrderService(ApplicationDbContext context, INotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        // ------------------------------------------------------------------------------------------
        // Roles (facility re-read from the database, never trusted from the session)
        // ------------------------------------------------------------------------------------------
        public static bool IsAdmin(User? u) => u?.UserRole?.UserType == UserType.Administrator;
        public static bool IsPharmacist(User? u) => u?.UserRole?.UserType == UserType.Pharmacist;

        public async Task<bool> IsCmsUserAsync(User? u)
        {
            if (u == null) return false;
            if (IsAdmin(u)) return true;
            return u.UserRole?.UserType == UserType.CmsOfficer &&
                   await _context.Facilities.AnyAsync(f => f.FacilityId == u.FacilityId && f.IsCentralStore);
        }

        public static bool CanDecide(User u, CylinderRequest r) =>
            IsAdmin(u) || (IsPharmacist(u) && u.FacilityId == r.FacilityId);

        // ------------------------------------------------------------------------------------------
        // Order status, derived from the items' movements (pure, unit-tested)
        // ------------------------------------------------------------------------------------------
        public static CylinderOrderStatus DeriveStatus(CylinderOrderStatus current, int quantity, int activeDispatched, int activeReceived)
        {
            if (current is CylinderOrderStatus.PlantDown or CylinderOrderStatus.Modified) return current;
            if (activeDispatched >= quantity && activeReceived >= activeDispatched) return CylinderOrderStatus.Received;
            if (activeDispatched > 0) return CylinderOrderStatus.Dispatched;
            return CylinderOrderStatus.Ordered;
        }

        public static bool CanDispatch(CylinderOrderStatus status, int quantity, int activeDispatched) =>
            status is CylinderOrderStatus.Ordered or CylinderOrderStatus.Dispatched && activeDispatched < quantity;

        public static string FormatOrderNumber(int year, long number) =>
            $"ORD-{year}-{number.ToString("D4", CultureInfo.InvariantCulture)}";

        // ------------------------------------------------------------------------------------------
        // Lookups
        // ------------------------------------------------------------------------------------------
        public async Task<List<LookupItem>> GetCylinderItemsAsync()
        {
            var categories = await _context.LookupItems.AsNoTracking()
                .Where(l => l.LookupList!.Name == LookupListNames.Category && l.RowState == RowStatus.Active)
                .ToListAsync();
            var cylinderCategoryIds = categories.Where(c => c.GetFlag(LookupFlags.CylinderProfile)).Select(c => c.Id).ToList();

            return await _context.LookupItems.AsNoTracking()
                .Where(l => l.LookupList!.Name == LookupListNames.SubCategory && l.RowState == RowStatus.Active &&
                            l.ParentId != null && cylinderCategoryIds.Contains(l.ParentId.Value))
                .OrderBy(l => l.SortIndex)
                .ToListAsync();
        }

        private Task<List<LookupItem>> GetListAsync(string listName) =>
            _context.LookupItems.AsNoTracking()
                .Where(l => l.LookupList!.Name == listName && l.RowState == RowStatus.Active)
                .OrderBy(l => l.SortIndex)
                .ToListAsync();

        public Task<List<LookupItem>> GetSizesAsync() => GetListAsync(LookupListNames.CylinderSize);
        public Task<List<LookupItem>> GetContentsAsync() => GetListAsync(LookupListNames.CylinderContent);

        // ------------------------------------------------------------------------------------------
        // Page data
        // ------------------------------------------------------------------------------------------
        public async Task<CylinderIndexViewModel> LoadIndexAsync(User user)
        {
            bool isCms = await IsCmsUserAsync(user);
            var vm = new CylinderIndexViewModel
            {
                Items = await GetCylinderItemsAsync(),
                Sizes = await GetSizesAsync(),
                Contents = await GetContentsAsync(),
                IsAdmin = IsAdmin(user),
                IsPharmacist = IsPharmacist(user),
                IsCmsUser = isCms,
                CurrentUserId = user.UserId,
                CurrentFacilityId = user.FacilityId,
            };

            IQueryable<CylinderRequest> requests = _context.CylinderRequests.AsNoTracking()
                .Include(r => r.Facility).Include(r => r.Description).Include(r => r.Size).Include(r => r.Content).Include(r => r.Requester)
                .Where(r => r.RowState == RowStatus.Active);

            vm.FacilityRequests = await (IsAdmin(user) ? requests : requests.Where(r => r.FacilityId == user.FacilityId))
                .OrderByDescending(r => r.DateCreated).Take(100).ToListAsync();

            if (isCms)
            {
                var orderedRequestIds = _context.CylinderOrders
                    .Where(o => o.Status != CylinderOrderStatus.PlantDown && o.RowState == RowStatus.Active)
                    .Select(o => o.RequestId);
                vm.ApprovedRequests = await requests
                    .Where(r => r.Status == CylinderRequestStatus.Approved && !orderedRequestIds.Contains(r.RequestId))
                    .OrderBy(r => r.DateApproved).ToListAsync();
            }

            IQueryable<CylinderOrder> orders = _context.CylinderOrders.AsNoTracking()
                .Include(o => o.Request!).ThenInclude(r => r.Facility)
                .Include(o => o.Request!).ThenInclude(r => r.Size)
                .Include(o => o.Request!).ThenInclude(r => r.Content)
                .Include(o => o.Request!).ThenInclude(r => r.Description)
                .Include(o => o.Items).ThenInclude(i => i.Movement)
                .Where(o => o.RowState == RowStatus.Active);
            if (!isCms) orders = orders.Where(o => o.Request!.FacilityId == user.FacilityId);

            foreach (var order in await orders.OrderByDescending(o => o.DateCreated).Take(100).ToListAsync())
                vm.Orders.Add(ToRow(order, isCms));

            return vm;
        }

        public static CylinderOrderRow ToRow(CylinderOrder order, bool isCms)
        {
            var active = order.Items.Where(i => i.Movement != null && i.Movement.DateRejected == null).ToList();
            return new CylinderOrderRow
            {
                Order = order,
                Dispatched = active.Count,
                Received = active.Count(i => i.Movement!.DateReceived != null),
                BatchIds = order.Items.Where(i => i.Movement?.BatchId != null).Select(i => i.Movement!.BatchId!.Value).Distinct().ToList(),
                CanDispatch = isCms && CanDispatch(order.Status, order.Quantity, active.Count)
            };
        }

        // ------------------------------------------------------------------------------------------
        // Requests
        // ------------------------------------------------------------------------------------------
        public async Task<List<string>> SubmitRequestAsync(User user, CylinderRequestForm form)
        {
            var errors = await ValidateFormAsync(form);
            if (errors.Any()) return errors;

            CylinderRequest? request;
            if (form.RequestId != null)
            {
                request = await _context.CylinderRequests.FirstOrDefaultAsync(r => r.RequestId == form.RequestId && r.RowState == RowStatus.Active);
                if (request == null) return new List<string> { "Request not found." };
                if (request.FacilityId != user.FacilityId && !IsAdmin(user)) return new List<string> { "You can only revise your facility's requests." };
                if (request.Status != CylinderRequestStatus.Rejected && request.Status != CylinderRequestStatus.Requested)
                    return new List<string> { "Only a rejected or returned request can be revised." };
                request.ModifiedBy = user.UserId;
                request.DateModified = DateTime.Now;
            }
            else
            {
                request = new CylinderRequest
                {
                    RequestId = Guid.NewGuid(),
                    FacilityId = user.FacilityId,
                    CreatedBy = user.UserId,
                    DateCreated = DateTime.Now,
                    RowState = RowStatus.Active
                };
                _context.CylinderRequests.Add(request);
            }

            request.DescriptionId = form.DescriptionId!.Value;
            request.SizeId = form.SizeId!.Value;
            request.ContentId = form.ContentId!.Value;
            request.Quantity = form.Quantity;
            request.RequisitionName = form.RequisitionName!.Trim();
            request.RequisitionNumber = form.RequisitionNumber!.Trim();
            request.Status = CylinderRequestStatus.Requested;
            request.ApprovedBy = null;
            request.DateApproved = null;
            request.DecisionReason = null;

            await _context.SaveChangesAsync();
            await _notificationService.CreateCylinderRequestNotification(request.FacilityId, user.UserId);
            return new List<string>();
        }

        private async Task<List<string>> ValidateFormAsync(CylinderRequestForm f)
        {
            var errors = new List<string>();
            var itemIds = (await GetCylinderItemsAsync()).Select(x => x.Id).ToHashSet();
            var sizeIds = (await GetSizesAsync()).Select(x => x.Id).ToHashSet();
            var contentIds = (await GetContentsAsync()).Select(x => x.Id).ToHashSet();

            if (f.DescriptionId == null || !itemIds.Contains(f.DescriptionId.Value)) errors.Add("Choose a cylinder item.");
            if (f.SizeId == null || !sizeIds.Contains(f.SizeId.Value)) errors.Add("Choose a size.");
            if (f.ContentId == null || !contentIds.Contains(f.ContentId.Value)) errors.Add("Choose a content.");
            if (f.Quantity < 1) errors.Add("Quantity must be at least 1.");
            if (string.IsNullOrWhiteSpace(f.RequisitionNumber)) errors.Add("Requisition number is required.");
            if (string.IsNullOrWhiteSpace(f.RequisitionName)) errors.Add("Requisition name is required.");
            return errors;
        }

        public async Task<string?> DecideRequestAsync(User user, Guid requestId, bool approve, string? reason)
        {
            var request = await _context.CylinderRequests.FirstOrDefaultAsync(r => r.RequestId == requestId && r.RowState == RowStatus.Active);
            if (request == null) return "Request not found.";
            if (!CanDecide(user, request)) return "Only the facility pharmacist can approve or reject this request.";
            if (request.Status != CylinderRequestStatus.Requested) return "This request is not waiting for approval.";
            if (!approve && string.IsNullOrWhiteSpace(reason)) return "Give a reason for the rejection.";

            request.Status = approve ? CylinderRequestStatus.Approved : CylinderRequestStatus.Rejected;
            request.ApprovedBy = user.UserId;
            request.DateApproved = DateTime.Now;
            request.DecisionReason = approve ? null : reason!.Trim();
            request.ModifiedBy = user.UserId;
            request.DateModified = DateTime.Now;
            await _context.SaveChangesAsync();

            if (approve)
                await _notificationService.CreateCylinderRequestApprovedNotification(user.UserId);
            else
                await _notificationService.CreateUserNotification(request.CreatedBy,
                    $"Your cylinder request {request.RequisitionNumber} was rejected: {request.DecisionReason}", "cylinder", user.UserId);
            return null;
        }

        // ------------------------------------------------------------------------------------------
        // Orders (CMS)
        // ------------------------------------------------------------------------------------------
        public async Task<(CylinderOrder? order, string? error)> CreateOrderAsync(User user, Guid requestId, int quantity)
        {
            if (!await IsCmsUserAsync(user)) return (null, "Only a CMS officer at the central store can create orders.");
            if (quantity < 1) return (null, "Quantity must be at least 1.");

            var request = await _context.CylinderRequests.FirstOrDefaultAsync(r => r.RequestId == requestId && r.RowState == RowStatus.Active);
            if (request == null) return (null, "Request not found.");
            if (request.Status != CylinderRequestStatus.Approved) return (null, "Only an approved request can be ordered.");
            if (await _context.CylinderOrders.AnyAsync(o => o.RequestId == requestId && o.Status != CylinderOrderStatus.PlantDown && o.RowState == RowStatus.Active))
                return (null, "This request already has an order.");

            var order = new CylinderOrder
            {
                OrderId = Guid.NewGuid(),
                RequestId = requestId,
                OrderedBy = user.UserId,
                OrderNumber = FormatOrderNumber(DateTime.Today.Year, await NextOrderNumberAsync()),
                Quantity = quantity,
                Status = CylinderOrderStatus.Ordered,
                CreatedBy = user.UserId,
                DateCreated = DateTime.Now,
                RowState = RowStatus.Active
            };
            _context.CylinderOrders.Add(order);
            await _context.SaveChangesAsync();
            return (order, null);
        }

        private async Task<long> NextOrderNumberAsync()
        {
            var value = new SqlParameter("@value", SqlDbType.BigInt) { Direction = ParameterDirection.Output };
            await _context.Database.ExecuteSqlRawAsync("SET @value = NEXT VALUE FOR dbo.CylinderOrderSeq", value);
            return (long)value.Value;
        }

        public async Task<string?> ModifyOrderAsync(User user, Guid orderId, int quantity)
        {
            var (order, error) = await LoadOrderForCmsAsync(user, orderId);
            if (order == null) return error;
            int active = order.Items.Count(i => i.Movement != null && i.Movement.DateRejected == null);
            if (order.Status is CylinderOrderStatus.PlantDown or CylinderOrderStatus.Received) return "This order can no longer be changed.";
            if (quantity < 1) return "Quantity must be at least 1.";
            if (quantity < active) return $"Quantity cannot be less than the {active} already dispatched.";

            order.Quantity = quantity;
            order.Status = CylinderOrderStatus.Modified;
            Touch(order, user);
            await _context.SaveChangesAsync();
            return null;
        }

        public async Task<string?> ResubmitOrderAsync(User user, Guid orderId)
        {
            var (order, error) = await LoadOrderForCmsAsync(user, orderId);
            if (order == null) return error;
            if (order.Status != CylinderOrderStatus.Modified) return "Only a modified order can be re-submitted.";

            order.Status = CylinderOrderStatus.Ordered;
            ApplyDerivedStatus(order);
            Touch(order, user);
            await _context.SaveChangesAsync();
            return null;
        }

        // Plant down: CMS cannot fill the order; the request goes back to the facility to resubmit
        public async Task<string?> PlantDownAsync(User user, Guid orderId, string? note)
        {
            var (order, error) = await LoadOrderForCmsAsync(user, orderId);
            if (order == null) return error;
            if (order.Items.Any(i => i.Movement != null && i.Movement.DateRejected == null))
                return "Cylinders have already been dispatched on this order.";
            if (order.Status is CylinderOrderStatus.PlantDown or CylinderOrderStatus.Received) return "This order is already closed.";

            order.Status = CylinderOrderStatus.PlantDown;
            Touch(order, user);
            var request = order.Request!;
            request.Status = CylinderRequestStatus.Requested;
            request.DecisionReason = "Returned by CMS (plant down)" + (string.IsNullOrWhiteSpace(note) ? "" : ": " + note.Trim());
            request.ModifiedBy = user.UserId;
            request.DateModified = DateTime.Now;
            await _context.SaveChangesAsync();

            await _notificationService.CreateUserNotification(request.CreatedBy,
                $"Your cylinder request {request.RequisitionNumber} was returned by CMS (plant down). Please revise and resubmit.", "cylinder", user.UserId);
            return null;
        }

        private async Task<(CylinderOrder? order, string? error)> LoadOrderForCmsAsync(User user, Guid orderId)
        {
            if (!await IsCmsUserAsync(user)) return (null, "Only a CMS officer at the central store can change orders.");
            var order = await _context.CylinderOrders
                .Include(o => o.Request)
                .Include(o => o.Items).ThenInclude(i => i.Movement)
                .FirstOrDefaultAsync(o => o.OrderId == orderId && o.RowState == RowStatus.Active);
            return order == null ? (null, "Order not found.") : (order, null);
        }

        private static void Touch(CylinderOrder order, User user)
        {
            order.ModifiedBy = user.UserId;
            order.DateModified = DateTime.Now;
        }

        // ------------------------------------------------------------------------------------------
        // Dispatch support (used by MovementQueueService) and status upkeep after receive / reject
        // ------------------------------------------------------------------------------------------
        public async Task<CylinderOrder?> GetOrderForDispatchAsync(Guid orderId)
        {
            return await _context.CylinderOrders
                .Include(o => o.Request!).ThenInclude(r => r.Facility)
                .Include(o => o.Request!).ThenInclude(r => r.Size)
                .Include(o => o.Request!).ThenInclude(r => r.Content)
                .Include(o => o.Request!).ThenInclude(r => r.Description)
                .Include(o => o.Items).ThenInclude(i => i.Movement)
                .FirstOrDefaultAsync(o => o.OrderId == orderId && o.RowState == RowStatus.Active);
        }

        public static int ActiveDispatched(CylinderOrder order) =>
            order.Items.Count(i => i.Movement != null && i.Movement.DateRejected == null);

        public static void ApplyDerivedStatus(CylinderOrder order)
        {
            var active = order.Items.Where(i => i.Movement != null && i.Movement.DateRejected == null).ToList();
            order.Status = DeriveStatus(order.Status, order.Quantity, active.Count, active.Count(i => i.Movement!.DateReceived != null));
        }

        /// <summary>Recomputes the status of every order that contains one of these movements. Saved by the caller.</summary>
        public async Task RefreshOrdersForMovementsAsync(IEnumerable<Guid> movementIds)
        {
            var ids = movementIds.ToList();
            var orders = await _context.CylinderOrders
                .Include(o => o.Items).ThenInclude(i => i.Movement)
                .Where(o => o.Items.Any(i => i.MovementId != null && ids.Contains(i.MovementId.Value)))
                .ToListAsync();
            foreach (var order in orders) ApplyDerivedStatus(order);
        }
    }
}
