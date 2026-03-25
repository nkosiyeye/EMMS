using Azure.Core;
using DocumentFormat.OpenXml.Office2010.Excel;
using DocumentFormat.OpenXml.Office2016.Excel;
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

        public AssetMovementController(ApplicationDbContext context, NotificationService notificationService, AssetService assetService, 
            AssetManagementRepo assetManagementRepo, MovementService movementService)
        {
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

                // Functional Status from last movement (or "-" if none)
                string functionalStatus = lastMove != null
                    ? Format.DisplayFunctionalStatus(lastMove.FunctionalStatus).ToString()
                    : "-";

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
