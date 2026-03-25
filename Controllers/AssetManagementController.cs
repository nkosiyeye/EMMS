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

        public AssetManagementController(ApplicationDbContext context, AssetManagementRepo repo, AssetService assetService)
        {
            _context = context;
            _assetService = assetService;
            _repo = repo;
            _mrepo = new AssetMovementRepo(_context);
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

                    if (lastMove == null && (isAdmin || isBiomed))
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

                if (asset.LastMovement != null)
                {
                    if (asset.LastMovement.Reason != Enumerators.MovementReason.Decommission)
                    {
                        statusHtml = Format.DisplayFunctionalStatus(asset.LastMovement.FunctionalStatus).ToString();
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

                return new
                {
                    checkbox = $"<input type='checkbox' class='asset-checkbox' value='{asset.Asset.AssetTagNumber}' />",
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
                MovementHistory = movementHistory
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
            };

            return Json(data);
        }


        [RequireLogin]
        public async Task<IActionResult> Edit(Guid id)
        {
            var assets = await _repo.GetAssetsFromDb();
            var asset = assets.FirstOrDefault(a => a.AssetId == id)!;
            var viewModel = await GetBaseAssetRegView(asset);

            return View("edit", viewModel);

        }

        [RequireLogin]
        [HttpPost]
        public async Task<IActionResult> Edit(AssetRegistrationViewModel Assetmodel)
        {
            var asset = Assetmodel.asset;
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            TempData["Error"] = string.Join("; ", errors);
            if (ModelState.IsValid)
            {
                UpdateEntity(asset); // TBD Replace with actual user ID
                //asset.CreatedBy = User.FindFirstValue(ClaimTypes.NameIdentifier);

                _context.Update(asset);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            var viewModel = await GetBaseAssetRegView(asset);

            return View("edit", viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> RegisterAsset(AssetRegistrationViewModel Assetmodel)
        {
            var asset = Assetmodel.asset;

            // Check for duplicate serial number
            if (await IsDuplicate(asset.SerialNumber))
            {
                ModelState.AddModelError("asset.SerialNumber", "An asset with this serial number already exists.");
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
            var allAssetsCount = (await _repo.GetAssetsFromDb()).Count();

            // Determine facility code
            var facilityCode = isAdmin && Assetmodel.facilityId.HasValue
                ? (await _context.Facilities.FirstOrDefaultAsync(f => f.FacilityId == Assetmodel.facilityId))?.FacilityCode
                : CurrentUser.Facility.FacilityCode;

            asset.AssetTagNumber = $"{facilityCode}AS-{(allAssetsCount + 1):D3}";
            asset.CreatedBy = CurrentUser!.UserId;
            asset.DateCreated = DateTime.Now;
            asset.RowState = RowStatus.Active;

            // Add asset
            CreateEntity(asset);
            _context.Add(asset);

            await _context.SaveChangesAsync();

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
                    _context.Update(asset);
                }


                await _context.SaveChangesAsync();
            }
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

            int existingCount = (await _repo.GetAssetsFromDb()).Count();
            int counter = existingCount + 1;

            var toInsert = new List<Asset>();
            var deployments = new List<MoveAsset>();

            foreach (var asset in assets)
            {
                // Determine quantity (default to 1 if not present or invalid)
                int qty = asset.Quantity;

                // Check if serial is required (front end should set SerialNumber to empty if not required)
                bool requiresSerial = !string.IsNullOrWhiteSpace(asset.SerialNumber) && asset.SerialNumber != asset.AssetTagNumber;

                // If serial is required, always treat as single asset
                if (requiresSerial || qty < 2)
                {
                    if (await IsDuplicate(asset.SerialNumber))
                        return BadRequest($"Duplicate serial number detected: {asset.SerialNumber}");

                    var facility = asset.FacilityId != null ? await _context.Facilities.FirstOrDefaultAsync((f) => f.FacilityId == asset.FacilityId) : null;
                    var facilityCode = facility != null ? facility.FacilityCode : null;

                    asset.AssetId = Guid.NewGuid();
                    asset.AssetTagNumber = $"{(facilityCode ?? currentFacilityCode)}AS-{counter:D3}";
                    if (string.IsNullOrEmpty(asset.SerialNumber)) asset.SerialNumber = asset.AssetTagNumber;
                    counter++;

                    asset.CreatedBy = currentUserId;
                    asset.DateCreated = DateTime.Now;
                    asset.RowState = RowStatus.Active;

                    toInsert.Add(asset);

                    // Deployment logic (unchanged)
                    if (asset.AlreadyDeployed == true && asset.FacilityId != null)
                    {
                        var deployment = new MoveAsset
                        {
                            MovementDate = asset.DateDeployed ?? DateTime.Now,
                            AssetId = asset.AssetId,
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
                else
                {
                    // For assets without serial, duplicate for quantity
                    for (int i = 0; i < qty; i++)
                    {
                        var newAsset = new Asset
                        {
                            // Copy all properties except AssetId, AssetTagNumber, SerialNumber
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
                            AlreadyDeployed = asset.AlreadyDeployed,
                            DateDeployed = asset.DateDeployed,
                            FacilityId = asset.FacilityId,
                            ServicePointId = asset.ServicePointId,
                            CreatedBy = currentUserId,
                            DateCreated = DateTime.Now,
                            RowState = RowStatus.Active
                        };

                        var facility = asset.FacilityId != null ? await _context.Facilities.FirstOrDefaultAsync((f) => f.FacilityId == asset.FacilityId) : null;
                        var facilityCode = facility != null ? facility.FacilityCode : null;

                        newAsset.AssetId = Guid.NewGuid();
                        newAsset.AssetTagNumber = $"{(facilityCode ?? currentFacilityCode)}AS-{counter:D3}";
                        newAsset.SerialNumber = newAsset.AssetTagNumber; // No serial, use tag as serial
                        counter++;

                        toInsert.Add(newAsset);

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
