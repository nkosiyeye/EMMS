using EMMS.Data.Repository;
using EMMS.Data;
using EMMS.Models.Admin;
using EMMS.Models;
using EMMS.ViewModels;

namespace EMMS.Service
{
    public class AssetService
    {
        private readonly ApplicationDbContext _context;

        public AssetService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<AssetIndexViewModel?> GetAssetIndexViewModel(User currentUser)
        {
            var _repo = new AssetManagementRepo(_context);
            var assets = _repo.GetAssetsFromDb().Result.OrderByDescending(a => a.DateCreated);
            var lastMovements = await _repo.GetAssetMovement();

            var lastMovementDict = lastMovements
                .Where(m => m != null)
                .ToDictionary(m => m.AssetId, m => m);

            var assetViewModels = assets.Select(asset => new AssetViewModel
            {
                Asset = asset,
                LastMovement = lastMovementDict.ContainsKey(asset.AssetId) ? lastMovementDict[asset.AssetId] : null
            }).ToList();

            var indexView = new AssetIndexViewModel
            {
                assetViewModels = currentUser!.UserRole!.UserType != Enumerators.UserType.Administrator ? assetViewModels
                    .Where(l => 
                        (l.LastMovement != null && l.LastMovement.FacilityId == currentUser.FacilityId)
                        || l.Asset.CreatedBy == currentUser.UserId
                    ).ToList() : assetViewModels,
                moveAsset = new MoveAsset()
            };

            return indexView;
        }

        public async Task<AssetIndexViewModel?> GetAssetDueServiceViewModel()
        {
            var _repo = new AssetManagementRepo(_context);
            var assets = _repo.GetAssetsDueService().Result.OrderByDescending(a => a.NextServiceDate);
            var lastMovements = await _repo.GetAssetMovement();

            var lastMovementDict = lastMovements
                .Where(m => m != null)
                .ToDictionary(m => m.AssetId, m => m);

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
    }

}
