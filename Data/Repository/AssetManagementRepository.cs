using EMMS.Data.Migrations;
using EMMS.Models;
using EMMS.Models.Admin;
using EMMS.Models.Entities;
using EMMS.ViewModels;
using Microsoft.EntityFrameworkCore;
using NuGet.ContentModel;
using static EMMS.Models.Enumerators;
using Asset = EMMS.Models.Asset;

namespace EMMS.Data.Repository
{
    public class AssetManagementRepo
    {
        private readonly ApplicationDbContext _context;
        public AssetManagementRepo(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Asset>> GetAssetsFromDb()
        {
            return await _context.Assets
                .Include(x => x.Category)
                .Include(x => x.SubCategory)
                .Include(x => x.Department)
                .Include(x => x.Manufacturer)
                .Include(x => x.Vendor)
                .Include(x => x.ServiceProvider)
                .ToListAsync();
        }
        public async Task<IEnumerable<AssetViewModel>> GetAssetsWithMovementDb()
        {
            var assets = await _context.Assets
                .Include(x => x.Category)
                .Include(x => x.SubCategory)
                .Include(x => x.Department)
                .Include(x => x.Manufacturer)
                .Include(x => x.Vendor)
                .Include(x => x.ServiceProvider)
                .Include(x => x.User)
                .ToListAsync();

            var assetIds = assets.Select(a => a.AssetId).ToList();

            // Fetch last movement for each asset
            var movements = await _context.AssetMovement
                .Include(a => a.Facility)
                .Where(m => assetIds.Contains(m.AssetId))
                .GroupBy(m => m.AssetId)
                .Select(g => g.OrderByDescending(m => m.MovementDate).FirstOrDefault())
                .ToListAsync();

            var movementDict = movements
                .Where(m => m != null)
                .ToDictionary(m => m.AssetId, m => m);

            return assets.Select(a => new AssetViewModel
            {
                Asset = a,
                LastMovement = movementDict.ContainsKey(a.AssetId) ? movementDict[a.AssetId] : null
            }).ToList();
        }

        public async Task<List<Models.Entities.Notification>> GetNotifications()
        {
            return await _context.Notifications
            .OrderByDescending(n => n.DateCreated)
            .ToListAsync();
        }

        public async Task<int?> GetAssetLocation(Guid assetId)
        {
            var assetMovement = await _context.AssetMovement
                                        .Where(m => m.AssetId == assetId)
                                        .Include(m => m.Facility)
                                        .Include(m => m.ServicePoint)
                                        .OrderByDescending(m => m.MovementDate)
                                        .FirstOrDefaultAsync();

            return assetMovement?.Facility?.FacilityId;
        }

        public async Task<Asset?> GetAssetById(Guid assetId)
        {
            return await _context.Assets
                .FirstOrDefaultAsync(a => a.AssetId == assetId && a.RowState == RowStatus.Active);
        }

        public async Task<List<MoveAsset?>> GetAssetMovement()
        {
            return await _context.AssetMovement
                                .Include(m => m.Facility)
                                .Include(m => m.ServicePoint)
                                .Include(m => m.RecievedUser)
                                .Include(m => m.RejectedUser)
                                .GroupBy(m => m.AssetId)
                                .Select(g => g.OrderByDescending(m => m.MovementDate).FirstOrDefault(g => g.DateReceived != null))
                                .ToListAsync();
        }

        public async Task<IEnumerable<LookupItem>> GetCategories()
        {
            var categories = await _context.LookupItems
                                .Where(x => x.LookupList.Name == "Category" && x.RowState == RowStatus.Active)
                                .ToListAsync();
            return categories;
        }

        public async Task<IEnumerable<LookupItem>> GetSubCategories()
        {
            return await _context.LookupItems
                .Where(x => x.LookupList.Name == "SubCategory" && x.RowState == RowStatus.Active)
                .ToListAsync();
        }

        public async Task<IEnumerable<LookupItem>> GetDepartments()
        {
            return await _context.LookupItems
                .Where(x => x.LookupList.Name == "Department" && x.RowState == RowStatus.Active).ToListAsync();
        }


        public async Task<IEnumerable<LookupItem>> GetManufacturers()
        {
            return await _context.LookupItems
                .Where(x => x.LookupList.Name == "Manufacturer" && x.RowState == RowStatus.Active)
                .ToListAsync();
        }

        public async Task<IEnumerable<LookupItem>> GetVendors()
        {
            return await _context.LookupItems
                .Where(x => x.LookupList.Name == "Vendor" && x.RowState == RowStatus.Active)
                .ToListAsync();
        }

        public async Task<IEnumerable<LookupItem>> GetServiceProviders()
        {
            return await _context.LookupItems
                .Where(x => x.LookupList.Name == "Service Provider" && x.RowState == RowStatus.Active)
                .ToListAsync();
        }

        public async Task<IEnumerable<LookupItem>> GetStatuses()
        {
            return await _context.LookupItems
                .Where(x => x.LookupList.Name == "Status" && x.RowState == RowStatus.Active)
                .ToListAsync();
        }

        public async Task<IEnumerable<LookupItem>> GetUnitOfMeasures()
        {
            return await _context.LookupItems
                .Where(x => x.LookupList.Name == "Unit of Measure" && x.RowState == RowStatus.Active)
                .ToListAsync();
        }

        public async Task<IEnumerable<LookupItem>> GetLifespanPeriods()
        {
            return await _context.LookupItems
                .Where(x => x.LookupList.Name == "Period" && x.RowState == RowStatus.Active)
                .ToListAsync();
        }

        public async Task<Asset?> GetAssetBySerialNumber(string serialNum)
        {
            var asset = await _context.Assets
                .FirstOrDefaultAsync(a => a.SerialNumber == serialNum && a.RowState == RowStatus.Active);

            return asset;
        }

        public async Task<IEnumerable<Asset>> GetAssetsDueService(int period = 4)
        {
            DateTime today = DateTime.Today;
            DateTime twoMonthsFromNow = today.AddMonths(period);

            var dueAssets = await _context.Assets
                .Include(a => a.SubCategory)
                .Where(a => a.NextServiceDate >= today && a.NextServiceDate <= twoMonthsFromNow)
                .OrderByDescending(a => a.NextServiceDate)
                .ToListAsync();

            return dueAssets;
        }
    }
}
