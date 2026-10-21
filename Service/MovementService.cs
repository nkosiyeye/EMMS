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

                query = _context.AssetMovement
                        .Where(x => x.RowState == RowStatus.Active && x.FromId == userFacilityId)
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

            // NEW: per-column filters from the header dropdowns.
            // Applied BEFORE filteredCount and BEFORE paging.
            // Comparison values must match what the controller puts in the JSON,
            // since the dropdown options are built from the rendered page.
            if (columnFilters != null)
            {
                foreach (var (name, value) in columnFilters)
                {
                    switch (name)
                    {
                        case "FromFacility":
                            // "-" means no From set — not a filterable option unless listed
                            query = value == "-"
                                ? query.Where(m => m.From == null)
                                : query.Where(m => m.From.FacilityName == value);
                            break;

                        case "ToFacility":
                            query = value == "-"
                                ? query.Where(m => m.Facility == null)
                                : query.Where(m => m.Facility.FacilityName == value);
                            break;

                        case "ServicePoint":
                            // "Unknown" is the display fallback for a null service point
                            query = value == "Unknown"
                                ? query.Where(m => m.ServicePoint == null)
                                : query.Where(m => m.ServicePoint.Name == value);
                            break;

                        case "Reason":
                            // SQL-translatable: compare enum constants directly.
                            // The column shows OtherReason text when Reason == Other; mirror that:
                            if (value == "Other")
                            {
                                // "Other" as rendered in the dropdown is actually the OtherReason text,
                                // or the literal "Other" fallback when OtherReason is null.
                                query = query.Where(m => m.Reason == MovementReason.Other
                                    && (m.OtherReason == null || m.OtherReason == value));
                            }
                            else
                            {
                                // Parse the display text back into the enum and match the column.
                                // Enum.TryParse fails silently to false for unmapped strings.
                                if (Enum.TryParse<MovementReason>(value, out var reason))
                                {
                                    query = query.Where(m => m.Reason == reason);
                                }
                                else
                                {
                                    // Not an enum name — can only be an Other-reason text value
                                    query = query.Where(m => m.Reason == MovementReason.Other
                                        && m.OtherReason == value);
                                }
                            }
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
