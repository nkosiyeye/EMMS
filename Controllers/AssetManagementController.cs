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

        public AssetManagementController(ApplicationDbContext context)
        {
            _context = context;
            _assetService = new AssetService(context);
            _repo = new AssetManagementRepo(context);
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
                .Include(a => a.Category)
                .Include(a => a.SubCategory)
                .Include(a => a.Department)
                .Include(a => a.Manufacturer)
                .Include(a => a.Vendor)
                .Include(a => a.ServiceProvider)
                .Include(u => u.User)
                .FirstOrDefaultAsync(a => a.AssetId == id);

            var serviceHistory = await _context.Job
                .Where(j => j.AssetId == id)
                .Include(j => j.FaultReport)
                .Include(j => j.Status)
                .OrderByDescending(j => j.DateCreated)
                .ToListAsync();

            var movementHistory = await _context.AssetMovement
                .Where(m => m.AssetId == id)
                .Include(m => m.Facility)
                .Include(m => m.ServicePoint)
                .Include(m => m.RecievedUser)
                .Include(m => m.RejectedUser)
                .OrderByDescending(m => m.MovementDate)
                .Where(m => (m.DateReceived != null || m.DateRejected != null) && m.RowState == RowStatus.Active)
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
                AssetTagNumber = $"{facilityCode}AS-" + (_repo.GetAssetsFromDb().Result.Count() + 1).ToString("D3"),
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
            var asset = _repo.GetAssetsFromDb().Result.FirstOrDefault(a => a.AssetId == id)!;
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
            if (await IsDuplicate(asset.SerialNumber))
            {
                ModelState.AddModelError("asset.SerialNumber", "An asset with this serial number already exists.");
            }

            if (ModelState.IsValid)
            {
                asset.AssetId = Guid.NewGuid();
                if (isAdmin && Assetmodel.facilityId.HasValue)
                {
                    var facilityCode = _context.Facilities.FirstOrDefault(f => f.FacilityId == Assetmodel.facilityId)?.FacilityCode;
                    asset.AssetTagNumber = $"{facilityCode}AS-" + (_repo.GetAssetsFromDb().Result.Count() + 1).ToString("D3");
                }
                else
                {
                    var facilityCode = CurrentUser.Facility.FacilityCode;
                    asset.AssetTagNumber = $"{facilityCode}AS-" + (_repo.GetAssetsFromDb().Result.Count() + 1).ToString("D3");
                }
                asset.CreatedBy = CurrentUser!.UserId;
                asset.DateCreated = DateTime.Now;
                asset.RowState = RowStatus.Active;

                CreateEntity(asset);
                _context.Add(asset);
                await _context.SaveChangesAsync();

                if (Assetmodel.alreadyDeployed)
                {   
                    var asmove = new MoveAsset()
                    {
                        MovementDate = Assetmodel.dateDeployed ?? DateTime.Now,
                        AssetId = asset.AssetId,
                        MovementType = MovementType.Facility,
                        FromId = Assetmodel.facilityId ?? 0,
                        FacilityId = Assetmodel.facilityId ?? 0,
                        ServicePointId = Assetmodel.ServicePointId,
                        Reason = Assetmodel.ServicePointId == null ? MovementReason.Deployment : MovementReason.Installation,
                        FunctionalStatus = FunctionalStatus.Functional,
                        IsApproved = true,
                        ApprovedBy = CurrentUser!.UserId,
                        ReceivedBy = CurrentUser!.UserId,
                        DateReceived = Assetmodel.dateDeployed ?? DateTime.Now,
                        DateCreated = DateTime.Now,
                        CreatedBy = CurrentUser!.UserId,
                    };
                    CreateEntity(asmove);
                    _context.Add(asmove);
                    if(asset.WarrantyEndDate != null)
                    {
                        asset.WarrantyStartDate = Assetmodel.dateDeployed ?? DateTime.Now;
                        _context.Update(asset);

                    }
                    await _context.SaveChangesAsync();
                }

                TempData["Success"] = "Equipment added successfully.";
                return RedirectToAction(nameof(Index));
            }
            else
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                TempData["RegistrationError"] = string.Join("\n", errors);

                var viewModel = new AssetRegistrationViewModel
                {
                    asset = asset, // Keep the submitted asset data
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

                return View(viewModel);
            }
        }

        async Task<bool> IsDuplicate(string serialNum)
        {
            Asset? asset = await _repo.GetAssetBySerialNumber(serialNum);
            if (asset != null)
                return true;

            return false;
        }

        async Task<AssetRegistrationViewModel> GetBaseAssetRegView (Asset asset)
        {
            return new AssetRegistrationViewModel
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
            };
        }
    }
}
