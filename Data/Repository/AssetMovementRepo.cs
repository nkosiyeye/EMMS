using EMMS.Models.Entities;
using EMMS.Models;
using static EMMS.Models.Enumerators;
using Microsoft.EntityFrameworkCore;
using EMMS.Data.Migrations;
using EMMS.Models.Admin;

namespace EMMS.Data.Repository
{
    public class AssetMovementRepo
    {
        private readonly ApplicationDbContext _context;
        public AssetMovementRepo(ApplicationDbContext context)
        {
            _context = context;
        }
        public async Task<IEnumerable<MoveAsset>> GetAssetMovement()
        {
            var query = _context.AssetMovement
                        .Where(x => x.RowState == RowStatus.Active);

            var moveAssets = await query
                .Include(x => x.Asset)
                .Include(x => x.From)
                .Include(x => x.Facility)
                .Include(x => x.ServicePoint)
                .Include(x => x.Condition)
                .OrderByDescending(m => m.DateCreated)
                .ThenByDescending(m => m.IsApproved)
                .ToListAsync();

            return moveAssets;
        }

        public async Task<MoveAsset?> GetLastMovement(Guid assetId)
        {
            return await _context.AssetMovement
                .Where(x => x.AssetId == assetId && x.DateRejected == null && x.RowState == RowStatus.Active)
                .Include(x => x.Asset)
                .OrderByDescending(x => x.DateCreated)
                .FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<Facility>> GetFacilities()
        {
            return await _context.Facilities
                .Where(x => x.RowState == RowStatus.Active)
                .ToListAsync();
        }

        public async Task<IEnumerable<LookupItem>> GetServicePoints()
        {
            return await _context.LookupItems
                .Where(x => x.LookupList.Name == "Service Point" && x.RowState == RowStatus.Active)
                .ToListAsync();
        }

        public async Task<IEnumerable<LookupItem>> GetReasons()
        {
            return await _context.LookupItems
                .Where(x => x.LookupList.Name == "Reason" && x.RowState == RowStatus.Active)
                .ToListAsync();
        }

        public async Task<IEnumerable<LookupItem>> GetFunctionalStatuses()
        {
            return await _context.LookupItems
                .Where(x => x.LookupList.Name == "Functional Status" && x.RowState == RowStatus.Active)
                .ToListAsync();
        }

        public async Task<IEnumerable<LookupItem>> GetConditions()
        {
            return await _context.LookupItems
                .Where(x => x.LookupList.Name == "Condition" && x.RowState == RowStatus.Active)
                .ToListAsync();
        }
    }
}
