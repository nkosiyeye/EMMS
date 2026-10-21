using Azure.Core;
using DocumentFormat.OpenXml.Office2010.Excel;
using DocumentFormat.OpenXml.Office2016.Excel;
using EMMS.Constants;
using EMMS.CustomAttributes;
using EMMS.Data;
using EMMS.Data.Migrations;
using EMMS.Data.Repository;
using EMMS.Models;
using EMMS.Models.Admin;
using EMMS.Models.Entities;
using EMMS.Models.Pagination;
using EMMS.Service;
using EMMS.Utility;
using EMMS.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Drawing.Printing;
using System.Net;
using System.Threading.Tasks;
using static EMMS.Models.Enumerators;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace EMMS.Controllers
{
    public class AssetMovementController : BaseController
    {
        private readonly ApplicationDbContext _context;
        private readonly AssetService _assetService;
        private readonly MovementService _movementService;
        private readonly NotificationService _notificationService;
        private readonly AssetManagementRepo _assetManagementRepo;
        private readonly MovementQueueService _queueService;
        private readonly CylinderOrderService _orderService;

        public AssetMovementController(ApplicationDbContext context, NotificationService notificationService, AssetService assetService, 
            AssetManagementRepo assetManagementRepo, MovementService movementService, MovementQueueService queueService, CylinderOrderService orderService)
        {
            _orderService = orderService;
            _queueService = queueService;
            _context = context;
            _assetService = assetService;
            _notificationService = notificationService;
            _assetManagementRepo = assetManagementRepo;
            _movementService = movementService;
        }

        private AssetMovementRepo GetRepo() => new AssetMovementRepo(_context);

        private async Task<MoveAssetViewModel> LoadViewModel(MoveAsset? moveModel = null)
        {
            var assets = await _assetService.GetAssetIndexViewModel(CurrentUser);
            return new MoveAssetViewModel
            {
                MoveAsset = moveModel ?? new MoveAsset(),
                AssetIndex = assets
            };
        }

        [HttpGet]
        [RequireLogin]
        public async Task<IActionResult> Index()
        {
            var repo = GetRepo();
            //var data = await LoadViewModel();
            //var movements = await repo.GetAssetMovement();

            //data.MoveAssets = isAdmin
            //    ? movements
            //    : movements.Where(m => m.FromId == CurrentUser.FacilityId);
            var data = new MoveAssetViewModel();

            data.Conditions = await repo.GetConditions();
            return View(data);
        }

        [HttpPost]
        public IActionResult GetMovements([FromForm] DataTablesRequest request)
        {
            try
            {
                var currentUser = CurrentUser;
                if (currentUser == null)
                {
                    return JsonError(request);
                }

                bool isAdmin = currentUser.UserRole?.UserType == UserType.Administrator;
                bool isManager = currentUser.UserRole?.UserType == UserType.FacilityManager;

                var result = _movementService.GetPaginatedMovements(
                    request.Start,
                    request.Length,
                    request.Search?.Value?.Trim() ?? "",
                    currentUser
                );

                var rows = result.Items.Select(m => new
                {
                    tag = m.Asset?.AssetTagNumber ?? "-",
                    fromFacility = m.From?.FacilityName ?? "-",
                    toFacility = m.Facility?.FacilityName ?? "-",
                    servicePoint = m.ServicePoint?.Name ?? "Unknown",
                    dateMoved = m.MovementDate.ToShortDateString(),
                    functionalStatus = Format.DisplayFunctionalStatus(m.FunctionalStatus).ToString(),
                    reasonForMovement = m.Reason == MovementReason.Other
                                        ? (m.OtherReason ?? "Other")
                                        : m.Reason.ToString(),

                    // Return raw values — JavaScript will decide what to display
                    movementId = m.MovementId.ToString(),
                    isApproved = m.IsApproved,
                    canApprove = isAdmin || isManager,
                    dateReceived = m.DateReceived.HasValue,
                    dateRejected = m.DateRejected.HasValue,
                    batchId = m.BatchId?.ToString(),
                    // You can also return isManager/isAdmin if needed, but usually not necessary
                }).ToList();

                return Json(new
                {
                    draw = request.Draw,
                    recordsTotal = result.TotalCount,
                    recordsFiltered = result.FilteredCount,
                    data = rows
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return JsonError(request);
            }
        }
        [HttpPost]
        public IActionResult GetAssets([FromForm] DataTablesRequest request)
        {
            var currentUser = CurrentUser;
            if (currentUser == null)
            {
                return JsonError(request);
            }

            bool isAdmin = currentUser.UserRole?.UserType == UserType.Administrator;

            // Get paginated assets (filtered by user role/facility as before)
            var result = _assetService.GetPaginatedAssets(
                request.Start,
                request.Length,
                request.Search?.Value?.Trim() ?? "",
                currentUser
            );

            var data = result.Items.Select(assetVm =>
            {
                var asset = assetVm.Asset;
                var lastMove = assetVm.LastMovement;

                // Location: prefer Facility name → Service Point name → fallback
                string location = lastMove != null
                    ? (lastMove.Facility?.FacilityName
                       ?? lastMove.ServicePoint?.Name
                       ?? "N/A")
                    : "-";

                string functionalStatus = Format.DisplayFunctionalStatus(asset.FunctionalStatus).ToString();

                // Simple action: only "Move Asset" link (matching commented Razor)
                string actionHtml = $@"<a class='table-cta-btn btn btn-sm btn-primary'
                               title='Move Asset'
                               href='/AssetMovement/moveAsset/{asset.AssetId}'>
                                   <i class='fa fa-arrows-alt me-1'></i> Move Asset
                               </a>";

                return new
                {
                    tag = asset.AssetTagNumber ?? "-",
                    category = asset.Category?.Name ?? "-",
                    subCategory = asset.SubCategory?.Name ?? "-",
                    placement = asset.IsPlacement ? "Yes" : "No",
                    location,
                    functionalStatus,
                    action = actionHtml
                };
            }).ToList();

            return Json(new
            {
                draw = request.Draw,
                recordsTotal = result.TotalCount,
                recordsFiltered = result.FilteredCount,
                data
            });
        }
        

        private IActionResult JsonError(DataTablesRequest request) =>
            Json(new { draw = request?.Draw ?? 1, recordsTotal = 0, recordsFiltered = 0, data = Array.Empty<object>() });

        [HttpGet]
        public async Task<IActionResult> GetFacilites(bool isOffSite)
        {
            var facilities = await _context.Facilities
                .Where(x => (x.isOffSite ?? false) == isOffSite && x.RowState == RowStatus.Active)
                .Select(x => new { x.FacilityId, x.FacilityName })
                .ToListAsync();

            return Json(facilities);
        }
        [HttpGet]
        public async Task<IActionResult> GetServicePoints(int facilityId)
        {

            var query = _context.LookupItems
                .Include(x => x.LookupList)
                .Where(x => x.LookupList.Name.ToLower().Contains("service point") && x.RowState == RowStatus.Active);

            // Otherwise filter by facility
            var servicePoints = await query
                .Where(x =>
                    x.ParentFacilityId == null ||
                    x.ParentFacilityId == facilityId
                )
                .ToListAsync();

            return Json(servicePoints);
        }

        [HttpGet]
        [RequireLogin]
        public async Task<IActionResult> MoveAsset(Guid id)
        {
            var repo = GetRepo();
            var moveAsset = new MoveAsset();
            moveAsset.MovementDate = DateTime.Today;
            var history = await repo.GetLastMovement(id);

            if (history != null)
            {
                moveAsset.AssetId = id;
                moveAsset.FacilityId = history.FacilityId;
                moveAsset.MovementType = history.MovementType;
                moveAsset.FromId = history.FacilityId;
                moveAsset.Asset = history.Asset;
            }
            else
            {
                var asset = (await _assetManagementRepo.GetAssetsFromDb())
                    .FirstOrDefault(a => a.AssetId == id);
                if (asset == null)
                {
                    TempData["MovementError"] = "Asset not found.";
                    return RedirectToAction(nameof(Index));
                }
                moveAsset.AssetId = asset.AssetId;
                moveAsset.Asset = asset;
                moveAsset.MovementType = MovementType.Facility;
            }

            var viewModel = new MoveRequestViewModel
            {
                Asset = moveAsset.Asset,
                MoveAsset = moveAsset,
                Facilities = await repo.GetFacilities(),
                ServicePoints = await repo.GetServicePoints(),
                Reasons = await repo.GetReasons(),
                FunctionalStatuses = await repo.GetFunctionalStatuses()
            };

            return View(viewModel);
        }

        [HttpGet]
        [RequireLogin]
        public async Task<IActionResult> Edit(Guid id)
        {
            var repo = GetRepo();
            var moveAsset = (await repo.GetAssetMovement()).FirstOrDefault(m => m.MovementId == id);

            var viewModel = new MoveRequestViewModel
            {
                Asset = moveAsset.Asset,
                MoveAsset = moveAsset,
                Facilities = await repo.GetFacilities(),
                ServicePoints = await repo.GetServicePoints(),
                Reasons = await repo.GetReasons(),
                FunctionalStatuses = await repo.GetFunctionalStatuses()
            };

            if (moveAsset.Reason == MovementReason.Installation)
            {
                var asset = await _context.Assets.FirstOrDefaultAsync(a => a.AssetId == moveAsset.AssetId);
                viewModel.WarrantyEndDate = asset.WarrantyEndDate;
            }

            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(MoveRequestViewModel model)
        {
            var assetMovement = model.MoveAsset;
            if (!ModelState.IsValid)
            {
                TempData["MovementError"] = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                return RedirectToAction(nameof(Edit), new { id = model.MoveAsset.MovementId });
            }

            UpdateEntity(assetMovement);
            _context.Update(assetMovement);

            if (assetMovement.Reason == MovementReason.Installation && model.WarrantyEndDate != null)
            {
                var asset = await _context.Assets.FirstOrDefaultAsync(a => a.AssetId == assetMovement.AssetId);
                if (asset != null)
                {
                    asset.WarrantyStartDate = assetMovement?.MovementDate ?? DateTime.Today;
                    asset.WarrantyEndDate = model?.WarrantyEndDate ?? DateTime.Today;
                    UpdateEntity(asset);
                    _context.Update(asset);
                }
                else
                {
                    ModelState.AddModelError("", "Asset is null Reselect Asset");
                }
            }
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        [HttpPost]
        public async Task<IActionResult> MoveAsset(MoveRequestViewModel model)
        {
            var repo = GetRepo(); // assuming this returns your repository

            // 1. Reload the asset (critical!)
            if (model.MoveAsset?.AssetId != Guid.Empty)
            {
                model.Asset = await _context.Assets
                    .Include(a => a.SubCategory)
                    .FirstOrDefaultAsync(a => a.AssetId == model.MoveAsset.AssetId);

                if (model.Asset == null)
                {
                    ModelState.AddModelError("", "Selected asset not found.");
                }
            }

            // 2. Existing validation checks
            var existing = _context.AssetMovement
                .OrderByDescending(m => m.DateCreated)
                .FirstOrDefault(m => m.AssetId == model.MoveAsset.AssetId);

            if (existing != null && existing.DateRejected == null && existing.DateReceived == null)
            {
                ModelState.AddModelError("", "Asset already has a movement pending.");
            }

            var workrequest = _context.WorkRequest
                .OrderByDescending(m => m.DateCreated)
                .FirstOrDefault(m => m.AssetId == model.MoveAsset.AssetId);

            if (workrequest != null && workrequest.CloseDate == null)
            {
                ModelState.AddModelError("", "Can't move an asset that has a work request in progress.");
            }

            // Service point flagged RequiresPatient (e.g. Home Based Care): capture the patient
            bool requiresPatient = false;
            if (model.MoveAsset?.MovementType == MovementType.ServicePoint && model.MoveAsset.ServicePointId != null)
            {
                var servicePoint = await _context.LookupItems.AsNoTracking().FirstOrDefaultAsync(l => l.Id == model.MoveAsset.ServicePointId);
                requiresPatient = servicePoint?.GetFlag(LookupFlags.RequiresPatient) == true;
                if (requiresPatient)
                    foreach (var error in MovementQueueService.ValidatePatient(model.Patient))
                        ModelState.AddModelError("", error);
            }
            if (model.MoveAsset != null) model.MoveAsset.Patient = null;

            // Dispatch only comes from a cylinder order; Return only goes to the central store
            if (model.MoveAsset != null)
            {
                bool toCentralStore = await _context.Facilities.AnyAsync(f => f.FacilityId == model.MoveAsset.FacilityId && f.IsCentralStore);
                foreach (var error in MovementQueueService.ValidateReason(model.MoveAsset.Reason, toCentralStore, dispatch: false))
                    ModelState.AddModelError("", error);
            }

            // 3. If still invalid → repopulate everything and return view
            if (!ModelState.IsValid)
            {
                TempData["Error"] = string.Join("; ", ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage));

                // Repopulate dropdowns
                model.Facilities = await repo.GetFacilities();
                model.ServicePoints = await repo.GetServicePoints();
                model.Reasons = await repo.GetReasons();
                model.FunctionalStatuses = await repo.GetFunctionalStatuses();

                // VERY IMPORTANT: keep the asset loaded
                // (already done above if AssetId was present)

                return View("MoveAsset", model);
            }

            // ── Success path ───────────────────────────────────────────────────────
            var assetMovement = model.MoveAsset;

            assetMovement.FromId = (assetMovement?.FromId != null && assetMovement?.FromId != 0) ? assetMovement.FromId : CurrentUser!.FacilityId;

            assetMovement.MovementDate = assetMovement.MovementDate.Date
                .AddHours(DateTime.Now.Hour)
                .AddMinutes(DateTime.Now.Minute)
                .AddSeconds(DateTime.Now.Second);

            if (requiresPatient)
                assetMovement.PatientId = await _queueService.UpsertPatientAsync(model.Patient!, CurrentUser!);

            CreateEntity(assetMovement);
            _context.Add(assetMovement);

            await _notificationService.CreateMovementRequestNotification(
                assetMovement.FacilityId,
                CurrentUser.UserId
            );

            if (assetMovement.Reason == MovementReason.Installation && model.WarrantyEndDate != null)
            {
                var asset = model.Asset; // now safe because we reloaded it
                if (asset != null)
                {
                    asset.WarrantyStartDate = assetMovement.MovementDate;
                    asset.WarrantyEndDate = model.WarrantyEndDate;
                    UpdateEntity(asset);
                    _context.Update(asset);
                }
            }

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        [RequireLogin]
        public async Task<IActionResult> RecieveAsset()
        {
            var repo = GetRepo();
            var data = new MoveAssetViewModel();
            //var data = await LoadViewModel();
            //var movements = await repo.GetAssetMovement();

            //data.MoveAssets = isAdmin
            //    ? movements
            //    : movements.Where(m => m.FacilityId == CurrentUser.FacilityId);

            data.Conditions = await repo.GetConditions();
            data.Reasons = await repo.GetReasons();
            return View(data);
        }
        [HttpPost]
        public IActionResult GetPendingReceipts([FromForm] DataTablesRequest request)
        {
            var currentUser = CurrentUser;
            if (currentUser == null) return JsonError(request);

            var query = _context.AssetMovement
                .AsNoTracking()
                .Where(m => m.IsApproved &&
                            m.DateReceived == null &&
                            m.DateRejected == null &&
                            m.RowState == RowStatus.Active);

            if (!isAdmin)
            {
                var fid = currentUser.FacilityId;
                query = query.Where(m => m.FacilityId == fid);
            }

            int total = query.Count();

            if (!string.IsNullOrWhiteSpace(request.Search?.Value))
            {
                var s = request.Search.Value.Trim().ToLower();
                query = query.Where(m =>
                    m.Asset.AssetTagNumber.ToLower().Contains(s) ||
                    m.From.FacilityName.ToLower().Contains(s) ||
                    (m.ServicePoint != null && m.ServicePoint.Name.ToLower().Contains(s)) ||
                    m.Reason.ToString().ToLower().Contains(s)
                );
            }

            int filtered = query.Count();

            var items = query
                .Include(m => m.Asset)
                .Include(m => m.From)
                .Include(m => m.ServicePoint)
                .OrderByDescending(m => m.MovementDate)
                .Skip(request.Start)
                .Take(request.Length)
                .ToList();

            var data = items.Select(m => new
            {
                assetTag = m.Asset?.AssetTagNumber ?? "-",
                fromFacility = m.From?.FacilityName ?? "-",
                servicePoint = m.ServicePoint?.Name ?? "N/A",
                dateSent = m.MovementDate.ToString("dd MMM yyyy"),
                funcStatus = Format.DisplayFunctionalStatus(m.FunctionalStatus).ToString(),
                reason = m.Reason.ToString(),
                movementId = m.MovementId.ToString(),
                action = "" // we use buttons with data attributes
            }).ToList();

            return Json(new
            {
                draw = request.Draw,
                recordsTotal = total,
                recordsFiltered = filtered,
                data
            });
        }

        [HttpPost]
        public IActionResult GetReceiptHistory([FromForm] DataTablesRequest request)
        {
            var currentUser = CurrentUser;
            if (currentUser == null) return JsonError(request);

            var query = _context.AssetMovement
                .AsNoTracking()
                .Where(m => (m.DateReceived != null || m.DateRejected != null) &&
                            m.RowState == RowStatus.Active);

            if (!isAdmin)
            {
                var fid = currentUser.FacilityId;
                query = query.Where(m => m.FacilityId == fid);
            }

            int total = query.Count();

            if (!string.IsNullOrWhiteSpace(request.Search?.Value))
            {
                var s = request.Search.Value.Trim().ToLower();
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
                .Skip(request.Start)
                .Take(request.Length)
                .ToList();

            var data = items.Select(m => new
            {
                assetTag = m.Asset?.AssetTagNumber ?? "-",
                fromFacility = m.From?.FacilityName ?? "-",
                servicePoint = m.ServicePoint?.Name ?? "N/A",
                dateReceived = m.DateReceived.HasValue ? $"<span class='badge bg-success'>{m.DateReceived.Value:dd MMM yyyy}</span>" : "<span class='badge bg-secondary'>Not Received</span>",
                dateRejected = m.DateRejected.HasValue ? $"<span class='badge bg-danger'>{m.DateRejected.Value:dd MMM yyyy}</span>" : "<span class='badge bg-secondary'>Not Rejected</span>",
                funcStatus = Format.DisplayFunctionalStatus(m.FunctionalStatus).ToString()
            }).ToList();

            return Json(new
            {
                draw = request.Draw,
                recordsTotal = total,
                recordsFiltered = filtered,
                data
            });
        }

        [HttpPost]
        public async Task<IActionResult> ReceiveAsset(MoveAssetViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                return View("RecieveAsset", await LoadViewModel(model.MoveAsset));
            }

            var movement = await _context.AssetMovement.FindAsync(model.MoveAsset.MovementId);
            if (movement == null)
            {
                ModelState.AddModelError("", "Asset movement not found.");
                return View("RecieveAsset", model);
            }

            movement.DateReceived = model.MoveAsset.DateReceived;
            movement.Condition = model.MoveAsset.Condition;
            movement.ReceivedBy = model.MoveAsset.ReceivedBy;
            movement.Remarks = model.MoveAsset.Remarks;

            _context.Update(movement);
            await _context.SaveChangesAsync();
            await _orderService.RefreshOrdersForMovementsAsync(new[] { movement.MovementId });
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(RecieveAsset));
        }

        [HttpPost]
        public async Task<IActionResult> RejectAsset(MoveAssetViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                return View("RecieveAsset", await LoadViewModel(model.MoveAsset));
            }

            var movement = await _context.AssetMovement.FindAsync(model.MoveAsset.MovementId);
            if (movement == null)
            {
                ModelState.AddModelError("", "Asset movement not found.");
                return View("RecieveAsset", model);
            }

            movement.DateRejected = model.MoveAsset.DateRejected;
            movement.RejectedReasonId = model.MoveAsset.RejectedReasonId;
            movement.RejectedBy = model.MoveAsset.RejectedBy;
            movement.Remarks = model.MoveAsset.Remarks;

            _context.Update(movement);
            await _context.SaveChangesAsync();
            await _orderService.RefreshOrdersForMovementsAsync(new[] { movement.MovementId });
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(RecieveAsset));
        }

        public async Task<IActionResult> ApproveMovement(Guid id)
        {
            var movement = await _context.AssetMovement.FindAsync(id);
            if (movement == null) return RedirectToAction(nameof(Index));

            movement.IsApproved = true;
            movement.ApprovedBy = CurrentUser.UserId;
            UpdateEntity(movement);

            var asset = await _context.Assets.FindAsync(movement.AssetId);
            var notification = new Models.Entities.Notification
            {
                Message = $"Receive Asset: {asset?.AssetTagNumber}",
                Type = "move",
                DateCreated = DateTime.Now,
                FacilityId = movement.FacilityId,
                RowState = RowStatus.Active
            };

            _context.Notifications.Add(notification);
            _context.Update(movement);

            await _notificationService.CreateMovementApprovalNotification(movement.FacilityId, CurrentUser.UserId);

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        [HttpPost]
        public async Task<IActionResult> ReportStolenMissing(Guid AssetId, string DocketNumber)
        {
            var asset = await _context.Assets.FindAsync(AssetId);
            if (asset == null)
            {
                TempData["Error"] = "Asset not found.";
                return RedirectToAction("Index", "AssetManagement");
            }
            var repo = GetRepo();
            var history = await repo.GetLastMovement(AssetId);
            if (history != null && history.DateRejected == null && history.DateReceived == null)
            {


                TempData["Error"] = $"Asset {asset.AssetTagNumber} has a Movement Pending.";
            }
            else
            {

                //if (history != null)
                //{
                //    moveAsset.AssetId = id;
                //    moveAsset.FacilityId = history.FacilityId;
                //    moveAsset.MovementType = history.MovementType;
                //    moveAsset.FromId = history.FacilityId;
                //    moveAsset.Asset = history.Asset;
                //}

                var movement = new MoveAsset
                {
                    AssetId = AssetId,
                    MovementDate = DateTime.Now,
                    Reason = MovementReason.StolenorMissing, // <-- Enum
                    MovementType = history?.MovementType ?? MovementType.Facility, // <-- Enum
                    FunctionalStatus = FunctionalStatus.Unknown,
                    Remarks = $"Reported stolen/missing. Police Docket: {DocketNumber}",
                    FromId = history?.FromId ?? CurrentUser.FacilityId,
                    FacilityId = history?.FacilityId ?? CurrentUser.FacilityId,
                    ServicePointId = history?.ServicePointId,
                    IsApproved = true,
                    ApprovedBy = CurrentUser.UserId,
                    DateReceived = DateTime.Now,
                    ReceivedBy = CurrentUser.UserId,

                };

                CreateEntity(movement);
                _context.AssetMovement.Add(movement);

                await _context.SaveChangesAsync();

                TempData["Success"] = $"Asset {asset.AssetTagNumber} reported as Stolen/Missing.";

            }
            return RedirectToAction("Index", "AssetManagement");
        }


        // ------------------------------------------------------------------------------------------
        // Movement Queue: scan or tick assets, choose the destination once, submit one batch
        // ------------------------------------------------------------------------------------------
        [HttpGet]
        [RequireLogin]
        [AuthorizeRole(nameof(UserType.Administrator), nameof(UserType.FacilityManager), nameof(UserType.Biomed), nameof(UserType.GeneralUser), nameof(UserType.Pharmacist), nameof(UserType.CmsOfficer))]
        public async Task<IActionResult> Queue(Guid? orderId)
        {
            var (order, error) = await LoadDispatchOrder(orderId);
            if (error != null)
            {
                TempData["CylinderError"] = error;
                return RedirectToAction("Index", "Cylinder");
            }
            return View("Queue", await LoadQueueViewModel(new BatchMoveRequest(), order));
        }

        private async Task<MovementQueueViewModel> LoadQueueViewModel(BatchMoveRequest request, CylinderOrder? order = null)
        {
            return new MovementQueueViewModel
            {
                Items = await _queueService.GetQueueAsync(CurrentUser!.UserId),
                Request = request,
                ServicePoints = await GetRepo().GetServicePoints(),
                Dispatch = order == null ? null : CylinderOrderService.ToRow(order, isCms: true)
            };
        }

        // Dispatch mode: the order must be dispatchable and the user a CMS officer at the central store
        private async Task<(CylinderOrder? order, string? error)> LoadDispatchOrder(Guid? orderId)
        {
            if (orderId == null) return (null, null);
            if (!await _orderService.IsCmsUserAsync(CurrentUser)) return (null, "Only a CMS officer at the central store can dispatch orders.");
            var order = await _orderService.GetOrderForDispatchAsync(orderId.Value);
            if (order == null) return (null, "Order not found.");
            if (!CylinderOrderService.CanDispatch(order.Status, order.Quantity, CylinderOrderService.ActiveDispatched(order)))
                return (null, $"Order {order.OrderNumber} cannot be dispatched ({order.Status}).");
            return (order, null);
        }

        [HttpPost]
        [RequireLogin]
        [AuthorizeRole(nameof(UserType.Administrator), nameof(UserType.FacilityManager), nameof(UserType.Biomed), nameof(UserType.GeneralUser), nameof(UserType.Pharmacist), nameof(UserType.CmsOfficer))]
        public async Task<IActionResult> QueueScan([FromForm] string? tag, [FromForm] Guid? orderId)
        {
            var (order, error) = await LoadDispatchOrder(orderId);
            if (error != null) return Json(new { code = "Error", message = error, item = (object?)null, queueCount = 0 });
            var result = await _queueService.ScanAsync(tag, CurrentUser!, order);
            return Json(new { code = result.Code.ToString(), result.Message, result.Item, result.QueueCount });
        }

        // "Move selected" from the asset grid: same checks as scanning, for facilities without a scanner
        [HttpPost]
        [RequireLogin]
        [AuthorizeRole(nameof(UserType.Administrator), nameof(UserType.FacilityManager), nameof(UserType.Biomed), nameof(UserType.GeneralUser), nameof(UserType.Pharmacist), nameof(UserType.CmsOfficer))]
        public async Task<IActionResult> QueueAddSelected(List<string> tags)
        {
            var results = await _queueService.AddManyAsync(tags ?? new List<string>(), CurrentUser!);
            var added = results.Count(r => r.Code == QueueScanResult.Added);
            TempData["QueueInfo"] = $"{added} of {results.Count} selected asset(s) added to the queue.";
            var rejected = results.Where(r => r.Code != QueueScanResult.Added).Select(r => r.Message).ToList();
            if (rejected.Any()) TempData["QueueErrors"] = string.Join("\n", rejected);
            return RedirectToAction(nameof(Queue));
        }

        [HttpPost]
        [RequireLogin]
        public async Task<IActionResult> QueueRemove(Guid id)
        {
            var count = await _queueService.RemoveAsync(CurrentUser!.UserId, id);
            return Json(new { queueCount = count });
        }

        [HttpPost]
        [RequireLogin]
        public async Task<IActionResult> QueueClear()
        {
            await _queueService.ClearAsync(CurrentUser!.UserId);
            return RedirectToAction(nameof(Queue));
        }

        [HttpPost]
        [RequireLogin]
        [AuthorizeRole(nameof(UserType.Administrator), nameof(UserType.FacilityManager), nameof(UserType.Biomed), nameof(UserType.GeneralUser), nameof(UserType.Pharmacist), nameof(UserType.CmsOfficer))]
        public async Task<IActionResult> QueueSubmit(MovementQueueViewModel model, Guid? orderId)
        {
            var request = model.Request ?? new BatchMoveRequest();
            var (order, error) = await LoadDispatchOrder(orderId);
            if (error != null)
            {
                TempData["CylinderError"] = error;
                return RedirectToAction("Index", "Cylinder");
            }

            QueueSubmitResult result;
            try
            {
                result = await _queueService.SubmitAsync(CurrentUser!, request, order);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                result = new QueueSubmitResult { Errors = { "The batch could not be saved. Nothing was moved and the queue is unchanged." } };
            }

            if (!result.Success)
            {
                TempData["QueueErrors"] = string.Join("\n", result.Errors);
                if (order != null) order = await _orderService.GetOrderForDispatchAsync(order.OrderId);
                return View("Queue", await LoadQueueViewModel(request, order));
            }

            TempData["Success"] = order != null
                ? $"Order {order.OrderNumber}: {result.Count} cylinder(s) dispatched (pre-approved)."
                : $"Batch submitted: {result.Count} movement(s) awaiting approval.";
            return RedirectToAction(nameof(Batch), new { id = result.BatchId });
        }

        [HttpGet]
        [RequireLogin]
        public async Task<IActionResult> Batch(Guid id)
        {
            var movements = await _queueService.GetBatchAsync(id);
            if (movements.Count == 0) return NotFound();

            var userType = CurrentUser?.UserRole?.UserType;
            var vm = new BatchViewModel
            {
                BatchId = id,
                Movements = movements,
                CanApprove = userType == UserType.Administrator || userType == UserType.FacilityManager,
                // Patient details only for admins and the two facilities involved
                CanSeePatient = isAdmin || movements.Any(m => m.FromId == CurrentUser?.FacilityId || m.FacilityId == CurrentUser?.FacilityId)
            };
            return View("Batch", vm);
        }

        [HttpPost]
        [RequireLogin]
        [AuthorizeRole(nameof(UserType.Administrator), nameof(UserType.FacilityManager))]
        public async Task<IActionResult> ApproveBatch(Guid id)
        {
            var count = await _queueService.ApproveBatchAsync(id, CurrentUser!);
            TempData["Success"] = $"{count} movement(s) approved.";
            return RedirectToAction(nameof(Batch), new { id });
        }

        [HttpGet]
        [RequireLogin]
        public async Task<IActionResult> BatchGatePass(Guid id)
        {
            var movements = (await _queueService.GetBatchAsync(id)).Where(m => m.IsApproved && m.DateRejected == null).ToList();
            return movements.Count == 0 ? NotFound() : View("BatchGatePass", movements);
        }

        // ------------------------------------------------------------------------------------------
        // Scan to Receive (batch)
        // ------------------------------------------------------------------------------------------
        [HttpGet]
        [RequireLogin]
        public async Task<IActionResult> IncomingBatches()
        {
            return Json(await _queueService.GetIncomingBatchesAsync(CurrentUser!));
        }

        [HttpGet]
        [RequireLogin]
        public async Task<IActionResult> BatchReceiveItems(Guid id)
        {
            return Json(await _queueService.GetBatchForReceiveAsync(id, CurrentUser!));
        }

        public class ReceiveBatchRequest
        {
            public Guid BatchId { get; set; }
            public List<Guid> MovementIds { get; set; } = new();
            public int? ConditionId { get; set; }
            public string? Remarks { get; set; }
        }

        [HttpPost]
        [RequireLogin]
        public async Task<IActionResult> ReceiveBatch([FromBody] ReceiveBatchRequest? request)
        {
            if (request == null || request.BatchId == Guid.Empty)
                return BadRequest(new { errors = new[] { "Choose a batch and scan at least one asset." } });
            try
            {
                var result = await _queueService.ReceiveBatchAsync(CurrentUser!, request.BatchId, request.MovementIds, request.ConditionId, request.Remarks);
                return result.Errors.Any() ? BadRequest(new { errors = result.Errors }) : Json(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return BadRequest(new { errors = new[] { "The receipt could not be saved. Nothing was changed." } });
            }
        }

        [HttpGet]
        [RequireLogin]
        public async Task<IActionResult> GatePass(Guid id)
        {
            var movement = await _context.AssetMovement
                .Include(m => m.Asset)
                .Include(m => m.Facility)
                .Include(m => m.From)
                .Include(m => m.ServicePoint)
                .Include(m => m.ApprovedUser)
                .ThenInclude(m => m.UserRole)
                .Include(m => m.ApprovedUser)
                .ThenInclude(m => m.Designations)
                .FirstOrDefaultAsync(m => m.MovementId == id);

            return movement == null ? NotFound() : View("GatePass", movement);
        }

        [HttpPost]
        //[ValidateAntiForgeryToken]
        //[RequireLogin]
        public async Task<IActionResult> deleteMovement(Guid id)
        {
            var movement = await _context.AssetMovement.FindAsync(id);
            if (movement == null)
            {
                TempData["Error"] = "Asset movement not found.";
                return RedirectToAction(nameof(Index));
            }

            _context.AssetMovement.Remove(movement);
            await _context.SaveChangesAsync();
            TempData["MovementSuccess"] = "Asset movement deleted successfully.";
            return RedirectToAction(nameof(Index));
        }
    }

}
