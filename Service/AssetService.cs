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
            // Fetch assets asynchronously and without change tracking
            var assets = await _repo.GetAssetsFromDb()
                .ConfigureAwait(false);

            
            var orderedAssets = assets
                .OrderByDescending(a => a.DateCreated)
                .ToList();

            var lastMovements = await _repo.GetAssetMovement()
                .ConfigureAwait(false);

            var lastMovementDict = lastMovements?
                .Where(m => m != null)
                .ToDictionary(m => m.AssetId, m => m)
                ?? new Dictionary<Guid, MoveAsset>();

            
            var assetViewModels = orderedAssets.Select(asset => new AssetViewModel
            {
                Asset = asset,
                LastMovement = lastMovementDict.TryGetValue(asset.AssetId, out var move) ? move : null
            }).ToList();

            
            if (currentUser?.UserRole?.UserType != Enumerators.UserType.Administrator)
            {
                assetViewModels = assetViewModels
                    .Where(l =>
                        (l.LastMovement != null && l.LastMovement.FacilityId == currentUser!.FacilityId) ||
                        l.Asset.CreatedBy == currentUser!.UserId)
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
