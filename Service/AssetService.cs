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
            var assets = await _repo.GetAssetsFromDb();
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
                assetViewModels = assetViewModels
                    .Where(l =>
                        (l.LastMovement != null && l.LastMovement.FacilityId == currentUser.FacilityId)
                        || l.Asset.CreatedBy == currentUser.UserId
                    ).ToList(),
                moveAsset = new MoveAsset()
            };

            return indexView;
        }
    }

}
