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

        public PaginatedResult<MoveAsset> GetPaginatedMovements(
            int start, int length, string search, User currentUser,
            Dictionary<string, string> columnFilters = null)
        {
            var query = _context.AssetMovement
                        .Where(x => x.RowState == RowStatus.Active)
                        .Include(x => x.Asset)
                        .ThenInclude(x => x.SubCategory)
                        .Include(x => x.From)
                        .Include(x => x.Facility)
                        .Include(x => x.ServicePoint)
                        .Include(x => x.Condition)
                        .AsQueryable();

            bool isAdmin = currentUser?.UserRole?.UserType == Enumerators.UserType.Administrator;

            // If NOT admin → only show movements leaving the user's facility
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

                // Narrow the query above (keeps its Includes; lazy loading is off)
                query = query.Where(x => x.FromId == userFacilityId);
            }

            int totalCount = query.Count();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();

                // Reason is stored as a number; match the search against the enum names in memory
                var matchingReasons = Enum.GetValues<MovementReason>()
                    .Where(r => r.ToString().Contains(search, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                query = query.Where(m =>
                    (m.Asset != null && (m.Asset.AssetTagNumber.Contains(search) ||
                                         (m.Asset.SubCategory != null && m.Asset.SubCategory.Name.Contains(search)))) ||
                    (m.From != null && m.From.FacilityName.Contains(search)) ||
                    (m.Facility != null && m.Facility.FacilityName.Contains(search)) ||
                    (m.ServicePoint != null && m.ServicePoint.Name.Contains(search)) ||
                    matchingReasons.Contains(m.Reason) ||
                    (m.OtherReason != null && m.OtherReason.Contains(search))
                );
            }

            // Per-column filters from the header dropdowns. Values are facility / lookup ids, enum values
            // or fixed keys (see AssetMovementController.GetFilterOptions). Applied before filteredCount and paging.
            if (columnFilters != null)
            {
                foreach (var (name, value) in columnFilters)
                {
                    int.TryParse(value, out var id);

                    switch (name)
                    {
                        case "FromFacility":
                            query = query.Where(m => m.FromId == id);
                            break;

                        case "ToFacility":
                            query = query.Where(m => m.FacilityId == id);
                            break;

                        case "ServicePoint":
                            // "none" = no service point (shown as "Unknown")
                            query = value == "none"
                                ? query.Where(m => m.ServicePointId == null)
                                : query.Where(m => m.ServicePointId == id);
                            break;

                        case "FunctionalStatus":
                            if (Enum.TryParse<FunctionalStatus>(value, out var status) && Enum.IsDefined(status))
                                query = query.Where(m => m.FunctionalStatus == status);
                            break;

                        case "Reason":
                            // "Other" covers every free-text OtherReason as well
                            if (Enum.TryParse<MovementReason>(value, out var reason) && Enum.IsDefined(reason))
                                query = query.Where(m => m.Reason == reason);
                            break;

                        case "Approved":
                            var approved = value == "true";
                            query = query.Where(m => m.IsApproved == approved);
                            break;

                        case "Received":
                            // Mirrors the Received Status column: Rejected wins over Received, else Pending
                            query = value switch
                            {
                                "rejected" => query.Where(m => m.DateRejected != null),
                                "received" => query.Where(m => m.DateRejected == null && m.DateReceived != null),
                                "pending" => query.Where(m => m.DateRejected == null && m.DateReceived == null),
                                _ => query
                            };
                            break;
                    }
                }
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
