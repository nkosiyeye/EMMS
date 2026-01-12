using EMMS.Models;

namespace EMMS.ViewModels
{
    public class AssetIndexViewModel
    {
        public IEnumerable<AssetViewModel>? assetViewModels { get; set; }
        public AssetRegistrationViewModel? assetRegistrationViewModel { get; set; }

        public MoveAsset? moveAsset { get; set; }


    }
}
