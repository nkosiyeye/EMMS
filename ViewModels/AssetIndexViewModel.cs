using EMMS.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace EMMS.ViewModels
{
    public class AssetIndexViewModel
    {
        public IEnumerable<AssetViewModel>? assetViewModels { get; set; }
        public AssetRegistrationViewModel? assetRegistrationViewModel { get; set; }

        public MoveAsset? moveAsset { get; set; }

        // Header dropdown options on the Manage Assets table, keyed by DataTables column name
        public Dictionary<string, List<SelectListItem>> FilterOptions { get; set; } = new();


    }
}
