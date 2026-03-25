using EMMS.Data.Repository;
using EMMS.Data;
using EMMS.Models.Admin;
using EMMS.Models;
using EMMS.ViewModels;
using Microsoft.EntityFrameworkCore;
using EMMS.Data.Migrations;
using Microsoft.Extensions.Caching.Memory;
using EMMS.Models.Pagination;

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
        public PaginatedResult<AssetViewModel> GetPaginatedAssets(int start, int length, string search, User currentUser)
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

                // Filter assets whose *latest* movement is in the user's facility
                query = query.Where(a => _context.AssetMovement
                    .Where(m => m.AssetId == a.AssetId)
                    .OrderByDescending(m => m.DateCreated)
                    .Select(m => m.FacilityId)
                    .FirstOrDefault() == userFacilityId);
            }

            int totalCount = query.Count();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(a =>
                    a.AssetTagNumber.Contains(search) ||
                    a.SubCategory.Name.Contains(search) ||
                    a.Category.Name.Contains(search));
            }

            int filteredCount = query.Count();

            var assets = query
                .OrderByDescending(a => a.DateCreated)
                .Skip(start)
                .Take(length)
                .ToList();

            // Now load last movements — restrict to user's facility when not admin
            var assetIds = assets.Select(a => a.AssetId).ToList();

            // 1. Start with the base query and filtering
            var lastMovementsQuery = _context.AssetMovement
                .Where(m => assetIds.Contains(m.AssetId));

            // 2. Apply security/facility filtering
            if (!isAdmin)
            {
                var userFacilityId = currentUser.FacilityId;
                lastMovementsQuery = lastMovementsQuery.Where(m => m.FacilityId == userFacilityId);
            }

            // 3. Apply sorting and includes LAST
            var lastMovements = lastMovementsQuery
                .Include(m => m.Facility)
                .Include(m => m.ServicePoint)
                .OrderByDescending(m => m.MovementDate) // Sorting happens here
                .ToList();

            var lastMovementDict = lastMovements
                .GroupBy(m => m.AssetId)
                .ToDictionary(
                    g => g.Key,
                    g => g.FirstOrDefault());

            var items = assets.Select(asset => new AssetViewModel
            {
                Asset = asset,
                LastMovement = lastMovementDict.TryGetValue(asset.AssetId, out var move)
                    ? move
                    : null
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
