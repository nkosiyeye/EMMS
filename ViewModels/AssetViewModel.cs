using EMMS.Models;

namespace EMMS.ViewModels
{
    public class AssetViewModel
    {
        public Asset Asset { get; set; }
        public MoveAsset? LastMovement { get; set; }
    }

}
