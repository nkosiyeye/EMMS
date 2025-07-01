using EMMS.Data.Migrations;
using EMMS.Models;
using EMMS.Models.Entities;
using Microsoft.EntityFrameworkCore;
using static EMMS.Models.Enumerators;

namespace EMMS.Data.Repository
{
    public class AssetManagementRepo
    {
        private readonly ApplicationDbContext _context;
        public AssetManagementRepo(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Asset>> GetAssetsFromDb()
        {
            return await _context.Assets
                .Include(x => x.Category)
                .Include(x => x.SubCategory)
                .Include(x => x.Department)
                .Include(x => x.Manufacturer)
                .Include(x => x.Vendor)
                .Include(x => x.ServiceProvider)
                .Include(x => x.ServicePeriodName)
                .Include(x => x.Status)
                .ToListAsync();
        }
        public async Task<List<Models.Entities.Notification>> GetNotifications()
        {
            return await _context.Notifications
            .OrderByDescending(n => n.DateCreated)
            .ToListAsync();
        }

        public async Task<List<MoveAsset?>> GetAssetMovement()
        {
            return await _context.AssetMovement
                                .Include(m => m.Facility)
                                .Include(m => m.ServicePoint)
                                .Include(m => m.FunctionalStatus)
                                .Include(m => m.Reason)
                                .GroupBy(m => m.AssetId)
                                .Select(g => g.OrderByDescending(m => m.MovementDate).FirstOrDefault(g => g.DateReceived != null))
                                .ToListAsync();
        }
        public async Task<IEnumerable<LookupItem>> GetCategories()
        {
            var categories = await _context.LookupItems
                                .Where(x => x.LookupList.Name == "Category" && x.RowState == RowStatus.Active)
                                .ToListAsync();
            return categories;
        }
        public async Task<IEnumerable<LookupItem>> GetSubCategories()
        {
            return await _context.LookupItems
                .Where(x => x.LookupList.Name == "SubCategory" && x.RowState == RowStatus.Active)
                .ToListAsync();
        }

        public async Task<IEnumerable<LookupItem>> GetDepartments()
        {
            return await _context.LookupItems
                .Where(x => x.LookupList.Name == "Department" && x.RowState == RowStatus.Active)
                .ToListAsync();
        }

        public async Task<IEnumerable<LookupItem>> GetManufacturers()
        {
            return await _context.LookupItems
                .Where(x => x.LookupList.Name == "Manufacturer" && x.RowState == RowStatus.Active)
                .ToListAsync();
        }

        public async Task<IEnumerable<LookupItem>> GetVendors()
        {
            return await _context.LookupItems
                .Where(x => x.LookupList.Name == "Vendor" && x.RowState == RowStatus.Active)
                .ToListAsync();
        }

        public async Task<IEnumerable<LookupItem>> GetServiceProviders()
        {
            return await _context.LookupItems
                .Where(x => x.LookupList.Name == "Service Provider" && x.RowState == RowStatus.Active)
                .ToListAsync();
        }

        public async Task<IEnumerable<LookupItem>> GetStatuses()
        {
            return await _context.LookupItems
                .Where(x => x.LookupList.Name == "Status" && x.RowState == RowStatus.Active)
                .ToListAsync();
        }

        public async Task<IEnumerable<LookupItem>> GetUnitOfMeasures()
        {
            return await _context.LookupItems
                .Where(x => x.LookupList.Name == "Unit of Measure" && x.RowState == RowStatus.Active)
                .ToListAsync();
        }

        public async Task<IEnumerable<LookupItem>> GetLifespanPeriods()
        {
            return await _context.LookupItems
                .Where(x => x.LookupList.Name == "Period" && x.RowState == RowStatus.Active)
                .ToListAsync();
        }
    }
}
