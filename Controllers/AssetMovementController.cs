using EMMS.CustomAttributes;
using EMMS.Data.Repository;
using EMMS.Models;
using EMMS.Models.Pagination;
using EMMS.Service;
using EMMS.Utility;
using EMMS.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using static EMMS.Models.Enumerators;

namespace EMMS.Controllers
{
    public class AssetMovementController : BaseController
    {
        private readonly AssetService _assetService;
        private readonly MovementService _movementService;
        private readonly AssetMovementRepo _repo;
        private readonly MovementQueueService _queueService;
        private readonly CylinderOrderService _orderService;

        public AssetMovementController(AssetService assetService, MovementService movementService, AssetMovementRepo repo,
            MovementQueueService queueService, CylinderOrderService orderService)
        {
            _assetService = assetService;
            _movementService = movementService;
            _repo = repo;
            _queueService = queueService;
            _orderService = orderService;
        }

        // Receive Asset page (also shown again when a receive / reject form fails)
        private async Task<MoveAssetViewModel> LoadReceiveViewModel(MoveAsset? moveModel = null)
        {
            return new MoveAssetViewModel
            {
                MoveAsset = moveModel ?? new MoveAsset(),
                Conditions = await _repo.GetConditions(),
                Reasons = await _repo.GetReasons()
            };
        }

        [HttpGet]
        [RequireLogin]
        public async Task<IActionResult> Index()
        {
            var data = new MoveAssetViewModel
            {
                Conditions = await _repo.GetConditions(),
                FilterOptions = await GetFilterOptions()
            };
            return View(data);
        }

        // Full option lists for the Movement History header filters (not just the values on the current page).
        // Values are ids / enum values / fixed keys; MovementService.GetPaginatedMovements matches on them.
        private async Task<Dictionary<string, List<SelectListItem>>> GetFilterOptions()
        {
            static List<SelectListItem> FromEnum<T>() where T : struct, Enum => Enum.GetValues<T>()
                .Select(e => new SelectListItem(e.ToString(), Convert.ToInt32(e).ToString()))
                .ToList();

            var facilities = (await _repo.GetFacilities())
                .OrderBy(f => f.FacilityName)
                .Select(f => new SelectListItem(f.FacilityName, f.FacilityId.ToString()))
                .ToList();

            var servicePoints = (await _repo.GetServicePoints())
                .OrderBy(i => i.Name)
                .Select(i => new SelectListItem(i.Name, i.Id.ToString()))
                .Append(new SelectListItem("Unknown", "none"))
                .ToList();

            var options = new Dictionary<string, List<SelectListItem>>
            {
                ["ToFacility"] = facilities,
                ["ServicePoint"] = servicePoints,
                ["FunctionalStatus"] = FromEnum<FunctionalStatus>(),
                ["Reason"] = FromEnum<MovementReason>(),
                ["Approved"] = new() { new("Approved", "true"), new("Pending", "false") },
                ["Received"] = new() { new("Received", "received"), new("Rejected", "rejected"), new("Pending", "pending") }
            };

            // Non-admins only see movements leaving their own facility, so a From filter adds nothing for them
            if (CurrentUser?.UserRole?.UserType == UserType.Administrator)
                options["FromFacility"] = facilities;

            return options;
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

                // Per-column filter values (ids / enum values / fixed keys) sent by the header dropdowns
                var columnFilters = new Dictionary<string, string>();

                foreach (var col in request.Columns ?? new List<DataTablesColumn>())
                {
                    var val = col.Search?.Value;
                    if (string.IsNullOrWhiteSpace(val) || !col.Searchable || string.IsNullOrWhiteSpace(col.Name))
                        continue;

                    columnFilters[col.Name] = val.Trim();
                }

                var result = _movementService.GetPaginatedMovements(
                    request.Start,
                    request.Length,
                    request.Search?.Value?.Trim() ?? "",
                    currentUser,
                    columnFilters
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
            var facilities = await _movementService.GetFacilitiesAsync(isOffSite);
            return Json(facilities.Select(x => new { x.FacilityId, x.FacilityName }));
        }

        [HttpGet]
        public async Task<IActionResult> GetServicePoints(int facilityId)
        {
            return Json(await _movementService.GetServicePointsForFacilityAsync(facilityId));
        }

        [HttpGet]
        [RequireLogin]
        public async Task<IActionResult> MoveAsset(Guid id)
        {
            var viewModel = await _movementService.GetMoveFormAsync(id);
            if (viewModel == null)
            {
                TempData["MovementError"] = "Asset not found.";
                return RedirectToAction(nameof(Index));
            }
            return View(viewModel);
        }

        [HttpGet]
        [RequireLogin]
        public async Task<IActionResult> Edit(Guid id)
        {
            var viewModel = await _movementService.GetEditFormAsync(id);
            return viewModel == null ? NotFound() : View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(MoveRequestViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["MovementError"] = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                return RedirectToAction(nameof(Edit), new { id = model.MoveAsset?.MovementId });
            }

            await _movementService.UpdateMovementAsync(model, CurrentUser!);
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> MoveAsset(MoveRequestViewModel model)
        {
            foreach (var error in await _movementService.ValidateMoveAsync(model))
                ModelState.AddModelError("", error);

            if (!ModelState.IsValid)
            {
                TempData["Error"] = string.Join("; ", ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage));

                // model.Asset was reloaded by the validation, so the form shows it again
                await _movementService.FillFormLookupsAsync(model);
                return View("MoveAsset", model);
            }

            await _movementService.CreateMoveAsync(model, CurrentUser!);
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        [RequireLogin]
        public async Task<IActionResult> RecieveAsset()
        {
            return View(await LoadReceiveViewModel());
        }

        [HttpPost]
        public IActionResult GetPendingReceipts([FromForm] DataTablesRequest request)
        {
            var result = _movementService.GetPendingReceipts(request.Start, request.Length, request.Search?.Value, CurrentUser!);

            var data = result.Items.Select(m => new
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
                recordsTotal = result.TotalCount,
                recordsFiltered = result.FilteredCount,
                data
            });
        }

        [HttpPost]
        public IActionResult GetReceiptHistory([FromForm] DataTablesRequest request)
        {
            var result = _movementService.GetReceiptHistory(request.Start, request.Length, request.Search?.Value, CurrentUser!);

            var data = result.Items.Select(m => new
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
                recordsTotal = result.TotalCount,
                recordsFiltered = result.FilteredCount,
                data
            });
        }

        [HttpPost]
        public async Task<IActionResult> ReceiveAsset(MoveAssetViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                return View("RecieveAsset", await LoadReceiveViewModel(model.MoveAsset));
            }

            if (!await _movementService.ReceiveAsync(model.MoveAsset))
            {
                ModelState.AddModelError("", "Asset movement not found.");
                return View("RecieveAsset", await LoadReceiveViewModel(model.MoveAsset));
            }
            return RedirectToAction(nameof(RecieveAsset));
        }

        [HttpPost]
        public async Task<IActionResult> RejectAsset(MoveAssetViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                return View("RecieveAsset", await LoadReceiveViewModel(model.MoveAsset));
            }

            if (!await _movementService.RejectAsync(model.MoveAsset))
            {
                ModelState.AddModelError("", "Asset movement not found.");
                return View("RecieveAsset", await LoadReceiveViewModel(model.MoveAsset));
            }
            return RedirectToAction(nameof(RecieveAsset));
        }

        public async Task<IActionResult> ApproveMovement(Guid id)
        {
            await _movementService.ApproveAsync(id, CurrentUser!);
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> ReportStolenMissing(Guid AssetId, string DocketNumber)
        {
            var (success, message) = await _movementService.ReportStolenMissingAsync(AssetId, DocketNumber, CurrentUser!);
            TempData[success ? "Success" : "Error"] = message;
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
                ServicePoints = await _repo.GetServicePoints(),
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
            var movement = await _movementService.GetGatePassAsync(id);
            return movement == null ? NotFound() : View("GatePass", movement);
        }

        [HttpPost]
        public async Task<IActionResult> deleteMovement(Guid id)
        {
            if (!await _movementService.DeleteAsync(id))
            {
                TempData["Error"] = "Asset movement not found.";
                return RedirectToAction(nameof(Index));
            }

            TempData["MovementSuccess"] = "Asset movement deleted successfully.";
            return RedirectToAction(nameof(Index));
        }
    }

}
