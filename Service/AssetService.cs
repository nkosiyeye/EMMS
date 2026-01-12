using EMMS.Data.Repository;
using EMMS.Data;
using EMMS.Models.Admin;
using EMMS.Models;
using EMMS.ViewModels;
using Microsoft.EntityFrameworkCore;
using EMMS.Data.Migrations;
using Microsoft.Extensions.Caching.Memory;

namespace EMMS.Service
{
    public class AssetService
    {
        private readonly ApplicationDbContext _context;
        private readonly AssetManagementRepo _repo;
        private readonly IMemoryCache _cache;

        public AssetService(ApplicationDbContext context, IMemoryCache cache,AssetManagementRepo repo)
        {
            _context = context;
            _repo = repo;
        }
        public async Task<AssetIndexViewModel?> GetAssetIndexViewModel(User currentUser)
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

                assetViewModels = assetViewModels
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
    }
}
