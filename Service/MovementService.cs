using EMMS.Data.Repository;
using EMMS.Data;
using EMMS.Models.Admin;
using EMMS.Models.Pagination;
using EMMS.ViewModels;
using Microsoft.Extensions.Caching.Memory;
using EMMS.Models;
using Microsoft.EntityFrameworkCore;
using static EMMS.Models.Enumerators;

namespace EMMS.Service
{
    public class MovementService : IMovementService
    {
        private readonly ApplicationDbContext _context;

        public MovementService(ApplicationDbContext context)
        {
            _context = context;
        }
        public PaginatedResult<MoveAsset> GetPaginatedMovements(int start, int length, string search, User currentUser)
        {
            var query = _context.AssetMovement
                        .Where(x => x.RowState == RowStatus.Active)
                        .Include(x => x.Asset)
                        .ThenInclude(x => x.SubCategory)
                        .Include(x => x.From)
                        .Include(x => x.Facility)
                        .Include(x => x.ServicePoint)
                        .Include(x => x.Condition)
                        .OrderByDescending(m => m.MovementDate)
                        .AsQueryable();

            bool isAdmin = currentUser?.UserRole?.UserType == Enumerators.UserType.Administrator;

            // If NOT admin → only show assets currently located in the user's facility
            if (!isAdmin)
            {
                if (currentUser?.FacilityId == null)
                {
                    return new PaginatedResult<MoveAsset>
                    {
                        TotalCount = 0,
                        FilteredCount = 0,
                        Items = new List<MoveAsset>()
                    };
                }

                var userFacilityId = currentUser.FacilityId;

                // Filter assets whose *latest* movement is in the user's facility
                query = _context.AssetMovement
                        .Where(x => x.RowState == RowStatus.Active && x.FromId == userFacilityId)
                        .Include(x => x.Asset)
                        .ThenInclude(x => x.SubCategory)
                        .Include(x => x.From)
                        .Include(x => x.Facility)
                        .Include(x => x.ServicePoint)
                        .Include(x => x.Condition)
                        .OrderByDescending(m => m.MovementDate)
                        .AsQueryable();
            }

            int totalCount = query.Count();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(m =>
                    (m.Asset != null && m.Asset.AssetTagNumber.Contains(search)) ||
                    (m.From != null && m.From.FacilityName.Contains(search)) ||
                    (m.Facility != null && m.Facility.FacilityName.Contains(search)) ||
                    (m.ServicePoint != null && m.ServicePoint.Name.Contains(search)) ||
                    m.Reason.ToString().Contains(search) ||
                    (m.OtherReason != null && m.OtherReason.Contains(search))
                );
            }

            int filteredCount = query.Count();

            var movements = query
                .OrderByDescending(a => a.MovementDate)
                .Skip(start)
                .Take(length)
                .ToList();

            return new PaginatedResult<MoveAsset>
            {
                TotalCount = totalCount,
                FilteredCount = filteredCount,
                Items = movements
            };

        }
    }
}
