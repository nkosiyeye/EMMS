using EMMS.Data.Repository;
using EMMS.Data;
using EMMS.Models.Admin;
using EMMS.Models;
using EMMS.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using EMMS.Models.Pagination;
using static EMMS.Models.Enumerators;

namespace EMMS.Service
{
    public class AssetService : IAssetService
    {
        private readonly ApplicationDbContext _context;
        private readonly AssetManagementRepo _repo;
        private readonly IMemoryCache _cache;

        public AssetService(ApplicationDbContext context, IMemoryCache cache,AssetManagementRepo repo)
        {
            _context = context;
            _repo = repo;
        }
        // The assets a user may pick from (work request asset picker), newest first, scoped in SQL: admins see every
        // asset; data collectors only those they registered; everyone else those currently at their facility or
        // registered by them.
        public async Task<AssetIndexViewModel> GetAssetIndexViewModel(User currentUser)
        {
            var query = _context.Assets
                .AsNoTracking()
                .Where(a => a.RowState == RowStatus.Active);

            var role = currentUser.UserRole?.UserType;
            var userId = currentUser.UserId;
            var userFacilityId = currentUser.FacilityId;

            if (role == UserType.DataCollector)
                query = query.Where(a => a.CreatedBy == userId);
            else if (role != UserType.Administrator)
                query = query.Where(a => a.CreatedBy == userId ||
                    _context.AssetCurrentLocations.Any(l => l.AssetId == a.AssetId && l.FacilityId == userFacilityId));

            var assets = await query
                .Include(x => x.Category)
                .Include(x => x.SubCategory)
                .Include(x => x.Department)
                .Include(x => x.Manufacturer)
                .Include(x => x.Vendor)
                .Include(x => x.ServiceProvider)
                .OrderByDescending(a => a.DateCreated)
                .ToListAsync();

            // Current location of just these assets (the id query runs as a SQL subquery)
            var locations = await _repo.GetCurrentLocationMovements(query.Select(a => a.AssetId));

            return new AssetIndexViewModel
            {
                assetViewModels = assets.Select(asset => new AssetViewModel
                {
                    Asset = asset,
                    LastMovement = locations.GetValueOrDefault(asset.AssetId)
                }).ToList(),
                moveAsset = new MoveAsset()
            };
        }

        // Asset page: the asset, its job and movement history, where it is now / is heading, and its status log
        public async Task<AssetDetailViewModel?> GetAssetDetailAsync(Guid id)
        {
            var asset = await _context.Assets
                .AsNoTracking()
                .Include(a => a.Category)
                .Include(a => a.SubCategory)
                .Include(a => a.Department)
                .Include(a => a.Manufacturer)
                .Include(a => a.Vendor)
                .Include(a => a.ServiceProvider)
                .Include(u => u.User)
                .Include(a => a.CylinderDetail!.Size)
                .Include(a => a.CylinderDetail!.Content)
                .FirstOrDefaultAsync(a => a.AssetId == id);
            if (asset == null) return null;

            var serviceHistory = await _context.Job
                .AsNoTracking()
                .Where(j => j.AssetId == id)
                .Include(j => j.FaultReport)
                .Include(j => j.Status)
                .OrderByDescending(j => j.DateCreated)
                .ToListAsync();

            var movementHistory = await _context.AssetMovement
                .AsNoTracking()
                .Where(m => m.AssetId == id && m.RowState == RowStatus.Active)
                .Where(m => m.DateReceived != null || m.DateRejected != null)
                .Include(m => m.Facility)
                .Include(m => m.ServicePoint)
                .Include(m => m.RecievedUser)
                .Include(m => m.RejectedUser)
                .OrderByDescending(m => m.MovementDate)
                .ToListAsync();

            return new AssetDetailViewModel
            {
                Asset = asset,
                ServiceHistory = serviceHistory,
                MovementHistory = movementHistory,
                CurrentLocation = (await _repo.GetCurrentLocationMovements(new[] { id })).GetValueOrDefault(id),
                PendingMovement = (await _repo.GetInTransitMovements(new[] { id })).GetValueOrDefault(id),
                StatusHistory = await AssetStatusTracker.GetHistoryAsync(_context, id)
            };
        }

        // Active asset with its lookups and cylinder detail, for the edit form
        public async Task<Asset?> GetAssetForEditAsync(Guid id)
        {
            return await _context.Assets
                .AsNoTracking()
                .Include(x => x.Category)
                .Include(x => x.SubCategory)
                .Include(x => x.Department)
                .Include(x => x.Manufacturer)
                .Include(x => x.Vendor)
                .Include(x => x.ServiceProvider)
                .Include(a => a.CylinderDetail!.Size)
                .Include(a => a.CylinderDetail!.Content)
                .FirstOrDefaultAsync(a => a.AssetId == id && a.RowState == RowStatus.Active);
        }

        // Sticker sheet: active assets with these tags, in the order given (unknown tags are skipped)
        public async Task<List<Asset>> GetAssetsByTagsAsync(IEnumerable<string?> tags)
        {
            var wanted = tags
                .Select(t => t?.Trim())
                .Where(t => !string.IsNullOrEmpty(t))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var assets = await _context.Assets
                .AsNoTracking()
                .Include(a => a.SubCategory)
                .Include(a => a.CylinderDetail!.Size)
                .Where(a => wanted.Contains(a.AssetTagNumber) && a.RowState == RowStatus.Active)
                .ToListAsync();

            var byTag = assets.ToDictionary(a => a.AssetTagNumber, StringComparer.OrdinalIgnoreCase);
            return wanted.Where(byTag.ContainsKey).Select(t => byTag[t!]).ToList();
        }

        public PaginatedResult<AssetViewModel> GetPaginatedAssets(
            int start, int length, string search, User currentUser,
            Dictionary<string, string> columnFilters = null)
        {
            var query = _context.Assets
                .Include(a => a.Category)
                .Include(a => a.SubCategory)
                .Include(a => a.Vendor)
                .Include(a => a.ServiceProvider)
                .AsQueryable();

            bool isAdmin = currentUser?.UserRole?.UserType == Enumerators.UserType.Administrator;

            // If NOT admin → only show assets currently located in the user's facility
            if (!isAdmin)
            {
                if (currentUser?.FacilityId == null)
                {
                    return new PaginatedResult<AssetViewModel>
                    {
                        TotalCount = 0,
                        FilteredCount = 0,
                        Items = new List<AssetViewModel>()
                    };
                }

                var userFacilityId = currentUser.FacilityId;

                // Assets whose current location (vw_AssetCurrentLocation) is the user's facility
                query = query.Where(a => _context.AssetCurrentLocations.Any(l => l.AssetId == a.AssetId && l.FacilityId == userFacilityId));
            }

            int totalCount = query.Count();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();
                query = query.Where(a =>
                    a.AssetTagNumber.Contains(search) ||
                    a.SerialNumber.Contains(search) ||
                    a.Model.Contains(search) ||
                    a.SubCategory.Name.Contains(search) ||
                    a.Category.Name.Contains(search) ||
                    a.Vendor.Name.Contains(search) ||
                    a.ServiceProvider.Name.Contains(search) ||
                    _context.AssetCurrentLocations.Any(l => l.AssetId == a.AssetId && (
                        _context.Facilities.Any(f => f.FacilityId == l.FacilityId && f.FacilityName.Contains(search)) ||
                        _context.LookupItems.Any(sp => sp.Id == l.ServicePointId && sp.Name.Contains(search)))));
            }

            // Per-column filters from the header dropdowns. Values are lookup/facility ids or enum values
            // (see AssetManagementController.GetFilterOptions). Applied before filteredCount and paging.
            if (columnFilters != null)
            {
                foreach (var (name, value) in columnFilters)
                {
                    int.TryParse(value, out var id);

                    switch (name)
                    {
                        case "Category":
                            query = query.Where(a => a.CategoryId == id);
                            break;

                        case "ItemName":
                            query = query.Where(a => a.SubCategoryId == id);
                            break;

                        case "Placement":
                            var isPlacement = value == "true";
                            query = query.Where(a => a.IsPlacement == isPlacement);
                            break;

                        case "Vendor":
                            query = query.Where(a => a.VendorId == id);
                            break;

                        case "Service Provider":
                            query = query.Where(a => a.ServiceProviderId == id);
                            break;

                        case "Procurement":
                            query = query.Where(a => a.StatusId == id);
                            break;

                        case "Facility":
                            // Facility shown in the table = current location's facility
                            query = query.Where(a => _context.AssetCurrentLocations
                                .Any(l => l.AssetId == a.AssetId && l.FacilityId == id));
                            break;

                        case "ServicePoint":
                            query = query.Where(a => _context.AssetCurrentLocations
                                .Any(l => l.AssetId == a.AssetId && l.ServicePointId == id));
                            break;

                        case "Status":
                            query = ApplyStatusFilter(query, value);
                            break;
                    }
                }
            }

            int filteredCount = query.Count();

            var assets = query
                .OrderByDescending(a => a.DateCreated)
                .Skip(start)
                .Take(length)
                .ToList();

            // Current location and any pending movement for this page, from the two location views
            var assetIds = assets.Select(a => a.AssetId).ToList();

            var locationMovementIds = _context.AssetCurrentLocations
                .Where(l => assetIds.Contains(l.AssetId))
                .Select(l => l.MovementId);
            var lastMovementDict = _context.AssetMovement
                .AsNoTracking()
                .Where(m => locationMovementIds.Contains(m.MovementId))
                .Include(m => m.Facility)
                .Include(m => m.ServicePoint)
                .ToDictionary(m => m.AssetId);

            var pendingMovementIds = _context.AssetsInTransit
                .Where(t => assetIds.Contains(t.AssetId))
                .Select(t => t.MovementId);
            var pendingDict = _context.AssetMovement
                .AsNoTracking()
                .Where(m => pendingMovementIds.Contains(m.MovementId))
                .Include(m => m.Facility)
                .Include(m => m.ServicePoint)
                .ToDictionary(m => m.AssetId);

            var items = assets.Select(asset => new AssetViewModel
            {
                Asset = asset,
                LastMovement = lastMovementDict.TryGetValue(asset.AssetId, out var move)
                    ? move
                    : null,
                PendingMovement = pendingDict.TryGetValue(asset.AssetId, out var pending) ? pending : null
            }).OrderByDescending(m => m.Asset.DateCreated).ToList();

            return new PaginatedResult<AssetViewModel>
            {
                TotalCount = totalCount,
                FilteredCount = filteredCount,
                Items = items
            };
        }

        // Status filter value for non-functional or under maintenance (the dashboard's "out of service" bars link to it)
        public const string OutOfServiceFilter = "outofservice";

        // Mirrors the Status column in AssetManagementController.GetAssets: the pending movement (else the current
        // location) decides; a Decommission reason shows "Decommissioned", otherwise the asset's functional status.
        // Assets with no movement show "-" and match no status.
        private IQueryable<Asset> ApplyStatusFilter(IQueryable<Asset> query, string value)
        {
            if (value == "awaiting")
                return query.Where(a => _context.AssetsInTransit.Any(t => t.AssetId == a.AssetId && !t.IsApproved));
            if (value == "intransit")
                return query.Where(a => _context.AssetsInTransit.Any(t => t.AssetId == a.AssetId && t.IsApproved));

            if (value == "decommissioned")
                return query.Where(a =>
                    _context.AssetsInTransit.Any(t => t.AssetId == a.AssetId &&
                        _context.AssetMovement.Any(m => m.MovementId == t.MovementId && m.Reason == MovementReason.Decommission)) ||
                    (!_context.AssetsInTransit.Any(t => t.AssetId == a.AssetId) &&
                        _context.AssetCurrentLocations.Any(l => l.AssetId == a.AssetId && l.Reason == MovementReason.Decommission)));

            FunctionalStatus[] statuses;
            if (value == OutOfServiceFilter)
                statuses = new[] { FunctionalStatus.NonFunctional, FunctionalStatus.UnderMaintenance };
            else if (Enum.TryParse<FunctionalStatus>(value, out var status) && Enum.IsDefined(status))
                statuses = new[] { status };
            else
                return query;

            return query.Where(a => statuses.Contains(a.FunctionalStatus) &&
                (
                    _context.AssetsInTransit.Any(t => t.AssetId == a.AssetId &&
                        _context.AssetMovement.Any(m => m.MovementId == t.MovementId && m.Reason != MovementReason.Decommission)) ||
                    (!_context.AssetsInTransit.Any(t => t.AssetId == a.AssetId) &&
                        _context.AssetCurrentLocations.Any(l => l.AssetId == a.AssetId && l.Reason != MovementReason.Decommission))
                ));
        }

    }
}
