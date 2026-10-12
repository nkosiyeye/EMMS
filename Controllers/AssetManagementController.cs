using EMMS.Constants;
using EMMS.CustomAttributes;
using EMMS.Data;
using EMMS.Data.Repository;
using EMMS.Models;
using EMMS.Models.Entities;
using EMMS.Models.Pagination;
using EMMS.Service;
using EMMS.Utility;
using EMMS.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using static EMMS.Models.Enumerators;

namespace EMMS.Controllers
{
    public class AssetManagementController : BaseController
    {
        private readonly ApplicationDbContext _context;
        private readonly AssetService _assetService;
        private readonly SelectList procurementStatusList = new SelectList
            (Enum.GetValues(
                typeof(ProcurementStatus)).Cast<ProcurementStatus>().Select(e => new { Id = (int)e, Name = e.ToString() }),
            "Id", "Name");
        private readonly AssetManagementRepo _repo;
        private readonly AssetMovementRepo _mrepo;
        private readonly AssetTagService _tagService;

        public AssetManagementController(ApplicationDbContext context, AssetManagementRepo repo, AssetService assetService, AssetTagService tagService)
        {
            _context = context;
            _assetService = assetService;
            _repo = repo;
            _mrepo = new AssetMovementRepo(_context);
            _tagService = tagService;
        }

        [HttpGet]
        public async Task<IActionResult> GetSubCategories(int categoryId)
        {
            var subCategories = await _context.LookupItems
                .Where(x => x.LookupList.Name == "SubCategory" && x.ParentId == categoryId && x.RowState == RowStatus.Active)
                .Select(x => new { x.Id, x.Name })
                .ToListAsync();

            return Json(subCategories);
        }
        [HttpGet]
        public IActionResult GetLookupItemDetails(int id)
        {
            try
            {
                var item = _context.LookupItems.Find(id);

                if (item == null)
                {
                    return NotFound(new
                    {
                        message = "Lookup item not found"
                    });
                }

                return Json(new
                {
                    item.Id,
                    item.Name,
                    item.FlagsJson
                });
            }
            catch (Exception)
            {
                // log ex here if you have logging
                return StatusCode(500, new
                {
                    message = "An unexpected error occurred. Please reload and try again."
                });
            }
        }


        [RequireLogin]
        public async Task<IActionResult> Index()
        {
            //return View(await _assetService.GetAssetIndexViewModel(CurrentUser));
            return View(new AssetIndexViewModel());
        }

        [HttpPost]
        public IActionResult GetAssets([FromForm] DataTablesRequest request)
        {
            var currentUser = CurrentUser; // however you fetch user

            bool isAdmin = currentUser?.UserRole?.UserType == Enumerators.UserType.Administrator;
            bool isBiomed = currentUser?.UserRole?.UserType == Enumerators.UserType.Biomed;
            bool isDataCollector = currentUser?.UserRole?.UserType == Enumerators.UserType.DataCollector;

            var result = _assetService.GetPaginatedAssets(request.Start, request.Length, request.Search?.Value,CurrentUser);

            var data = result.Items.Select(asset =>
            {
                var lastMove = asset.LastMovement;

                string actionHtml = "";

                if (lastMove?.Reason == Enumerators.MovementReason.StolenorMissing)
                {
                    actionHtml = $@"
                <a class='dropdown-item' href='/AssetManagement/detail/{asset.Asset.AssetId}'>
                    <span class='badge bg-danger'>
                        <i class='fa fa-eye me-1'></i>View Asset
                    </span>
                </a>";
                }
                else
                {
                    actionHtml = $@"
            <div class='dropdown'>
                <button class='btn btn-sm btn-outline-secondary dropdown-toggle' type='button' data-bs-toggle='dropdown'>
                    <i class='fa fa-ellipsis-h'></i>
                </button>
                <ul class='dropdown-menu'>";

                    if (lastMove == null && asset.PendingMovement == null && (isAdmin || isBiomed))
                    {
                        actionHtml += $@"
                <li>
                    <a class='dropdown-item' href='/AssetManagement/edit/{asset.Asset.AssetId}'>
                        <i class='fa fa-edit me-1'></i> Edit Asset
                    </a>
                </li>";
                    }

                    actionHtml += $@"
                <li>
                    <a class='dropdown-item' href='/AssetManagement/detail/{asset.Asset.AssetId}'>
                        <i class='fa fa-eye me-1'></i> View Asset
                    </a>
                </li>";

                    if (!isDataCollector && lastMove?.Reason != Enumerators.MovementReason.Decommission)
                    {
                        actionHtml += $@"
                <li>
                    <a class='dropdown-item' href='/AssetMovement/moveAsset/{asset.Asset.AssetId}'>
                        <i class='fa fa-arrows-alt me-1'></i> Move Asset
                    </a>
                </li>

                <li>
                    <a class='dropdown-item' href='/JobManagement/workRequest/{asset.Asset.AssetId}'>
                        <i class='fa fa-toolbox me-1'></i> Work Request
                    </a>
                </li>

                <li>
                    <a class='dropdown-item' href='/JobManagement/AutomateServiceRequest/{asset.Asset.AssetId}'>
                        <i class='fa fa-toolbox me-1'></i> Service Request
                    </a>
                </li>";
                    }

                    actionHtml += $@"
                <li>
                    <button class='dropdown-item stolen-missing-btn'
                        data-id='{asset.Asset.AssetId}'
                        data-tag='{asset.Asset.AssetTagNumber}'
                        data-name='{asset.Asset.SubCategory?.Name}'
                        data-category='{asset.Asset.Category?.Name}'
                        data-serial='{asset.Asset.SerialNumber}'
                        data-model='{asset.Asset.Model}'
                        data-location='{asset.LastMovement?.Facility?.FacilityName ?? "Unknown"}'
                        type='button'>
                        <i class='fa fa-exclamation-triangle me-1 text-danger'></i> Stolen/Missing Asset
                    </button>
                </li>";

                    if (lastMove?.Reason == Enumerators.MovementReason.Decommission && isAdmin)
                    {
                        actionHtml += $@"
                <li>
                    <a href='/AssetManagement/HideAsset/{asset.Asset.AssetId}'
                       class='dropdown-item text-danger'>
                        <i class='fa fa-eye-slash me-1'></i> Hide Asset
                    </a>
                </li>";
                    }

                    actionHtml += "</ul></div>";
                }
                string statusHtml;

                // Functional status: the newest report, which is the pending movement's when one exists
                var statusSource = asset.PendingMovement ?? asset.LastMovement;
                if (statusSource != null)
                {
                    if (statusSource.Reason != Enumerators.MovementReason.Decommission)
                    {
                        statusHtml = Format.DisplayFunctionalStatus(statusSource.FunctionalStatus).ToString();
                    }
                    else
                    {
                        statusHtml = Format.DisplayProcurementStatus(ProcurementStatus.Decommissioned).ToString();
                    }
                }
                else
                {
                    statusHtml = "<span class='text-muted'>-</span>";
                }

                // Location stays the source until received; show where it is heading
                if (asset.PendingMovement != null)
                {
                    var destination = System.Net.WebUtility.HtmlEncode(
                        (asset.PendingMovement.Facility?.FacilityName ?? "?") +
                        (asset.PendingMovement.ServicePoint != null ? " / " + asset.PendingMovement.ServicePoint.Name : ""));
                    statusHtml += asset.PendingMovement.IsApproved
                        ? $" <span class='badge bg-primary' title='Approved, not yet received'>In transit &rarr; {destination}</span>"
                        : $" <span class='badge bg-warning text-dark' title='Movement awaiting approval'>Awaiting approval &rarr; {destination}</span>";
                }

                return new
                {
                    checkbox = $"<input type='checkbox' class='asset-checkbox' value='{System.Net.WebUtility.HtmlEncode(asset.Asset.AssetTagNumber)}' />",
                    tag = asset.Asset.AssetTagNumber,
                    category = asset.Asset.Category?.Name,
                    itemName = asset.Asset.SubCategory?.Name,
                    placement = asset.Asset.IsPlacement ? "Yes" : "No",
                    vendor = asset.Asset.Vendor?.Name ?? "Unknown",
                    serviceProvider = asset.Asset.ServiceProvider?.Name ?? "Unknown",
                    procurement = Format.DisplayProcurementStatus((ProcurementStatus)asset.Asset.StatusId).ToString(),
                    facility = asset.LastMovement?.Facility?.FacilityName ?? "-",
                    servicePoint = asset.LastMovement?.ServicePoint?.Name ?? "<span class='text-muted fst-italic'>Unknown</span>",
                    status = statusHtml,
                    action = actionHtml
                };
            });

            return Json(new
            {
                draw = request.Draw,
                recordsTotal = result.TotalCount,
                recordsFiltered = result.FilteredCount,
                data
            });
        }

        [RequireLogin]
        public async Task<IActionResult> Detail(Guid id)
        {
            var asset = await _context.Assets
                .AsNoTracking()
                .Include(a => a.Category)
                .Include(a => a.SubCategory)
                .Include(a => a.Department)
                .Include(a => a.Manufacturer)
                .Include(a => a.Vendor)
                .Include(a => a.ServiceProvider)
                .Include(u => u.User)
                .Include(a => a.CylinderDetail!.Size)
                .Include(a => a.CylinderDetail!.Content)
                .FirstOrDefaultAsync(a => a.AssetId == id);

            var serviceHistory = await _context.Job
                .AsNoTracking()
                .Where(j => j.AssetId == id)
                .Include(j => j.FaultReport)
                .Include(j => j.Status)
                .OrderByDescending(j => j.DateCreated)
                .ToListAsync();

            var movementHistory = await _context.AssetMovement
                .AsNoTracking()
                .Where(m => m.AssetId == id && m.RowState == RowStatus.Active)
                .Where(m => m.DateReceived != null || m.DateRejected != null)
                .Include(m => m.Facility)
                .Include(m => m.ServicePoint)
                .Include(m => m.RecievedUser)
                .Include(m => m.RejectedUser)
                .OrderByDescending(m => m.MovementDate)
                .ToListAsync();

            var vm = new AssetDetailViewModel
            {
                Asset = asset,
                ServiceHistory = serviceHistory,
                MovementHistory = movementHistory,
                CurrentLocation = (await _repo.GetCurrentLocationMovements(new[] { id })).GetValueOrDefault(id),
                PendingMovement = (await _repo.GetInTransitMovements(new[] { id })).GetValueOrDefault(id)
            };

            return View(vm);
        }




        [RequireLogin]
        [AuthorizeRole(nameof(UserType.Administrator), nameof(UserType.FacilityManager), nameof(UserType.Biomed))]
        public async Task<IActionResult> registerAsset()
        {

            var facilityCode = CurrentUser.Facility.FacilityCode;
            var asset = new Asset()
            {
                AssetTagNumber = "Tag"
            };
            var viewModel = await GetBaseAssetRegView(asset);
            viewModel.alreadyDeployed = isAdmin ? false : true;
            viewModel.dateDeployed = viewModel.alreadyDeployed ? DateTime.Now : null;
            viewModel.facilityId = isAdmin ? null : CurrentUser!.FacilityId;

            return View(viewModel);
        }
        [RequireLogin]
        [AuthorizeRole(nameof(UserType.Administrator), nameof(UserType.DataCollector), nameof(UserType.Biomed))]
        public async Task<IActionResult> quickRegisterAsset()
        {

            var facilityCode = CurrentUser.Facility.FacilityCode;
            var asset = new Asset()
            {
                AssetTagNumber = "Tag"
            };
            var viewModel = await GetBaseAssetRegView(asset);
            viewModel.alreadyDeployed = isAdmin ? false : true;
            viewModel.dateDeployed = viewModel.alreadyDeployed ? DateTime.Now : null;
            viewModel.facilityId = isAdmin ? null : CurrentUser!.FacilityId;

            return View(viewModel);
        }


        async Task<AssetRegistrationViewModel> GetBaseAssetRegView(Asset asset)
        {

            return new AssetRegistrationViewModel
            {
                asset = asset,
                Statuses = procurementStatusList,
                Categories = new List<LookupItem>(),
                Departments = new List<LookupItem>(),
                Manufacturers = new List<LookupItem>(),
                Vendors = new List<LookupItem>(),
                ServiceProviders = new List<LookupItem>(),
                UnitOfMeasures = new List<LookupItem>(),
                LifespanPeriods = new List<LookupItem>(),
                Facilities = new List<Facility>(),
                ServicePoints = new List<LookupItem>(),
                CylinderSizes = new List<LookupItem>(),
                CylinderContents = new List<LookupItem>(),
            };
        }
        [HttpGet]
        public async Task<IActionResult> GetDropdownData()
        {
            var categories = await _repo.GetCategories();
            //var subCategories = await _repo.GetSubCategories();
            var departments = await _repo.GetDepartments();
            var manufacturers = await _repo.GetManufacturers();
            var vendors = await _repo.GetVendors();
            var serviceProviders = await _repo.GetServiceProviders();
            var unitOfMeasures = await _repo.GetUnitOfMeasures();
            var lifespanPeriods = await _repo.GetLifespanPeriods();
            var facilities = await _repo.GetFacilities();
            var servicePoints = await _repo.GetServicePoints();
            var cylinderSizes = await _repo.GetCylinderSizes();
            var cylinderContents = await _repo.GetCylinderContents();

            var data = new
            {
                Categories = categories.Select(c => new { c.Id, c.Name }),
                //SubCategories = subCategories.Select(s => new { s.Id, s.Name, s.CategoryId }),
                Departments = departments.Select(d => new { d.Id, d.Name }),
                Manufacturers = manufacturers.Select(m => new { m.Id, m.Name }),
                Vendors = vendors.Select(v => new { v.Id, v.Name }),
                ServiceProviders = serviceProviders.Select(s => new { s.Id, s.Name }),
                UnitOfMeasures = unitOfMeasures.Select(u => new { u.Id, u.Name }),
                LifespanPeriods = lifespanPeriods.Select(l => new { l.Id, l.Name }),
                Facilities = facilities.Select(f => new { f.FacilityId, f.FacilityName }),
                ServicePoints = servicePoints.Select(s => new { s.Id, s.Name }),
                Statuses = procurementStatusList,
                CylinderSizes = cylinderSizes.Select(s => new { s.Id, s.Name }),
                CylinderContents = cylinderContents.Select(c => new { c.Id, c.Name }),
                CentralStoreFacilityId = await _repo.GetCentralStoreFacilityId(),
            };

            return Json(data);
        }


        [RequireLogin]
        public async Task<IActionResult> Edit(Guid id)
        {
            var assets = await _repo.GetAssetsFromDb();
            var asset = assets.FirstOrDefault(a => a.AssetId == id)!;
            asset.CylinderDetail = await _repo.GetCylinderDetail(id);
            var viewModel = await GetBaseAssetRegView(asset);
            viewModel.CylinderSizes = await _repo.GetCylinderSizes();
            viewModel.CylinderContents = await _repo.GetCylinderContents();

            return View("edit", viewModel);

        }

        [RequireLogin]
        [HttpPost]
        public async Task<IActionResult> Edit(AssetRegistrationViewModel Assetmodel)
        {
            var asset = Assetmodel.asset;

            // Cylinder detail is saved separately (insert, update or remove) so Update(asset) never touches it
            var detail = asset.CylinderDetail;
            asset.CylinderDetail = null;
            bool isCylinder = (await GetCylinderCategoryIds(new[] { asset.CategoryId })).Any();
            RemoveCylinderModelState(); // CylinderRules owns the Size/Content messages
            if (isCylinder)
            {
                var (sizeIds, contentIds) = await GetCylinderLookupIds();
                foreach (var error in CylinderRules.ValidateDetail(detail?.SizeId, detail?.ContentId, sizeIds, contentIds))
                    ModelState.AddModelError("", error);
            }

            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            TempData["Error"] = string.Join("; ", errors);
            if (ModelState.IsValid)
            {
                UpdateEntity(asset); // TBD Replace with actual user ID
                //asset.CreatedBy = User.FindFirstValue(ClaimTypes.NameIdentifier);

                _context.Update(asset);

                var existing = await _context.AssetCylinderDetails.FindAsync(asset.AssetId);
                if (isCylinder && existing == null)
                    _context.AssetCylinderDetails.Add(new AssetCylinderDetail { AssetId = asset.AssetId, SizeId = detail!.SizeId, ContentId = detail.ContentId });
                else if (isCylinder)
                {
                    existing!.SizeId = detail!.SizeId;
                    existing.ContentId = detail.ContentId;
                }
                else if (existing != null)
                    _context.AssetCylinderDetails.Remove(existing);

                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            asset.CylinderDetail = detail;
            var viewModel = await GetBaseAssetRegView(asset);
            viewModel.CylinderSizes = await _repo.GetCylinderSizes();
            viewModel.CylinderContents = await _repo.GetCylinderContents();

            return View("edit", viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> RegisterAsset(AssetRegistrationViewModel Assetmodel)
        {
            var asset = Assetmodel.asset;
            asset.SerialNumber = SerialNumber.Normalize(asset.SerialNumber)!;

            // Check for duplicate serial number
            if (asset.SerialNumber != null && await IsDuplicate(asset.SerialNumber))
            {
                ModelState.AddModelError("asset.SerialNumber", "An asset with this serial number already exists.");
            }

            // Category flag CylinderProfile is re-read here; the form's show/hide is never trusted
            RemoveCylinderModelState(); // CylinderRules owns the Size/Content messages
            if ((await GetCylinderCategoryIds(new[] { asset.CategoryId })).Any())
            {
                var (sizeIds, contentIds) = await GetCylinderLookupIds();
                foreach (var error in CylinderRules.Validate(asset.CylinderDetail?.SizeId, asset.CylinderDetail?.ContentId,
                             sizeIds, contentIds, asset.SerialNumber, Assetmodel.alreadyDeployed, Assetmodel.facilityId))
                    ModelState.AddModelError("", error);
            }
            else
            {
                asset.CylinderDetail = null;
            }

            if (!ModelState.IsValid)
            {
                // Reload lookups only if ModelState is invalid
                var viewModel = new AssetRegistrationViewModel
                {
                    asset = asset,
                    Categories = await _repo.GetCategories(),
                    SubCategories = await _repo.GetSubCategories(),
                    Departments = await _repo.GetDepartments(),
                    Manufacturers = await _repo.GetManufacturers(),
                    Vendors = await _repo.GetVendors(),
                    ServiceProviders = await _repo.GetServiceProviders(),
                    Statuses = procurementStatusList,
                    UnitOfMeasures = await _repo.GetUnitOfMeasures(),
                    LifespanPeriods = await _repo.GetLifespanPeriods(),
                    Facilities = await _mrepo.GetFacilities(),
                    ServicePoints = await _mrepo.GetServicePoints(),
                    CylinderSizes = await _repo.GetCylinderSizes(),
                    CylinderContents = await _repo.GetCylinderContents(),
                    alreadyDeployed = Assetmodel.alreadyDeployed,
                    dateDeployed = Assetmodel.dateDeployed,
                    facilityId = Assetmodel.facilityId,
                    ServicePointId = Assetmodel.ServicePointId
                };

                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                TempData["RegistrationError"] = string.Join("\n", errors);
                return View(viewModel);
            }

            // Generate AssetId
            asset.AssetId = Guid.NewGuid();
            if (asset.CylinderDetail != null) asset.CylinderDetail.AssetId = asset.AssetId;

            // Determine facility code
            var facilityCode = isAdmin && Assetmodel.facilityId.HasValue
                ? (await _context.Facilities.FirstOrDefaultAsync(f => f.FacilityId == Assetmodel.facilityId))?.FacilityCode
                : CurrentUser.Facility.FacilityCode;

            asset.AssetTagNumber = await _tagService.NextTagAsync(facilityCode);
            asset.CreatedBy = CurrentUser!.UserId;
            asset.DateCreated = DateTime.Now;
            asset.RowState = RowStatus.Active;

            // Add asset
            CreateEntity(asset);
            _context.Add(asset);

            if (Assetmodel.alreadyDeployed)
            {
                var deploymentDate = Assetmodel.dateDeployed ?? DateTime.Now;
                var facilityId = Assetmodel.facilityId ?? 0;

                var asmove = new MoveAsset
                {
                    MovementDate = deploymentDate,
                    AssetId = asset.AssetId,
                    MovementType = MovementType.Facility,
                    FromId = facilityId,
                    FacilityId = facilityId,
                    ServicePointId = Assetmodel.ServicePointId,
                    Reason = (ProcurementStatus)asset.StatusId == ProcurementStatus.Decommissioned ? MovementReason.Decommission : Assetmodel.ServicePointId == null ? MovementReason.Deployment : MovementReason.Installation,
                    FunctionalStatus = (ProcurementStatus)asset.StatusId == ProcurementStatus.Decommissioned ? FunctionalStatus.NonFunctional : FunctionalStatus.Functional,
                    IsApproved = true,
                    ApprovedBy = CurrentUser!.UserId,
                    ReceivedBy = CurrentUser!.UserId,
                    DateReceived = deploymentDate,
                    DateCreated = DateTime.Now,
                    CreatedBy = CurrentUser!.UserId
                };

                CreateEntity(asmove);
                _context.Add(asmove);

                if (asset.WarrantyEndDate != null)
                {
                    asset.WarrantyStartDate = deploymentDate;
                }
            }

            // Asset, cylinder detail and initial movement are saved together: all rows or nothing
            await _context.SaveChangesAsync();
            TempData["Success"] = "Equipment added successfully.";

            return RedirectToAction(nameof(Index));
        }
        
        [HttpPost]
        public async Task<IActionResult> SaveAssets([FromBody] List<Asset> assets)
        {
            if (assets == null || !assets.Any())
                return BadRequest("No assets were provided.");

            var currentFacilityCode = CurrentUser?.Facility?.FacilityCode;
            var currentUserId = CurrentUser?.UserId;

            if (currentFacilityCode == null || currentUserId == null)
                return Unauthorized("User context missing.");

            // Serial validation: the serial is typed or scanned, so normalise it and reject
            // missing (RequiresSerial), in-list duplicate and already-registered serials before saving anything.
            foreach (var asset in assets)
                asset.SerialNumber = SerialNumber.Normalize(asset.SerialNumber)!;

            var subCategoryIds = assets.Select(a => a.SubCategoryId).Distinct().ToList();
            var subCategories = await _context.LookupItems
                .AsNoTracking()
                .Where(l => subCategoryIds.Contains(l.Id))
                .ToDictionaryAsync(l => l.Id);

            var cylinderCategoryIds = await GetCylinderCategoryIds(assets.Select(a => a.CategoryId));
            var (sizeIds, contentIds) = cylinderCategoryIds.Any()
                ? await GetCylinderLookupIds()
                : (new HashSet<int>(), new HashSet<int>());

            var errors = new List<string>();
            for (int i = 0; i < assets.Count; i++)
            {
                if (cylinderCategoryIds.Contains(assets[i].CategoryId))
                {
                    foreach (var error in CylinderRules.Validate(assets[i].CylinderDetail?.SizeId, assets[i].CylinderDetail?.ContentId,
                                 sizeIds, contentIds, assets[i].SerialNumber, assets[i].AlreadyDeployed, assets[i].FacilityId))
                        errors.Add($"Row {i + 1}: {error}");
                    continue;
                }

                assets[i].CylinderDetail = null;
                subCategories.TryGetValue(assets[i].SubCategoryId, out var subCategory);
                // Same default as the quick registration form: required unless the item says RequiresSerial = false
                bool serialRequired = subCategory?.GetFlag(LookupFlags.RequiresSerial, true) ?? true;
                if (serialRequired && assets[i].SerialNumber == null)
                    errors.Add($"Row {i + 1}: serial number is required for {subCategory?.Name ?? "this item"}.");
            }

            foreach (var duplicate in SerialNumber.FindDuplicates(assets.Select(a => a.SerialNumber)))
                errors.Add($"Serial number {duplicate} appears more than once in this list.");

            var existingSerials = await _repo.GetExistingSerialNumbers(assets.Where(a => a.SerialNumber != null).Select(a => a.SerialNumber));
            foreach (var existing in existingSerials.Distinct(StringComparer.OrdinalIgnoreCase))
                errors.Add($"Serial number {existing} is already registered.");

            if (errors.Any())
                return BadRequest(string.Join("\n", errors));

            var facilityIds = assets.Where(a => a.FacilityId != null).Select(a => a.FacilityId!.Value).Distinct().ToList();
            var facilityCodes = await _context.Facilities
                .AsNoTracking()
                .Where(f => facilityIds.Contains(f.FacilityId))
                .ToDictionaryAsync(f => f.FacilityId, f => f.FacilityCode);

            var toInsert = new List<Asset>();
            var tagFacilityCodes = new List<string?>();
            var deployments = new List<MoveAsset>();

            foreach (var asset in assets)
            {
                // Determine quantity (default to 1 if not present or invalid)
                int qty = asset.Quantity;
                bool hasSerial = asset.SerialNumber != null;
                var facilityCode = asset.FacilityId != null && facilityCodes.TryGetValue(asset.FacilityId.Value, out var code) ? code : null;

                // An asset with a serial is always a single asset; without one, replicate for quantity
                var created = new List<Asset>();
                if (hasSerial || qty < 2)
                {
                    asset.AssetId = Guid.NewGuid();
                    if (asset.CylinderDetail != null) asset.CylinderDetail.AssetId = asset.AssetId;
                    asset.CreatedBy = currentUserId;
                    asset.DateCreated = DateTime.Now;
                    asset.RowState = RowStatus.Active;
                    created.Add(asset);
                }
                else
                {
                    for (int i = 0; i < qty; i++)
                    {
                        created.Add(new Asset
                        {
                            AssetId = Guid.NewGuid(),
                            CategoryId = asset.CategoryId,
                            SubCategoryId = asset.SubCategoryId,
                            ItemName = asset.ItemName,
                            DepartmentId = asset.DepartmentId,
                            ManufacturerId = asset.ManufacturerId,
                            Model = asset.Model,
                            VendorId = asset.VendorId,
                            ServiceProviderId = asset.ServiceProviderId,
                            StatusId = asset.StatusId,
                            ProcurementDate = asset.ProcurementDate,
                            Cost = asset.Cost,
                            LifespanQuantity = asset.LifespanQuantity,
                            IsPlacement = asset.IsPlacement,
                            PlacementStartDate = asset.PlacementStartDate,
                            PlacementEndDate = asset.PlacementEndDate,
                            IsDonated = asset.IsDonated,
                            IsServiceable = asset.IsServiceable,
                            NextServiceDate = asset.NextServiceDate,
                            ServiceInterval = asset.ServiceInterval,
                            WarrantyStartDate = asset.WarrantyStartDate,
                            WarrantyEndDate = asset.WarrantyEndDate,
                            AlreadyDeployed = asset.AlreadyDeployed,
                            DateDeployed = asset.DateDeployed,
                            FacilityId = asset.FacilityId,
                            ServicePointId = asset.ServicePointId,
                            IsIdle = asset.IsIdle,
                            CreatedBy = currentUserId,
                            DateCreated = DateTime.Now,
                            RowState = RowStatus.Active
                        });
                    }
                }

                foreach (var newAsset in created)
                {
                    toInsert.Add(newAsset);
                    tagFacilityCodes.Add(facilityCode ?? currentFacilityCode);

                    // Deployment logic (unchanged)
                    if (asset.AlreadyDeployed == true && asset.FacilityId != null)
                    {
                        var deployment = new MoveAsset
                        {
                            MovementDate = asset.DateDeployed ?? DateTime.Now,
                            AssetId = newAsset.AssetId,
                            MovementType = MovementType.Facility,
                            FromId = asset.FacilityId.Value,
                            FacilityId = asset.FacilityId.Value,
                            ServicePointId = asset.ServicePointId,
                            Reason = (ProcurementStatus)asset.StatusId == ProcurementStatus.Decommissioned ? MovementReason.Decommission : asset.ServicePointId == null ? MovementReason.Deployment : MovementReason.Installation,
                            FunctionalStatus = (ProcurementStatus)asset.StatusId == ProcurementStatus.Decommissioned ? FunctionalStatus.NonFunctional : asset.IsIdle ? FunctionalStatus.Idle : FunctionalStatus.Functional,
                            IsApproved = true,
                            ApprovedBy = currentUserId,
                            ReceivedBy = currentUserId,
                            DateReceived = asset.DateDeployed ?? DateTime.Now,
                            DateCreated = DateTime.Now,
                            CreatedBy = currentUserId
                        };
                        deployments.Add(deployment);
                    }
                }
            }

            // One sequence round trip for the whole batch
            var tags = await _tagService.NextTagsAsync(tagFacilityCodes);
            for (int i = 0; i < toInsert.Count; i++)
            {
                toInsert[i].AssetTagNumber = tags[i];
                // Items that do not require a serial keep today's behaviour: tag doubles as serial
                if (toInsert[i].SerialNumber == null) toInsert[i].SerialNumber = tags[i];
            }

            foreach (var entity in toInsert)
                CreateEntity(entity);

            await _context.AddRangeAsync(toInsert);

            if (deployments.Any())
            {
                foreach (var d in deployments)
                    CreateEntity(d);

                await _context.AddRangeAsync(deployments);
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                Message = "Assets saved successfully.",
                Count = toInsert.Count
            });
        }




        // Sticker sheet: Code 128 barcode of the asset tag, rendered server-side (works offline)
        [RequireLogin]
        [HttpPost]
        [AuthorizeRole(nameof(UserType.Administrator), nameof(UserType.Biomed))]
        public async Task<IActionResult> PrintStickers(List<string> tags)
        {
            var wanted = (tags ?? new List<string>())
                .Select(t => t?.Trim())
                .Where(t => !string.IsNullOrEmpty(t))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var assets = await _context.Assets
                .AsNoTracking()
                .Include(a => a.SubCategory)
                .Include(a => a.CylinderDetail!.Size)
                .Where(a => wanted.Contains(a.AssetTagNumber) && a.RowState == RowStatus.Active)
                .ToListAsync();

            // Keep the order the user selected them in
            var byTag = assets.ToDictionary(a => a.AssetTagNumber, StringComparer.OrdinalIgnoreCase);
            var ordered = wanted.Where(byTag.ContainsKey).Select(t => byTag[t!]).ToList();

            return View("stickers", ordered);
        }

        // Serial list pre-check for Quick Registration: returns the serials that are already registered
        [RequireLogin]
        [HttpPost]
        public async Task<IActionResult> CheckSerials([FromBody] List<string> serials)
        {
            var normalized = (serials ?? new List<string>())
                .Select(SerialNumber.Normalize)
                .Where(s => s != null)
                .Select(s => s!)
                .Take(1000)
                .ToList();

            return Json(await _repo.GetExistingSerialNumbers(normalized));
        }

        // Categories (of those given) whose FlagsJson has CylinderProfile = true
        async Task<HashSet<int>> GetCylinderCategoryIds(IEnumerable<int> categoryIds)
        {
            var ids = categoryIds.Distinct().ToList();
            var categories = await _context.LookupItems
                .AsNoTracking()
                .Where(l => ids.Contains(l.Id))
                .ToListAsync();

            return categories.Where(c => c.GetFlag(LookupFlags.CylinderProfile)).Select(c => c.Id).ToHashSet();
        }

        async Task<(HashSet<int> sizeIds, HashSet<int> contentIds)> GetCylinderLookupIds() =>
            ((await _repo.GetCylinderSizes()).Select(x => x.Id).ToHashSet(),
             (await _repo.GetCylinderContents()).Select(x => x.Id).ToHashSet());

        // Hidden cylinder inputs on a non-cylinder form must not cause binding errors
        void RemoveCylinderModelState()
        {
            foreach (var key in ModelState.Keys.Where(k => k.StartsWith("asset.CylinderDetail", StringComparison.OrdinalIgnoreCase)).ToList())
                ModelState.Remove(key);
        }

        async Task<bool> IsDuplicate(string serialNum)
        {
            Asset? asset = await _repo.GetAssetBySerialNumber(serialNum);
            if (asset != null)
                return true;

            return false;
        }

        [HttpGet]
        public async Task<IActionResult> HideAsset(Guid Id)
        {
            var asset = await _context.Assets.FirstOrDefaultAsync(a => a.AssetId == Id);
            if (asset != null)
            {
                asset.RowState = RowStatus.Inactive;
                _context.Update(asset);
                await _context.SaveChangesAsync();

            }

            return RedirectToAction(nameof(Index));
        }

    }
}
