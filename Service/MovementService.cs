using EMMS.Data.Repository;
using EMMS.Data;
using EMMS.Models.Admin;
using EMMS.Models.Pagination;
using EMMS.ViewModels;
using EMMS.Models;
using EMMS.Constants;
using EMMS.Models.Entities;
using EMMS.Utility;
using Microsoft.EntityFrameworkCore;
using static EMMS.Models.Enumerators;

namespace EMMS.Service
{
    public class MovementService : IMovementService
    {
        private readonly ApplicationDbContext _context;
        private readonly AssetMovementRepo _repo;
        private readonly INotificationService _notificationService;
        private readonly MovementQueueService _queueService;
        private readonly CylinderOrderService _orderService;

        public MovementService(ApplicationDbContext context, AssetMovementRepo repo, INotificationService notificationService,
            MovementQueueService queueService, CylinderOrderService orderService)
        {
            _context = context;
            _repo = repo;
            _notificationService = notificationService;
            _queueService = queueService;
            _orderService = orderService;
        }

        public PaginatedResult<MoveAsset> GetPaginatedMovements(
            int start, int length, string search, User currentUser,
            Dictionary<string, string> columnFilters = null)
        {
            var query = _context.AssetMovement
                        .Where(x => x.RowState == RowStatus.Active)
                        .Include(x => x.Asset)
                        .ThenInclude(x => x.SubCategory)
                        .Include(x => x.From)
                        .Include(x => x.Facility)
                        .Include(x => x.ServicePoint)
                        .Include(x => x.Condition)
                        .AsQueryable();

            bool isAdmin = currentUser?.UserRole?.UserType == Enumerators.UserType.Administrator;

            // If NOT admin → only show movements leaving the user's facility
            if (!isAdmin)
            {
                if (currentUser?.FacilityId == null)
                {
                    return new PaginatedResult<MoveAsset>
                    {
                        TotalCount = 0,
                        FilteredCount = 0,
                        Items = new List<MoveAsset>()
                    };
                }

                var userFacilityId = currentUser.FacilityId;

                // Narrow the query above (keeps its Includes; lazy loading is off)
                query = query.Where(x => x.FromId == userFacilityId);
            }

            int totalCount = query.Count();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();

                // Reason is stored as a number; match the search against the enum names in memory
                var matchingReasons = Enum.GetValues<MovementReason>()
                    .Where(r => r.ToString().Contains(search, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                query = query.Where(m =>
                    (m.Asset != null && (m.Asset.AssetTagNumber.Contains(search) ||
                                         (m.Asset.SubCategory != null && m.Asset.SubCategory.Name.Contains(search)))) ||
                    (m.From != null && m.From.FacilityName.Contains(search)) ||
                    (m.Facility != null && m.Facility.FacilityName.Contains(search)) ||
                    (m.ServicePoint != null && m.ServicePoint.Name.Contains(search)) ||
                    matchingReasons.Contains(m.Reason) ||
                    (m.OtherReason != null && m.OtherReason.Contains(search))
                );
            }

            // Per-column filters from the header dropdowns. Values are facility / lookup ids, enum values
            // or fixed keys (see AssetMovementController.GetFilterOptions). Applied before filteredCount and paging.
            if (columnFilters != null)
            {
                foreach (var (name, value) in columnFilters)
                {
                    int.TryParse(value, out var id);

                    switch (name)
                    {
                        case "FromFacility":
                            query = query.Where(m => m.FromId == id);
                            break;

                        case "ToFacility":
                            query = query.Where(m => m.FacilityId == id);
                            break;

                        case "ServicePoint":
                            // "none" = no service point (shown as "Unknown")
                            query = value == "none"
                                ? query.Where(m => m.ServicePointId == null)
                                : query.Where(m => m.ServicePointId == id);
                            break;

                        case "FunctionalStatus":
                            if (Enum.TryParse<FunctionalStatus>(value, out var status) && Enum.IsDefined(status))
                                query = query.Where(m => m.FunctionalStatus == status);
                            break;

                        case "Reason":
                            // "Other" covers every free-text OtherReason as well
                            if (Enum.TryParse<MovementReason>(value, out var reason) && Enum.IsDefined(reason))
                                query = query.Where(m => m.Reason == reason);
                            break;

                        case "Approved":
                            var approved = value == "true";
                            query = query.Where(m => m.IsApproved == approved);
                            break;

                        case "Received":
                            // Mirrors the Received Status column: Rejected wins over Received, else Pending
                            query = value switch
                            {
                                "rejected" => query.Where(m => m.DateRejected != null),
                                "received" => query.Where(m => m.DateRejected == null && m.DateReceived != null),
                                "pending" => query.Where(m => m.DateRejected == null && m.DateReceived == null),
                                _ => query
                            };
                            break;
                    }
                }
            }

            int filteredCount = query.Count();

            var movements = query
                .OrderByDescending(a => a.MovementDate)
                .Skip(start)
                .Take(length)
                .ToList();

            return new PaginatedResult<MoveAsset>
            {
                TotalCount = totalCount,
                FilteredCount = filteredCount,
                Items = movements
            };
        }
        // ------------------------------------------------------------------------------------------
        // Receipts (Receive Asset page tables)
        // ------------------------------------------------------------------------------------------

        // Approved movements waiting to be received or rejected at the user's facility (admins: everywhere)
        public PaginatedResult<MoveAsset> GetPendingReceipts(int start, int length, string? search, User currentUser)
        {
            var query = _context.AssetMovement
                .AsNoTracking()
                .Where(m => m.IsApproved &&
                            m.DateReceived == null &&
                            m.DateRejected == null &&
                            m.RowState == RowStatus.Active);

            if (!WorkRequestService.IsAdmin(currentUser))
            {
                var fid = currentUser.FacilityId;
                query = query.Where(m => m.FacilityId == fid);
            }

            int total = query.Count();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();

                // Reason is stored as a number; match the search against the enum names in memory
                var matchingReasons = Enum.GetValues<MovementReason>()
                    .Where(r => r.ToString().Contains(s, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                query = query.Where(m =>
                    m.Asset.AssetTagNumber.ToLower().Contains(s) ||
                    m.From.FacilityName.ToLower().Contains(s) ||
                    (m.ServicePoint != null && m.ServicePoint.Name.ToLower().Contains(s)) ||
                    matchingReasons.Contains(m.Reason)
                );
            }

            int filtered = query.Count();

            var items = query
                .Include(m => m.Asset)
                .Include(m => m.From)
                .Include(m => m.ServicePoint)
                .OrderByDescending(m => m.MovementDate)
                .Skip(start)
                .Take(length)
                .ToList();

            return new PaginatedResult<MoveAsset> { TotalCount = total, FilteredCount = filtered, Items = items };
        }

        // Movements already received or rejected at the user's facility (admins: everywhere), latest first
        public PaginatedResult<MoveAsset> GetReceiptHistory(int start, int length, string? search, User currentUser)
        {
            var query = _context.AssetMovement
                .AsNoTracking()
                .Where(m => (m.DateReceived != null || m.DateRejected != null) &&
                            m.RowState == RowStatus.Active);

            if (!WorkRequestService.IsAdmin(currentUser))
            {
                var fid = currentUser.FacilityId;
                query = query.Where(m => m.FacilityId == fid);
            }

            int total = query.Count();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(m =>
                    m.Asset.AssetTagNumber.ToLower().Contains(s) ||
                    m.From.FacilityName.ToLower().Contains(s) ||
                    (m.ServicePoint != null && m.ServicePoint.Name.ToLower().Contains(s))
                );
            }

            int filtered = query.Count();

            var items = query
                .Include(m => m.Asset)
                .Include(m => m.From)
                .Include(m => m.ServicePoint)
                .OrderByDescending(m => m.DateReceived ?? m.DateRejected ?? m.MovementDate)
                .Skip(start)
                .Take(length)
                .ToList();

            return new PaginatedResult<MoveAsset> { TotalCount = total, FilteredCount = filtered, Items = items };
        }

        // ------------------------------------------------------------------------------------------
        // Destination pickers
        // ------------------------------------------------------------------------------------------
        public Task<List<Facility>> GetFacilitiesAsync(bool isOffSite) =>
            _context.Facilities
                .AsNoTracking()
                .Where(x => (x.isOffSite ?? false) == isOffSite && x.RowState == RowStatus.Active)
                .ToListAsync();

        // Service points shared by every facility plus those belonging to this one
        public Task<List<LookupItem>> GetServicePointsForFacilityAsync(int facilityId) =>
            _context.LookupItems
                .AsNoTracking()
                .Where(x => x.LookupList.Name.ToLower().Contains("service point") && x.RowState == RowStatus.Active)
                .Where(x => x.ParentFacilityId == null || x.ParentFacilityId == facilityId)
                .ToListAsync();

        // ------------------------------------------------------------------------------------------
        // Single movement: request, edit, receive / reject, approve
        // ------------------------------------------------------------------------------------------

        public async Task FillFormLookupsAsync(MoveRequestViewModel vm)
        {
            vm.Facilities = await _repo.GetFacilities();
            vm.ServicePoints = await _repo.GetServicePoints();
            vm.Reasons = await _repo.GetReasons();
            vm.FunctionalStatuses = await _repo.GetFunctionalStatuses();
        }

        // New movement form, prefilled from the asset's last (not rejected) movement; null when the asset doesn't exist
        public async Task<MoveRequestViewModel?> GetMoveFormAsync(Guid assetId)
        {
            var moveAsset = new MoveAsset { MovementDate = DateTime.Today };
            var history = await _repo.GetLastMovement(assetId);

            if (history != null)
            {
                moveAsset.AssetId = assetId;
                moveAsset.FacilityId = history.FacilityId;
                moveAsset.MovementType = history.MovementType;
                moveAsset.FromId = history.FacilityId;
                moveAsset.Asset = history.Asset;
            }
            else
            {
                var asset = await _context.Assets
                    .AsNoTracking()
                    .Include(a => a.SubCategory)
                    .FirstOrDefaultAsync(a => a.AssetId == assetId && a.RowState == RowStatus.Active);
                if (asset == null) return null;

                moveAsset.AssetId = asset.AssetId;
                moveAsset.Asset = asset;
                moveAsset.MovementType = MovementType.Facility;
            }

            var vm = new MoveRequestViewModel { Asset = moveAsset.Asset, MoveAsset = moveAsset };
            await FillFormLookupsAsync(vm);
            return vm;
        }

        // Edit form for an active movement; null when it doesn't exist
        public async Task<MoveRequestViewModel?> GetEditFormAsync(Guid movementId)
        {
            var moveAsset = await _context.AssetMovement
                .Where(x => x.MovementId == movementId && x.RowState == RowStatus.Active)
                .Include(x => x.Asset)
                .ThenInclude(x => x.SubCategory)
                .Include(x => x.From)
                .Include(x => x.Facility)
                .Include(x => x.ServicePoint)
                .Include(x => x.Condition)
                .FirstOrDefaultAsync();
            if (moveAsset == null) return null;

            var vm = new MoveRequestViewModel { Asset = moveAsset.Asset, MoveAsset = moveAsset };
            await FillFormLookupsAsync(vm);

            if (moveAsset.Reason == MovementReason.Installation)
                vm.WarrantyEndDate = moveAsset.Asset?.WarrantyEndDate;

            return vm;
        }

        // An installation also restarts the asset's warranty from the movement date
        public async Task UpdateMovementAsync(MoveRequestViewModel model, User user)
        {
            var assetMovement = model.MoveAsset!;
            EntityStamp.Updated(assetMovement, user.UserId);
            _context.Update(assetMovement);

            if (assetMovement.Reason == MovementReason.Installation && model.WarrantyEndDate != null)
            {
                var asset = await _context.Assets.FirstOrDefaultAsync(a => a.AssetId == assetMovement.AssetId);
                if (asset != null)
                {
                    asset.WarrantyStartDate = assetMovement.MovementDate;
                    asset.WarrantyEndDate = model.WarrantyEndDate;
                    EntityStamp.Updated(asset, user.UserId);
                }
            }
            await _context.SaveChangesAsync();
        }

        // Service point flagged RequiresPatient (e.g. Home Based Care): the movement must capture the patient
        private async Task<bool> RequiresPatientAsync(MoveAsset? move)
        {
            if (move?.MovementType != MovementType.ServicePoint || move.ServicePointId == null) return false;
            var servicePoint = await _context.LookupItems.AsNoTracking().FirstOrDefaultAsync(l => l.Id == move.ServicePointId);
            return servicePoint?.GetFlag(LookupFlags.RequiresPatient) == true;
        }

        /// <summary>
        /// Checks a movement request and returns the errors (empty = valid). Reloads <c>model.Asset</c> (tracked) so
        /// the form can be shown again and <see cref="CreateMoveAsync"/> can update its warranty.
        /// </summary>
        public async Task<List<string>> ValidateMoveAsync(MoveRequestViewModel model)
        {
            var errors = new List<string>();
            var move = model.MoveAsset;
            var assetId = move?.AssetId;

            if (assetId != Guid.Empty)
            {
                model.Asset = await _context.Assets
                    .Include(a => a.SubCategory)
                    .FirstOrDefaultAsync(a => a.AssetId == assetId);
                if (model.Asset == null)
                    errors.Add("Selected asset not found.");
            }

            var existing = await _context.AssetMovement
                .OrderByDescending(m => m.DateCreated)
                .FirstOrDefaultAsync(m => m.AssetId == assetId);
            if (existing != null && existing.DateRejected == null && existing.DateReceived == null)
                errors.Add("Asset already has a movement pending.");

            var workRequest = await _context.WorkRequest
                .OrderByDescending(m => m.DateCreated)
                .FirstOrDefaultAsync(m => m.AssetId == assetId);
            if (workRequest != null && workRequest.CloseDate == null)
                errors.Add("Can't move an asset that has a work request in progress.");

            if (await RequiresPatientAsync(move))
                errors.AddRange(MovementQueueService.ValidatePatient(model.Patient));

            // Dispatch only comes from a cylinder order; Return only goes to the central store
            if (move != null)
            {
                bool toCentralStore = await _context.Facilities.AnyAsync(f => f.FacilityId == move.FacilityId && f.IsCentralStore);
                errors.AddRange(MovementQueueService.ValidateReason(move.Reason, toCentralStore, dispatch: false));
            }

            return errors;
        }

        /// <summary>Saves a validated movement request (awaiting approval) and notifies the destination.</summary>
        public async Task CreateMoveAsync(MoveRequestViewModel model, User user)
        {
            var assetMovement = model.MoveAsset!;
            bool requiresPatient = await RequiresPatientAsync(assetMovement);
            assetMovement.Patient = null;

            if (assetMovement.FromId == 0) assetMovement.FromId = user.FacilityId;

            // The form picks a date; the time is when it was submitted
            var now = DateTime.Now;
            assetMovement.MovementDate = assetMovement.MovementDate.Date
                .AddHours(now.Hour)
                .AddMinutes(now.Minute)
                .AddSeconds(now.Second);

            if (requiresPatient)
                assetMovement.PatientId = await _queueService.UpsertPatientAsync(model.Patient!, user);

            EntityStamp.Created(assetMovement, user.UserId);
            _context.Add(assetMovement);

            await _notificationService.CreateMovementRequestNotification(assetMovement.FacilityId, user.UserId);

            if (assetMovement.Reason == MovementReason.Installation && model.WarrantyEndDate != null && model.Asset != null)
            {
                model.Asset.WarrantyStartDate = assetMovement.MovementDate;
                model.Asset.WarrantyEndDate = model.WarrantyEndDate;
                EntityStamp.Updated(model.Asset, user.UserId);
                _context.Update(model.Asset);
            }

            await _context.SaveChangesAsync();
        }

        // Receiving sets where the asset now is (ApplicationDbContext applies the status); false when not found
        public async Task<bool> ReceiveAsync(MoveAsset input)
        {
            var movement = await _context.AssetMovement.FindAsync(input.MovementId);
            if (movement == null) return false;

            movement.DateReceived = input.DateReceived;
            movement.Condition = input.Condition;
            movement.ReceivedBy = input.ReceivedBy;
            movement.Remarks = input.Remarks;

            await _context.SaveChangesAsync();
            await _orderService.RefreshOrdersForMovementsAsync(new[] { movement.MovementId });
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> RejectAsync(MoveAsset input)
        {
            var movement = await _context.AssetMovement.FindAsync(input.MovementId);
            if (movement == null) return false;

            movement.DateRejected = input.DateRejected;
            movement.RejectedReasonId = input.RejectedReasonId;
            movement.RejectedBy = input.RejectedBy;
            movement.Remarks = input.Remarks;

            await _context.SaveChangesAsync();
            await _orderService.RefreshOrdersForMovementsAsync(new[] { movement.MovementId });
            await _context.SaveChangesAsync();
            return true;
        }

        // Approval sends the movement on its way and tells the destination facility to receive it
        public async Task<bool> ApproveAsync(Guid movementId, User user)
        {
            var movement = await _context.AssetMovement.FindAsync(movementId);
            if (movement == null) return false;

            movement.IsApproved = true;
            movement.ApprovedBy = user.UserId;
            EntityStamp.Updated(movement, user.UserId);

            var tag = await _context.Assets.Where(a => a.AssetId == movement.AssetId).Select(a => a.AssetTagNumber).FirstOrDefaultAsync();
            _context.Notifications.Add(new Models.Entities.Notification
            {
                Message = $"Receive Asset: {tag}",
                Type = "move",
                DateCreated = DateTime.Now,
                FacilityId = movement.FacilityId,
                RowState = RowStatus.Active
            });

            await _notificationService.CreateMovementApprovalNotification(movement.FacilityId, user.UserId);
            await _context.SaveChangesAsync();
            return true;
        }

        /// <summary>
        /// Records a stolen/missing report as a pre-approved, received movement where the asset last was.
        /// Refused while the asset has a movement pending. Returns the message to show.
        /// </summary>
        public async Task<(bool Success, string Message)> ReportStolenMissingAsync(Guid assetId, string docketNumber, User user)
        {
            var tag = await _context.Assets.Where(a => a.AssetId == assetId).Select(a => a.AssetTagNumber).FirstOrDefaultAsync();
            if (tag == null) return (false, "Asset not found.");

            var history = await _repo.GetLastMovement(assetId);
            if (history != null && history.DateRejected == null && history.DateReceived == null)
                return (false, $"Asset {tag} has a Movement Pending.");

            var movement = new MoveAsset
            {
                AssetId = assetId,
                MovementDate = DateTime.Now,
                Reason = MovementReason.StolenorMissing,
                MovementType = history?.MovementType ?? MovementType.Facility,
                FunctionalStatus = FunctionalStatus.Unknown,
                Remarks = $"Reported stolen/missing. Police Docket: {docketNumber}",
                FromId = history?.FromId ?? user.FacilityId,
                FacilityId = history?.FacilityId ?? user.FacilityId,
                ServicePointId = history?.ServicePointId,
                IsApproved = true,
                ApprovedBy = user.UserId,
                DateReceived = DateTime.Now,
                ReceivedBy = user.UserId,
            };
            EntityStamp.Created(movement, user.UserId);
            _context.AssetMovement.Add(movement);
            await _context.SaveChangesAsync();

            return (true, $"Asset {tag} reported as Stolen/Missing.");
        }

        public Task<MoveAsset?> GetGatePassAsync(Guid movementId) =>
            _context.AssetMovement
                .AsNoTracking()
                .Include(m => m.Asset)
                .Include(m => m.Facility)
                .Include(m => m.From)
                .Include(m => m.ServicePoint)
                .Include(m => m.ApprovedUser)
                .ThenInclude(m => m.UserRole)
                .Include(m => m.ApprovedUser)
                .ThenInclude(m => m.Designations)
                .FirstOrDefaultAsync(m => m.MovementId == movementId);

        public async Task<bool> DeleteAsync(Guid movementId)
        {
            var movement = await _context.AssetMovement.FindAsync(movementId);
            if (movement == null) return false;

            _context.AssetMovement.Remove(movement);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
