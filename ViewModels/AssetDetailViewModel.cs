using EMMS.Models;

namespace EMMS.ViewModels
{
    public class AssetDetailViewModel
    {
        public Asset Asset { get; set; }
        public IEnumerable<Job>? ServiceHistory { get; set; } // or a custom DTO
        public IEnumerable<MoveAsset>? MovementHistory { get; set; } // or a custom DTO
    }

}
