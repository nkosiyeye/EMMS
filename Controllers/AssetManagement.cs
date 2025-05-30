using System.Diagnostics;
using EMMS.Data;
using EMMS.Data.Repository;
using EMMS.Models;
using EMMS.Models.Entities;
using EMMS.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static EMMS.Models.Enumerators;

namespace EMMS.Controllers
{
    public class AssetManagement : Controller
    {
        private readonly ApplicationDbContext _context;
        public AssetManagement(ApplicationDbContext context)
        {
            _context = context;
            
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

        public async Task<AssetIndexViewModel?> assetViewModel()
        {

            var _repo = new AssetManagementRepo(_context);
            var assets = await _repo.GetAssetsFromDb();

            // Fetch last movement for each asset
            var lastMovements = await _repo.GetAssetMovement();//.Result.Where(w => w.MovementTypeId == 110);

            // Map assetId to last movement
            var lastMovementDict = lastMovements
                .Where(m => m != null)
                .ToDictionary(m => m.AssetId, m => m);

            //view model to hold asset and last movement info
            var assetViewModels = assets.Select(asset => new AssetViewModel
            {
                Asset = asset,
                LastMovement = lastMovementDict.ContainsKey(asset.AssetId) ? lastMovementDict[asset.AssetId] : null
            }).ToList();

            var indexView = new AssetIndexViewModel
            {
                assetViewModels = assetViewModels,
                moveAsset = new MoveAsset()
            };
            return indexView;
        }
        public async Task<IActionResult> Index()
        {

            return View(await assetViewModel());
        }


        public async Task<IActionResult> Detail(Guid id)
        {
            var asset = await _context.Assets
                .Include(a => a.Category)
                .Include(a => a.SubCategory)
                .Include(a => a.Department)
                .Include(a => a.Manufacturer)
                .Include(a => a.Vendor)
                .Include(a => a.ServiceProvider)
                .Include(a => a.ServicePeriodName)
                .Include(a => a.Status)
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
                .Include(m => m.FunctionalStatus)
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

        public async Task<IActionResult> registerAsset()
        {
            var _repo = new AssetManagementRepo(_context);

            var viewModel = new AssetRegistrationViewModel
            {
                asset = new Asset()
                {
                    AssetTagNumber = "AS-"+(_repo.GetAssetsFromDb().Result.Count()+1).ToString("D3"),
                },
                Categories = await _repo.GetCategories(),
                SubCategories = await _repo.GetSubCategories(),
                Departments = await _repo.GetDepartments(),
                Manufacturers = await _repo.GetManufacturers(),
                Vendors = await _repo.GetVendors(),
                ServiceProviders = await _repo.GetServiceProviders(),
                Statuses = await _repo.GetStatuses(),
                UnitOfMeasures = await _repo.GetUnitOfMeasures(),
                LifespanPeriods = await _repo.GetLifespanPeriods()
            };

            return View(viewModel);
        }
             

        [HttpPost]
        public async Task<IActionResult> RegisterAsset(AssetRegistrationViewModel Assetmodel)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            TempData["Error"] = string.Join("; ", errors);
            if (ModelState.IsValid)
            {
                var asset = Assetmodel.asset;
                asset.DateCreated = DateTime.Now;
                asset.RowState = RowStatus.Active;
                asset.CreatedBy = Guid.NewGuid(); // TBD Replace with actual user ID
                //asset.CreatedBy = User.FindFirstValue(ClaimTypes.NameIdentifier);

                _context.Add(asset);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            var _repo = new AssetManagementRepo(_context);
            var viewModel = new AssetRegistrationViewModel
            {
                asset = new Asset()
                {
                    AssetTagNumber = "AS-" + (_repo.GetAssetsFromDb().Result.Count() + 1).ToString("D3"),
                },
                Categories = await _repo.GetCategories(),
                SubCategories = await _repo.GetSubCategories(),
                Departments = await _repo.GetDepartments(),
                Manufacturers = await _repo.GetManufacturers(),
                Vendors = await _repo.GetVendors(),
                ServiceProviders = await _repo.GetServiceProviders(),
                Statuses = await _repo.GetStatuses(),
            };

            return View(viewModel);

        }

    }
}
