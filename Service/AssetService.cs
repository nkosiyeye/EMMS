using EMMS.Data.Repository;
using EMMS.Data;
using EMMS.Models.Admin;
using EMMS.Models;
using EMMS.ViewModels;
using Microsoft.EntityFrameworkCore;
using EMMS.Data.Migrations;
using Microsoft.Extensions.Caching.Memory;
using EMMS.Models.Pagination;
using static EMMS.Models.Enumerators;

namespace EMMS.Service
{
    public class AssetService : IAssetService
    {
        private readonly ApplicationDbContext _context;
        private readonly AssetManagementRepo _repo;
        private readonly IMemoryCache _cache;

        public AssetService(ApplicationDbContext context, IMemoryCache cache,AssetManagementRepo repo)
        {
            _context = context;
            _repo = repo;
        }
        public async Task<AssetIndexViewModel?> GetAssetIndexViewModel(User currentUser,bool isDashboard=false)
        {
            // Get all active assets
            var assets = await _repo.GetAssetsFromDb().ConfigureAwait(false);
            var orderedAssets = assets?.OrderByDescending(a => a.DateCreated).ToList() ?? new List<Asset>();

            // Get latest movements
            var lastMovements = await _repo.GetAssetMovement().ConfigureAwait(false);

            var lastMovementDict = lastMovements?
                .Where(m => m != null && m.AssetId != Guid.Empty)
                .GroupBy(m => m.AssetId)
                .ToDictionary(g => g.Key, g => g.First())
                ?? new Dictionary<Guid, MoveAsset>();

            // Create view models safely
            var assetViewModels = orderedAssets.Select(asset => new AssetViewModel
            {
                Asset = asset ?? new Asset(),
                LastMovement = (asset != null && lastMovementDict.TryGetValue(asset.AssetId, out var move)) ? move : null
            }).ToList();

            // Apply facility restriction
            if (currentUser?.UserRole?.UserType != Enumerators.UserType.Administrator)
            {
                var userFacilityId = currentUser?.FacilityId;
                var userId = currentUser?.UserId;

                assetViewModels = assetViewModels
                    .Where(vm =>
                        (vm.LastMovement != null && vm.LastMovement.FacilityId == userFacilityId) ||
                        vm.Asset.CreatedBy == userId)
                    .ToList();
            }
            
            if(currentUser?.UserRole.UserType == Enumerators.UserType.DataCollector)
            {
                var userFacilityId = currentUser?.FacilityId;
                var userId = currentUser?.UserId;

                assetViewModels = isDashboard ? assetViewModels : assetViewModels
                    .Where(vm => vm.Asset.CreatedBy == userId)
                    .ToList();

            }

                return new AssetIndexViewModel
                {
                    assetViewModels = assetViewModels,
                    moveAsset = new MoveAsset()
                };
        }


        public async Task<AssetIndexViewModel?> GetAssetDueServiceViewModel()
        {
            // Fetch assets due for service asynchronously
            var assets = await _repo.GetAssetsDueService()
                .ConfigureAwait(false);

            var lastMovements = await _repo.GetAssetMovement()
                .ConfigureAwait(false);

            var lastMovementDict = lastMovements?
                .Where(m => m != null)
                .ToDictionary(m => m.AssetId, m => m)
                ?? new Dictionary<Guid, MoveAsset>();

            var assetViewModels = assets.Select(asset => new AssetViewModel
            {
                Asset = asset,
                LastMovement = lastMovementDict.TryGetValue(asset.AssetId, out var move) ? move : null
            }).ToList();

            return new AssetIndexViewModel
            {
                assetViewModels = assetViewModels,
                moveAsset = new MoveAsset()
            };
        }

        public PaginatedResult<AssetViewModel> GetPaginatedAssets(
            int start, int length, string search, User currentUser,
            Dictionary<string, string> columnFilters = null)
        {
            var query = _context.Assets
                .Include(a => a.Category)
                .Include(a => a.SubCategory)
                .Include(a => a.Vendor)
                .Include(a => a.ServiceProvider)
                .AsQueryable();

            bool isAdmin = currentUser?.UserRole?.UserType == Enumerators.UserType.Administrator;

            // If NOT admin → only show assets currently located in the user's facility
            if (!isAdmin)
            {
                if (currentUser?.FacilityId == null)
                {
                    return new PaginatedResult<AssetViewModel>
                    {
                        TotalCount = 0,
                        FilteredCount = 0,
                        Items = new List<AssetViewModel>()
                    };
                }

                var userFacilityId = currentUser.FacilityId;

                // Assets whose current location (vw_AssetCurrentLocation) is the user's facility
                query = query.Where(a => _context.AssetCurrentLocations.Any(l => l.AssetId == a.AssetId && l.FacilityId == userFacilityId));
            }

            int totalCount = query.Count();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(a =>
                    a.AssetTagNumber.Contains(search) ||
                    a.SubCategory.Name.Contains(search) ||
                    a.Category.Name.Contains(search));
            }

            // NEW: per-column filters from the header dropdowns.
            // Applied BEFORE filteredCount and BEFORE paging, so both the count
            // and the returned page honour them.
            if (columnFilters != null)
            {
                foreach (var (name, value) in columnFilters)
                {
                    switch (name)
                    {
                        case "Category":
                            query = query.Where(a => a.Category.Name == value);
                            break;

                        case "Vendor":
                            query = query.Where(a => a.Vendor.Name == value);
                            break;

                        case "Service Provider":
                            query = query.Where(a => a.ServiceProvider.Name == value);
                            break;

                        case "ServicePoint":
                            var servicePoint = _context.LookupItems.FirstOrDefault(l => l.Name == value );
                            query = query.Where(a => a.ServicePointId == servicePoint.Id);
                            break;

                        case "Facility":
                            // Facility shown in the table = current location's facility
                            var facility = _context.Facilities.FirstOrDefault(f => f.FacilityName == value);
                            query = query.Where(a => _context.AssetCurrentLocations
                                .Any(l => l.AssetId == a.AssetId && l.FacilityId == facility.FacilityId));
                            break;

                        case "FromFacility":
                            var fromFacility = _context.Facilities.FirstOrDefault(f => f.FacilityName == value);
                            query = query.Where(a => _context.AssetCurrentLocations
                                .Any(l => l.AssetId == a.AssetId && l.FacilityId == fromFacility.FacilityId));
                            break;

                        case "ToFacility":
                            var toFacility = _context.Facilities.FirstOrDefault(f => f.FacilityName == value);
                            query = query.Where(a => _context.AssetCurrentLocations
                                .Any(l => l.AssetId == a.AssetId && l.FacilityId == toFacility.FacilityId));
                            break;

                        case "Status":
                            // Mirror the controller's status logic:
                            //   PendingMovement ?? LastMovement drives the functional status;
                            //   Decommission renders as "Decommissioned".
                            // Nulls render as "-", which is not a filterable value.
                            query = query.Where(a =>
                                (_context.AssetsInTransit.Any(t => t.AssetId == a.AssetId)          // pending movement row exists
                                 || _context.AssetCurrentLocations.Any(l => l.AssetId == a.AssetId))
                                && (
                                    // Decommissioned branch
                                    (
                                        (_context.AssetsInTransit.Any(t => t.AssetId == a.AssetId)
                                            ? _context.AssetMovement.Where(m => _context.AssetsInTransit.Any(t => t.AssetId == a.AssetId && t.MovementId == m.MovementId))
                                                .Select(m => m.Reason).FirstOrDefault()
                                            : _context.AssetMovement.Where(m => _context.AssetCurrentLocations.Any(l => l.AssetId == a.AssetId && l.MovementId == m.MovementId))
                                                .Select(m => m.Reason).FirstOrDefault()
                                        ) == MovementReason.Decommission
                                    )
                                    && ((int)ProcurementStatus.Decommissioned).ToString() == value
                                 ||
                                    // Non-decommissioned branch: functional status text matches
                                    (
                                        (_context.AssetsInTransit.Any(t => t.AssetId == a.AssetId)
                                            ? _context.AssetMovement.Where(m => _context.AssetsInTransit.Any(t => t.AssetId == a.AssetId && t.MovementId == m.MovementId))
                                                .Select(m => (int)m.FunctionalStatus).FirstOrDefault()
                                            : _context.AssetMovement.Where(m => _context.AssetCurrentLocations.Any(l => l.AssetId == a.AssetId && l.MovementId == m.MovementId))
                                                .Select(m => (int)m.FunctionalStatus).FirstOrDefault()
                                        )).ToString() == value
                                 )
                                );
                            break;
                    }
                }
            }

            int filteredCount = query.Count();

            var assets = query
                .OrderByDescending(a => a.DateCreated)
                .Skip(start)
                .Take(length)
                .ToList();

            // Current location and any pending movement for this page, from the two location views
            var assetIds = assets.Select(a => a.AssetId).ToList();

            var locationMovementIds = _context.AssetCurrentLocations
                .Where(l => assetIds.Contains(l.AssetId))
                .Select(l => l.MovementId);
            var lastMovementDict = _context.AssetMovement
                .AsNoTracking()
                .Where(m => locationMovementIds.Contains(m.MovementId))
                .Include(m => m.Facility)
                .Include(m => m.ServicePoint)
                .ToDictionary(m => m.AssetId);

            var pendingMovementIds = _context.AssetsInTransit
                .Where(t => assetIds.Contains(t.AssetId))
                .Select(t => t.MovementId);
            var pendingDict = _context.AssetMovement
                .AsNoTracking()
                .Where(m => pendingMovementIds.Contains(m.MovementId))
                .Include(m => m.Facility)
                .Include(m => m.ServicePoint)
                .ToDictionary(m => m.AssetId);

            var items = assets.Select(asset => new AssetViewModel
            {
                Asset = asset,
                LastMovement = lastMovementDict.TryGetValue(asset.AssetId, out var move)
                    ? move
                    : null,
                PendingMovement = pendingDict.TryGetValue(asset.AssetId, out var pending) ? pending : null
            }).OrderByDescending(m => m.Asset.DateCreated).ToList();

            return new PaginatedResult<AssetViewModel>
            {
                TotalCount = totalCount,
                FilteredCount = filteredCount,
                Items = items
            };
        }

    }
}
