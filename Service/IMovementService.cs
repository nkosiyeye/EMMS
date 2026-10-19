using EMMS.Models;
using EMMS.Models.Admin;
using EMMS.Models.Pagination;
using EMMS.ViewModels;

namespace EMMS.Service
{
    public interface IMovementService
    {
        PaginatedResult<MoveAsset> GetPaginatedMovements(int start, int length, string search, User currentUser);
    }
}
