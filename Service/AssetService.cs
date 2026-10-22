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

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();
                query = query.Where(a =>
                    a.AssetTagNumber.Contains(search) ||
                    a.SerialNumber.Contains(search) ||
                    a.Model.Contains(search) ||
                    a.SubCategory.Name.Contains(search) ||
                    a.Category.Name.Contains(search) ||
                    a.Vendor.Name.Contains(search) ||
                    a.ServiceProvider.Name.Contains(search) ||
                    _context.AssetCurrentLocations.Any(l => l.AssetId == a.AssetId && (
                        _context.Facilities.Any(f => f.FacilityId == l.FacilityId && f.FacilityName.Contains(search)) ||
                        _context.LookupItems.Any(sp => sp.Id == l.ServicePointId && sp.Name.Contains(search)))));
            }

            // Per-column filters from the header dropdowns. Values are lookup/facility ids or enum values
            // (see AssetManagementController.GetFilterOptions). Applied before filteredCount and paging.
            if (columnFilters != null)
            {
                foreach (var (name, value) in columnFilters)
                {
                    int.TryParse(value, out var id);

                    switch (name)
                    {
                        case "Category":
                            query = query.Where(a => a.CategoryId == id);
                            break;

                        case "ItemName":
                            query = query.Where(a => a.SubCategoryId == id);
                            break;

                        case "Placement":
                            var isPlacement = value == "true";
                            query = query.Where(a => a.IsPlacement == isPlacement);
                            break;

                        case "Vendor":
                            query = query.Where(a => a.VendorId == id);
                            break;

                        case "Service Provider":
                            query = query.Where(a => a.ServiceProviderId == id);
                            break;

                        case "Procurement":
                            query = query.Where(a => a.StatusId == id);
                            break;

                        case "Facility":
                            // Facility shown in the table = current location's facility
                            query = query.Where(a => _context.AssetCurrentLocations
                                .Any(l => l.AssetId == a.AssetId && l.FacilityId == id));
                            break;

                        case "ServicePoint":
                            query = query.Where(a => _context.AssetCurrentLocations
                                .Any(l => l.AssetId == a.AssetId && l.ServicePointId == id));
                            break;

                        case "Status":
                            query = ApplyStatusFilter(query, value);
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

        // Mirrors the Status column in AssetManagementController.GetAssets: the pending movement (else the current
        // location) decides; a Decommission reason shows "Decommissioned", otherwise the asset's functional status.
        // Assets with no movement show "-" and match no status.
        private IQueryable<Asset> ApplyStatusFilter(IQueryable<Asset> query, string value)
        {
            if (value == "awaiting")
                return query.Where(a => _context.AssetsInTransit.Any(t => t.AssetId == a.AssetId && !t.IsApproved));
            if (value == "intransit")
                return query.Where(a => _context.AssetsInTransit.Any(t => t.AssetId == a.AssetId && t.IsApproved));

            if (value == "decommissioned")
                return query.Where(a =>
                    _context.AssetsInTransit.Any(t => t.AssetId == a.AssetId &&
                        _context.AssetMovement.Any(m => m.MovementId == t.MovementId && m.Reason == MovementReason.Decommission)) ||
                    (!_context.AssetsInTransit.Any(t => t.AssetId == a.AssetId) &&
                        _context.AssetCurrentLocations.Any(l => l.AssetId == a.AssetId && l.Reason == MovementReason.Decommission)));

            if (!Enum.TryParse<FunctionalStatus>(value, out var status) || !Enum.IsDefined(status))
                return query;

            return query.Where(a => a.FunctionalStatus == status &&
                (
                    _context.AssetsInTransit.Any(t => t.AssetId == a.AssetId &&
                        _context.AssetMovement.Any(m => m.MovementId == t.MovementId && m.Reason != MovementReason.Decommission)) ||
                    (!_context.AssetsInTransit.Any(t => t.AssetId == a.AssetId) &&
                        _context.AssetCurrentLocations.Any(l => l.AssetId == a.AssetId && l.Reason != MovementReason.Decommission))
                ));
        }

    }
}
