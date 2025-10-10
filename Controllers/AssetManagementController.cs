using EMMS.CustomAttributes;
using EMMS.Data;
using EMMS.Data.Repository;
using EMMS.Models;
using EMMS.Service;
using EMMS.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
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

        [RequireLogin]
        public async Task<IActionResult> Index()
        {
            return View(await _assetService.GetAssetIndexViewModel(CurrentUser));
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
                    Reason = Assetmodel.ServicePointId == null ? MovementReason.Deployment : MovementReason.Installation,
                    FunctionalStatus = FunctionalStatus.Functional,
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

        async Task<AssetRegistrationViewModel> GetBaseAssetRegView(Asset asset)
        {
            // Categories
            var categories = _repo.CategoriesCache;
            if (!categories.Any())
                categories = await _repo.GetCategories();

            // SubCategories
            var subCategories = _repo.SubCategoriesCache;
            if (!subCategories.Any())
                subCategories = await _repo.GetSubCategories();

            // Departments
            var departments = _repo.DepartmentsCache;
            if (!departments.Any())
                departments = await _repo.GetDepartments();

            // Manufacturers
            var manufacturers = _repo.ManufacturersCache;
            if (!manufacturers.Any())
                manufacturers = await _repo.GetManufacturers();

            // Vendors
            var vendors = _repo.VendorsCache;
            if (!vendors.Any())
                vendors = await _repo.GetVendors();

            // ServiceProviders
            var serviceProviders = _repo.ServiceProvidersCache;
            if (!serviceProviders.Any())
                serviceProviders = await _repo.GetServiceProviders();

            // UnitOfMeasures
            var unitOfMeasures = _repo.UnitOfMeasuresCache;
            if (!unitOfMeasures.Any())
                unitOfMeasures = await _repo.GetUnitOfMeasures();

            // LifespanPeriods
            var lifespanPeriods = _repo.LifespanPeriodsCache;
            if (!lifespanPeriods.Any())
                lifespanPeriods = await _repo.GetLifespanPeriods();

            // Facilities
            var facilities = _repo.FacilitiesCache;
            if (!facilities.Any())
                facilities = await _repo.GetFacilities();

            // ServicePoints
            var servicePoints = _repo.ServicePointsCache;
            if (!servicePoints.Any())
                servicePoints = await _repo.GetServicePoints();

            return new AssetRegistrationViewModel
            {
                asset = asset,
                Categories = categories,
                SubCategories = subCategories,
                Departments = departments,
                Manufacturers = manufacturers,
                Vendors = vendors,
                ServiceProviders = serviceProviders,
                Statuses = procurementStatusList,
                UnitOfMeasures = unitOfMeasures,
                LifespanPeriods = lifespanPeriods,
                Facilities = facilities,
                ServicePoints = servicePoints
            };
        }

    }
}
