using EMMS.Models;

namespace EMMS.ViewModels
{
    public class AssetIndexViewModel
    {
        public IEnumerable<AssetViewModel>? assetViewModels { get; set; }
        public MoveAsset? moveAsset { get; set; }


    }
}
