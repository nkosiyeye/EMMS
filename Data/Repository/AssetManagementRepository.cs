using EMMS.Models;
using EMMS.Models.Admin;
using EMMS.Models.Entities;
using EMMS.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using static EMMS.Models.Enumerators;
using Asset = EMMS.Models.Asset;

namespace EMMS.Data.Repository
{
    public class AssetManagementRepo
    {
        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _cache;

        // Cache lifetime (adjustable)
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

        public AssetManagementRepo(ApplicationDbContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        // ----------------------------------------------------
        // ASSET DATA METHODS (NO CACHING - DYNAMIC DATA)
        // ----------------------------------------------------

        public async Task<List<Models.Entities.Notification>> GetNotifications()
        {
            return await _context.Notifications
                .AsNoTracking()
                .OrderByDescending(n => n.DateCreated)
                .ToListAsync()
                .ConfigureAwait(false);
        }

        public async Task<int?> GetAssetLocation(Guid assetId)
        {
            return await _context.AssetCurrentLocations
                .Where(l => l.AssetId == assetId)
                .Select(l => (int?)l.FacilityId)
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);
        }

        // ----------------------------------------------------
        // LOCATION (vw_AssetCurrentLocation / vw_AssetInTransit)
        // ----------------------------------------------------

        // The movement that defines each asset's current location, keyed by AssetId (all assets when ids is null).
        // Pass an IQueryable of ids to keep the filter a SQL subquery instead of a parameter list.
        public async Task<Dictionary<Guid, MoveAsset>> GetCurrentLocationMovements(IEnumerable<Guid>? assetIds = null)
        {
            IQueryable<AssetCurrentLocation> locations = _context.AssetCurrentLocations;
            if (assetIds is IQueryable<Guid> idQuery)
            {
                locations = locations.Where(l => idQuery.Contains(l.AssetId));
            }
            else if (assetIds != null)
            {
                var ids = assetIds.ToList();
                locations = locations.Where(l => ids.Contains(l.AssetId));
            }
            var movementIds = locations.Select(l => l.MovementId);

            return await _context.AssetMovement
                .AsNoTracking()
                .Include(m => m.Facility)
                .Include(m => m.ServicePoint)
                .Include(m => m.RecievedUser)
                .Include(m => m.RejectedUser)
                .Where(m => movementIds.Contains(m.MovementId))
                .ToDictionaryAsync(m => m.AssetId)
                .ConfigureAwait(false);
        }

        // The pending movement of each asset that is in transit, keyed by AssetId
        public async Task<Dictionary<Guid, MoveAsset>> GetInTransitMovements(IEnumerable<Guid>? assetIds = null)
        {
            IQueryable<AssetInTransit> pending = _context.AssetsInTransit;
            if (assetIds != null)
            {
                var ids = assetIds.ToList();
                pending = pending.Where(t => ids.Contains(t.AssetId));
            }
            var movementIds = pending.Select(t => t.MovementId);

            return await _context.AssetMovement
                .AsNoTracking()
                .Include(m => m.From)
                .Include(m => m.Facility)
                .Where(m => movementIds.Contains(m.MovementId))
                .ToDictionaryAsync(m => m.AssetId)
                .ConfigureAwait(false);
        }

        public async Task<Asset?> GetAssetById(Guid assetId)
        {
            return await _context.Assets
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.AssetId == assetId && a.RowState == RowStatus.Active)
                .ConfigureAwait(false);
        }

        public async Task<IEnumerable<MoveAsset?>> GetAssetMovementFromSP()
        {
            return (await _context.AssetMovement
                .FromSqlRaw("EXEC sp_GetAssetMovements")
                .ToListAsync())
                .AsEnumerable();
        }
        public async Task<Asset?> GetAssetBySerialNumber(string serialNum)
        {
            return await _context.Assets
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.SerialNumber == serialNum && a.RowState == RowStatus.Active)
                .ConfigureAwait(false);
        }

        // Active assets whose serial is in the list (SQL collation is case-insensitive)
        public async Task<List<string>> GetExistingSerialNumbers(IEnumerable<string> serialNums)
        {
            var list = serialNums.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (list.Count == 0) return new List<string>();

            return await _context.Assets
                .AsNoTracking()
                .Where(a => a.RowState == RowStatus.Active && list.Contains(a.SerialNumber))
                .Select(a => a.SerialNumber)
                .ToListAsync()
                .ConfigureAwait(false);
        }

        public async Task<IEnumerable<Facility>> GetFacilities()
        {
            
            var result = await _context.Facilities
                .Where(x => x.RowState == RowStatus.Active && (x.isOffSite == false || x.isOffSite == null))
                .ToListAsync();            

            return result;
        }

        // Every active facility (off-site included), by name: facility pickers and filters
        public async Task<List<Facility>> GetActiveFacilitiesAsync()
        {
            return await _context.Facilities
                .AsNoTracking()
                .Where(f => f.RowState == RowStatus.Active)
                .OrderBy(f => f.FacilityName)
                .ToListAsync()
                .ConfigureAwait(false);
        }

        public async Task<IEnumerable<LookupItem>> GetServicePoints()
        {

            var result = await _context.LookupItems
                .Where(x => x.LookupList.Name == "Service Point" && x.RowState == RowStatus.Active)
                .ToListAsync();

            return result;
        }


        // ----------------------------------------------------
        // LOOKUPS (WITH CACHING)
        // ----------------------------------------------------

        private async Task<IEnumerable<LookupItem>> GetLookupItemsByName(string name)
        {

            // Otherwise fetch and cache
            var result = await _context.LookupItems
                .AsNoTracking()
                .Include(x => x.LookupList)
                .Where(x => x.LookupList.Name == name && x.RowState == RowStatus.Active)
                .ToListAsync()
                .ConfigureAwait(false);

            return result;
        }

        public async Task<IEnumerable<LookupItem>> GetCategories() => await GetLookupItemsByName("Category");
        public async Task<IEnumerable<LookupItem>> GetSubCategories() => await GetLookupItemsByName("SubCategory");
        public async Task<IEnumerable<LookupItem>> GetDepartments() => await GetLookupItemsByName("Department");
        public async Task<IEnumerable<LookupItem>> GetManufacturers() => await GetLookupItemsByName("Manufacturer");
        public async Task<IEnumerable<LookupItem>> GetVendors() => await GetLookupItemsByName("Vendor");
        public async Task<IEnumerable<LookupItem>> GetServiceProviders() => await GetLookupItemsByName("Service Provider");
        public async Task<IEnumerable<LookupItem>> GetStatuses() => await GetLookupItemsByName("Status");
        public async Task<IEnumerable<LookupItem>> GetUnitOfMeasures() => await GetLookupItemsByName("Unit of Measure");
        public async Task<IEnumerable<LookupItem>> GetLifespanPeriods() => await GetLookupItemsByName("Period");
        public async Task<IEnumerable<LookupItem>> GetCylinderSizes() => (await GetLookupItemsByName(Constants.LookupListNames.CylinderSize)).OrderBy(x => x.SortIndex);
        public async Task<IEnumerable<LookupItem>> GetCylinderContents() => (await GetLookupItemsByName(Constants.LookupListNames.CylinderContent)).OrderBy(x => x.SortIndex);

        public async Task<int?> GetCentralStoreFacilityId()
        {
            return await _context.Facilities
                .AsNoTracking()
                .Where(f => f.IsCentralStore && f.RowState == RowStatus.Active)
                .Select(f => (int?)f.FacilityId)
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);
        }

        public async Task<AssetCylinderDetail?> GetCylinderDetail(Guid assetId)
        {
            return await _context.AssetCylinderDetails
                .AsNoTracking()
                .Include(d => d.Size)
                .Include(d => d.Content)
                .FirstOrDefaultAsync(d => d.AssetId == assetId)
                .ConfigureAwait(false);
        }

        // Active item names (SubCategory list) under a category, for the cascading dropdown
        public async Task<List<LookupItem>> GetSubCategoriesOf(int categoryId)
        {
            return await _context.LookupItems
                .AsNoTracking()
                .Where(x => x.LookupList.Name == "SubCategory" && x.ParentId == categoryId && x.RowState == RowStatus.Active)
                .ToListAsync()
                .ConfigureAwait(false);
        }

        public async Task<LookupItem?> GetLookupItemAsync(int id)
        {
            return await _context.LookupItems
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id)
                .ConfigureAwait(false);
        }

        // Lookup items by id (any list, any state), keyed by id
        public async Task<Dictionary<int, LookupItem>> GetLookupItemsByIds(IEnumerable<int> ids)
        {
            var list = ids.Distinct().ToList();
            return await _context.LookupItems
                .AsNoTracking()
                .Where(l => list.Contains(l.Id))
                .ToDictionaryAsync(l => l.Id)
                .ConfigureAwait(false);
        }

        // ------------------------------
        // Cached Lookup Properties
        // ------------------------------

        public IEnumerable<LookupItem> CategoriesCache => _cache.Get<IEnumerable<LookupItem>>("Lookup_Category") ?? Enumerable.Empty<LookupItem>();
        public IEnumerable<LookupItem> SubCategoriesCache => _cache.Get<IEnumerable<LookupItem>>("Lookup_SubCategory") ?? Enumerable.Empty<LookupItem>();
        public IEnumerable<LookupItem> DepartmentsCache => _cache.Get<IEnumerable<LookupItem>>("Lookup_Department") ?? Enumerable.Empty<LookupItem>();
        public IEnumerable<LookupItem> ManufacturersCache => _cache.Get<IEnumerable<LookupItem>>("Lookup_Manufacturer") ?? Enumerable.Empty<LookupItem>();
        public IEnumerable<LookupItem> VendorsCache => _cache.Get<IEnumerable<LookupItem>>("Lookup_Vendor") ?? Enumerable.Empty<LookupItem>();
        public IEnumerable<LookupItem> ServiceProvidersCache => _cache.Get<IEnumerable<LookupItem>>("Lookup_Service Provider") ?? Enumerable.Empty<LookupItem>();
        public IEnumerable<LookupItem> StatusesCache => _cache.Get<IEnumerable<LookupItem>>("Lookup_Status") ?? Enumerable.Empty<LookupItem>();
        public IEnumerable<LookupItem> UnitOfMeasuresCache => _cache.Get<IEnumerable<LookupItem>>("Lookup_Unit of Measure") ?? Enumerable.Empty<LookupItem>();
        public IEnumerable<LookupItem> LifespanPeriodsCache => _cache.Get<IEnumerable<LookupItem>>("Lookup_Period") ?? Enumerable.Empty<LookupItem>();
        public IEnumerable<Facility> FacilitiesCache => _cache.Get<IEnumerable<Facility>>("Facilities_Active") ?? Enumerable.Empty<Facility>();
        public IEnumerable<LookupItem> ServicePointsCache => _cache.Get<IEnumerable<LookupItem>>("Lookup_Service Point") ?? Enumerable.Empty<LookupItem>();


        // ----------------------------------------------------
        // CACHE MANAGEMENT
        // ----------------------------------------------------

        public void ClearLookupCache()
        {
            var lookupKeys = new[]
            {
                "Lookup_Category",
                "Lookup_SubCategory",
                "Lookup_Department",
                "Lookup_Manufacturer",
                "Lookup_Vendor",
                "Lookup_Service Provider",
                "Lookup_Status",
                "Lookup_Unit of Measure",
                "Lookup_Period",
                "Facilities_Active",
                "Lookup_Service Point"
            };

            foreach (var key in lookupKeys)
                _cache.Remove(key);
        }
    }
}
