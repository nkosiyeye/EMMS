using EMMS.Data;
using EMMS.Models.Admin;
using EMMS.Models.Pagination;
using EMMS.ViewModels;

namespace EMMS.Service
{
    

    public interface IAssetService
    {
        PaginatedResult<AssetViewModel> GetPaginatedAssets(int start, int length, string search,User currentUser);
    }

}
