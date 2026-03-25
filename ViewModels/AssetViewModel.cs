using EMMS.Models;

namespace EMMS.ViewModels
{
    public class AssetViewModel
    {
        public Asset Asset { get; set; }
        public string? SubCategoryName { get; set; }
        public MoveAsset? LastMovement { get; set; }
        public int? daysOverDue { get; set; }
    }

}
