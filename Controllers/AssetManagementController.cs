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
        private readonly AssetService _assetService;
        private readonly AssetRegistrationService _registration;
        private readonly SelectList procurementStatusList = new SelectList
            (Enum.GetValues(
                typeof(ProcurementStatus)).Cast<ProcurementStatus>().Select(e => new { Id = (int)e, Name = e.ToString() }),
            "Id", "Name");
        private readonly AssetManagementRepo _repo;
        private readonly AssetMovementRepo _mrepo;
        private readonly FacilityAccess _access;

        public AssetManagementController(AssetManagementRepo repo, AssetMovementRepo mrepo, AssetService assetService, AssetRegistrationService registration, FacilityAccess access)
        {
            _access = access;
            _assetService = assetService;
            _registration = registration;
            _repo = repo;
            _mrepo = mrepo;
        }

        [HttpGet]
        [NoFacilityData("Sub-category lookup")]
        public async Task<IActionResult> GetSubCategories(int categoryId)
        {
            var subCategories = await _repo.GetSubCategoriesOf(categoryId);
            return Json(subCategories.Select(x => new { x.Id, x.Name }));
        }
        [HttpGet]
        [NoFacilityData("Lookup item")]
        public async Task<IActionResult> GetLookupItemDetails(int id)
        {
            try
            {
                var item = await _repo.GetLookupItemAsync(id);

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
        [FacilityChecked("AssetService.GetAssetIndexViewModel: non-admins see their facility")]
        public async Task<IActionResult> Index()
        {
            return View(new AssetIndexViewModel { FilterOptions = await GetFilterOptions() });
        }

        // Full option lists for the table's header filters (not just the values on the current page).
        // Values are ids / enum values; AssetService.GetPaginatedAssets matches on them.
        private async Task<Dictionary<string, List<SelectListItem>>> GetFilterOptions()
        {
            static List<SelectListItem> ToItems(IEnumerable<LookupItem> items) => items
                .OrderBy(i => i.Name)
                .Select(i => new SelectListItem(i.Name, i.Id.ToString()))
                .ToList();

            var options = new Dictionary<string, List<SelectListItem>>
            {
                ["Category"] = ToItems(await _repo.GetCategories()),
                ["ItemName"] = ToItems(await _repo.GetSubCategories()),
                ["Placement"] = new() { new("Yes", "true"), new("No", "false") },
                ["Vendor"] = ToItems(await _repo.GetVendors()),
                ["Service Provider"] = ToItems(await _repo.GetServiceProviders()),
                ["Procurement"] = Enum.GetValues<ProcurementStatus>()
                    .Select(e => new SelectListItem(e.ToString(), ((int)e).ToString())).ToList(),
                ["ServicePoint"] = ToItems(await _repo.GetServicePoints()),
                ["Status"] = Enum.GetValues<FunctionalStatus>()
                    .Select(e => new SelectListItem(e.ToString(), ((int)e).ToString()))
                    .Append(new SelectListItem("Out of service", AssetService.OutOfServiceFilter))
                    .Append(new SelectListItem("Decommissioned", "decommissioned"))
                    .Append(new SelectListItem("Awaiting approval", "awaiting"))
                    .Append(new SelectListItem("In transit", "intransit"))
                    .ToList()
            };

            // Non-admins only ever see assets at their own facility, so a facility filter adds nothing for them
            if (CurrentUser?.UserRole?.UserType == Enumerators.UserType.Administrator)
            {
                options["Facility"] = (await _repo.GetActiveFacilitiesAsync())
                    .Select(f => new SelectListItem(f.FacilityName, f.FacilityId.ToString()))
                    .ToList();
            }

            return options;
        }

        [HttpPost]
        [FacilityChecked("AssetService.GetPaginatedAssets: non-admins see their facility")]
        public IActionResult GetAssets([FromForm] DataTablesRequest request)
        {
            var currentUser = CurrentUser;

            bool isAdmin = currentUser?.UserRole?.UserType == Enumerators.UserType.Administrator;
            bool isBiomed = currentUser?.UserRole?.UserType == Enumerators.UserType.Biomed;
            bool isDataCollector = currentUser?.UserRole?.UserType == Enumerators.UserType.DataCollector;

            // Per-column filter values (ids / enum values) sent by the header dropdowns
            var columnFilters = new Dictionary<string, string>();

            foreach (var col in request.Columns ?? new List<DataTablesColumn>())
            {
                var val = col.Search?.Value;
                if (string.IsNullOrWhiteSpace(val) || !col.Searchable || string.IsNullOrWhiteSpace(col.Name))
                    continue;

                val = val.Trim();
                if (val.Length > 0)
                    columnFilters[col.Name] = val;
            }

            // Pass the filters to the service — it must apply them BEFORE paging
            var result = _assetService.GetPaginatedAssets(
                request.Start, request.Length, request.Search?.Value, currentUser, columnFilters);

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
                    <a class='dropdown-item js-service-request' href='#' data-asset-id='{asset.Asset.AssetId}'>
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

                // Functional status is the asset's own (see AssetStatusTracker); a decommission movement still overrides it
                var statusSource = asset.PendingMovement ?? asset.LastMovement;
                if (statusSource != null)
                {
                    if (statusSource.Reason != Enumerators.MovementReason.Decommission)
                    {
                        statusHtml = Format.DisplayFunctionalStatus(asset.Asset.FunctionalStatus).ToString();
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
        [FacilityScoped(FacilityRecord.Asset)]
        public async Task<IActionResult> Detail(Guid id)
        {
            var vm = await _assetService.GetAssetDetailAsync(id);
            return vm == null ? NotFound() : View(vm);
        }

        [RequireLogin]
        [AuthorizeRole(nameof(UserType.Administrator), nameof(UserType.FacilityManager), nameof(UserType.Biomed))]
        [NoFacilityData("Empty registration form")]
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
        [NoFacilityData("Empty quick registration form")]
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
        [NoFacilityData("Registration lookups and facility names")]
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
        [FacilityScoped(FacilityRecord.Asset, "id", FacilityAccessLevel.Change)]
        public async Task<IActionResult> Edit(Guid id)
        {
            var asset = await _assetService.GetAssetForEditAsync(id);
            if (asset == null) return NotFound();
            var viewModel = await GetBaseAssetRegView(asset);
            viewModel.CylinderSizes = await _repo.GetCylinderSizes();
            viewModel.CylinderContents = await _repo.GetCylinderContents();

            return View("edit", viewModel);

        }

        [RequireLogin]
        [HttpPost]
        [ValidateAntiForgeryToken]
        [FacilityScoped(FacilityRecord.Asset, "Assetmodel.asset.AssetId", FacilityAccessLevel.Change)]
        public async Task<IActionResult> Edit(AssetRegistrationViewModel Assetmodel)
        {
            var asset = Assetmodel.asset;

            // Cylinder detail is saved separately (insert, update or remove) so Update(asset) never touches it
            var detail = asset.CylinderDetail;
            asset.CylinderDetail = null;
            RemoveCylinderModelState(); // CylinderRules owns the Size/Content messages
            foreach (var error in await _registration.ValidateEditAsync(asset, detail))
                ModelState.AddModelError("", error);

            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            TempData["Error"] = string.Join("; ", errors);
            if (ModelState.IsValid)
            {
                await _registration.UpdateAsync(asset, detail, CurrentUser!);
                return RedirectToAction(nameof(Index));
            }

            asset.CylinderDetail = detail;
            var viewModel = await GetBaseAssetRegView(asset);
            viewModel.CylinderSizes = await _repo.GetCylinderSizes();
            viewModel.CylinderContents = await _repo.GetCylinderContents();

            return View("edit", viewModel);
        }

        [HttpPost]
        [AuthorizeRole(nameof(UserType.Administrator), nameof(UserType.FacilityManager), nameof(UserType.Biomed))]
        [FacilityChecked("AssetRegistrationService: only admins register for another facility")]
        public async Task<IActionResult> RegisterAsset(AssetRegistrationViewModel Assetmodel)
        {
            var asset = Assetmodel.asset;

            RemoveCylinderModelState(); // CylinderRules owns the Size/Content messages
            foreach (var (field, message) in await _registration.ValidateRegistrationAsync(Assetmodel))
                ModelState.AddModelError(field, message);

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

            await _registration.RegisterAsync(Assetmodel, CurrentUser!);
            TempData["Success"] = "Equipment added successfully.";

            return RedirectToAction(nameof(Index));
        }
        
        [HttpPost]
        [AuthorizeRole(nameof(UserType.Administrator), nameof(UserType.DataCollector), nameof(UserType.Biomed))]
        [FacilityChecked("Quick registration places each asset at the facility picked for it, by design (field data collection)")]
        public async Task<IActionResult> SaveAssets([FromBody] List<Asset> assets)
        {
            if (assets == null || !assets.Any())
                return BadRequest("No assets were provided.");

            if (CurrentUser?.Facility?.FacilityCode == null)
                return Unauthorized("User context missing.");

            var result = await _registration.SaveBulkAsync(assets, CurrentUser);
            if (result.Errors.Any())
                return BadRequest(string.Join("\n", result.Errors));

            return Ok(new
            {
                Message = "Assets saved successfully.",
                Count = result.Count
            });
        }

        // Sticker sheet: Code 128 barcode of the asset tag, rendered server-side (works offline)
        [RequireLogin]
        [HttpPost]
        [AuthorizeRole(nameof(UserType.Administrator), nameof(UserType.Biomed))]
        [FacilityChecked("Only tags FacilityAccess.VisibleAssets allows are printed")]
        public async Task<IActionResult> PrintStickers(List<string> tags)
        {
            // Only assets this user may see; keeps the order the user selected them in
            var wanted = (tags ?? new List<string>()).Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()).ToList();
            var visible = (await _access.VisibleAssets(CurrentUser!)
                    .Where(a => wanted.Contains(a.AssetTagNumber))
                    .Select(a => a.AssetTagNumber)
                    .ToListAsync())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            return View("stickers", await _assetService.GetAssetsByTagsAsync(wanted.Where(visible.Contains)));
        }

        // Serial list pre-check for Quick Registration: returns the serials that are already registered
        [RequireLogin]
        [HttpPost]
        [NoFacilityData("Serials are unique across all facilities; returns only which are taken")]
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

        // Hidden cylinder inputs on a non-cylinder form must not cause binding errors
        void RemoveCylinderModelState()
        {
            foreach (var key in ModelState.Keys.Where(k => k.StartsWith("asset.CylinderDetail", StringComparison.OrdinalIgnoreCase)).ToList())
                ModelState.Remove(key);
        }

        [HttpGet]
        [AuthorizeRole(nameof(UserType.Administrator), nameof(UserType.FacilityManager))]
        [FacilityScoped(FacilityRecord.Asset, "Id", FacilityAccessLevel.Change)]
        public async Task<IActionResult> HideAsset(Guid Id)
        {
            await _registration.HideAsync(Id);
            return RedirectToAction(nameof(Index));
        }

    }
}
