using EMMS.Models;

namespace EMMS.ViewModels
{
    public class AssetViewModel
    {
        public Asset Asset { get; set; }
        public string? SubCategoryName { get; set; }
        public MoveAsset? LastMovement { get; set; }      // current location (vw_AssetCurrentLocation)
        public MoveAsset? PendingMovement { get; set; }   // set when the asset is in transit (vw_AssetInTransit)
        public int? daysOverDue { get; set; }
    }

}
