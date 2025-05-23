using EMMS.Models;
using EMMS.Models.Entities;

namespace EMMS.ViewModels
{
    public class MoveAssetViewModel
    {
        public IEnumerable<MoveAsset>? MoveAssets { get; set; }
        public MoveAsset MoveAsset { get; set; } = new MoveAsset();
        public AssetIndexViewModel? AssetIndex { get; set; }

        // Lookup collections for dropdowns
        public IEnumerable<Asset>? Assets { get; set; }

        public IEnumerable<LookupItem>? Conditions { get; set; }
    }
}
