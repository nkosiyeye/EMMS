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

        public async Task<IEnumerable<Asset>> GetAssetsFromDb()
        {
            return await _context.Assets
                .AsNoTracking()
                .Include(x => x.Category)
                .Include(x => x.SubCategory)
                .Include(x => x.Department)
                .Include(x => x.Manufacturer)
                .Include(x => x.Vendor)
                .Include(x => x.ServiceProvider)
                .Where(a => a.RowState == RowStatus.Active)
                .OrderByDescending(a => a.DateCreated)
                .ToListAsync()
                .ConfigureAwait(false);
        }

        public async Task<IEnumerable<AssetViewModel>> GetAssetsWithMovementDb()
        {
            var assets = await _context.Assets
                .AsNoTracking()
                .Include(x => x.Category)
                .Include(x => x.SubCategory)
                .Include(x => x.Department)
                .Include(x => x.Manufacturer)
                .Include(x => x.Vendor)
                .Include(x => x.ServiceProvider)
                .Include(x => x.User)
                .Where(a => a.RowState == RowStatus.Active)
                .ToListAsync()
                .ConfigureAwait(false);

            var assetIds = assets.Select(a => a.AssetId).ToList();

            var movements = await _context.AssetMovement
                .AsNoTracking()
                .Include(a => a.Facility)
                .Where(m => assetIds.Contains(m.AssetId))
                .GroupBy(m => m.AssetId)
                .Select(g => g.OrderByDescending(m => m.MovementDate).FirstOrDefault())
                .ToListAsync()
                .ConfigureAwait(false);

            var movementDict = movements
                .Where(m => m != null)
                .ToDictionary(m => m.AssetId, m => m);

            return assets.Select(a => new AssetViewModel
            {
                Asset = a,
                LastMovement = movementDict.TryGetValue(a.AssetId, out var move) ? move : null
            }).ToList();
        }

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
            var assetMovement = await _context.AssetMovement
                .AsNoTracking()
                .Include(m => m.Facility)
                .Include(m => m.ServicePoint)
                .Where(m => m.AssetId == assetId)
                .OrderByDescending(m => m.MovementDate)
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);

            return assetMovement?.Facility?.FacilityId;
        }

        public async Task<Asset?> GetAssetById(Guid assetId)
        {
            return await _context.Assets
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.AssetId == assetId && a.RowState == RowStatus.Active)
                .ConfigureAwait(false);
        }

        public async Task<List<MoveAsset?>> GetAssetMovement()
        {
            return await _context.AssetMovement
                .AsNoTracking()
                .Include(m => m.Facility)
                .Include(m => m.ServicePoint)
                .Include(m => m.RecievedUser)
                .Include(m => m.RejectedUser)
                .GroupBy(m => m.AssetId)
                .Select(g => g.OrderByDescending(m => m.MovementDate)
                    .FirstOrDefault(g => g.DateReceived != null))
                .ToListAsync()
                .ConfigureAwait(false);
        }

        public async Task<Asset?> GetAssetBySerialNumber(string serialNum)
        {
            return await _context.Assets
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.SerialNumber == serialNum && a.RowState == RowStatus.Active)
                .ConfigureAwait(false);
        }

        public async Task<IEnumerable<Asset>> GetAssetsDueService(int period = 4)
        {
            DateTime today = DateTime.Today;
            DateTime dueDate = today.AddMonths(period);

            return await _context.Assets
                .AsNoTracking()
                .Include(a => a.SubCategory)
                .Where(a => a.NextServiceDate >= today && a.NextServiceDate <= dueDate)
                .OrderByDescending(a => a.NextServiceDate)
                .ToListAsync()
                .ConfigureAwait(false);
        }
        public async Task<IEnumerable<Facility>> GetFacilities()
        {
            const string cacheKey = "Facilities_Active";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<Facility>? cachedList))
                return cachedList;

            var result = await _context.Facilities
                .Where(x => x.RowState == RowStatus.Active && x.isOffSite == false)
                .ToListAsync();

            _cache.Set(cacheKey, result, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheDuration,
                Priority = CacheItemPriority.High
            });

            return result;
        }

        public async Task<IEnumerable<LookupItem>> GetServicePoints()
        {
            const string cacheKey = "Lookup_Service Point";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<LookupItem>? cachedList))
                return cachedList;

            var result = await _context.LookupItems
                .Where(x => x.LookupList.Name == "Service Point" && x.RowState == RowStatus.Active)
                .ToListAsync();

            _cache.Set(cacheKey, result, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheDuration,
                Priority = CacheItemPriority.High
            });

            return result;
        }


        // ----------------------------------------------------
        // LOOKUPS (WITH CACHING)
        // ----------------------------------------------------

        private async Task<IEnumerable<LookupItem>> GetLookupItemsByName(string name)
        {
            string cacheKey = $"Lookup_{name}";

            // Check if in cache
            if (_cache.TryGetValue(cacheKey, out IEnumerable<LookupItem>? cachedList))
                return cachedList;

            // Otherwise fetch and cache
            var result = await _context.LookupItems
                .AsNoTracking()
                .Include(x => x.LookupList)
                .Where(x => x.LookupList.Name == name && x.RowState == RowStatus.Active)
                .ToListAsync()
                .ConfigureAwait(false);

            _cache.Set(cacheKey, result, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheDuration,
                Priority = CacheItemPriority.High
            });

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
