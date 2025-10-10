using EMMS.Data.Migrations;
using EMMS.Models;
using EMMS.Models.Entities;
using Microsoft.EntityFrameworkCore;
using static EMMS.Models.Enumerators;

namespace EMMS.Data.Repository
{
    public class JobManagementRepo
    {
        private readonly ApplicationDbContext _context;
        public JobManagementRepo(ApplicationDbContext context)
        {
            _context = context;
        }
        public async Task<IEnumerable<WorkRequest>> GetWorkRequests()
        {
            return await _context.WorkRequest
                .Include(w => w.Asset)
                .ThenInclude(a => a.SubCategory)
                .Include(w => w.WorkStatus)
                .Include(w => w.Outcome)
                .Include(w => w.RequestedByUser)
                .Include(w => w.Job)
                .Include(w => w.CancelReason)
                .OrderByDescending(w => w.RequestDate)
                .ToListAsync();
        }
        public async Task<IEnumerable<EMMS.Models.InfrustructureWorkRequest>> GetInfrustructureWorkRequests()
        {
            return await _context.InfrustructureWorkRequest
                .Include(w => w.TypeOfRequest)
                .Include(w => w.WorkStatus)
                .Include(w => w.Outcome)
                .Include(w => w.RequestedByUser)
                .Include(w => w.Job)
                .Include(w => w.CancelReason)
                .OrderByDescending(w => w.RequestDate)
                .ToListAsync();
        }
        public async Task<WorkRequest?> GetWorkRequestByAssetId(Guid assetId)
        {
            return await _context.WorkRequest.Include(w => w.WorkStatus).Where(w => w.AssetId == assetId && (w.WorkStatus.Name == "Open" || w.WorkStatus.Name == "In Progress")).FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<Job>> GetJobfromDbs()
        {
            return await _context.Job
                .Include(w => w.Asset)
                .Include(w => w.InfraWorkRequest)
                .ThenInclude(w => w.TypeOfRequest)
                .Include(w => w.User)
                .Include(w => w.Status)
                .Include(w => w.FaultReport)
                .Include(w => w.ExternalProvider)
                .OrderByDescending(w => w.StartDate)
                .ToListAsync();
        }
        public async Task<int> GetJobsCount(int facilityId, bool completed, bool isAdmin)
        {
            var query = _context.Job.AsQueryable();

            if (completed)
                query = query.Where(j => j.EndDate != null);
            else
                query = query.Where(j => j.EndDate == null);

            if (!isAdmin)
                query = query.Where(j => j.FacilityId == facilityId);

            return await query.CountAsync();
        }
        public async Task<List<WorkRequest>> GetOpenWorkRequestsByFacility(int? facilityId = null)
        {
            var query = _context.WorkRequest
                .AsNoTracking()
                .Where(w => w.WorkStatus.Name == "Open");

            if (facilityId.HasValue)
                query = query.Where(w => w.FacilityId == facilityId.Value);

            return await query.ToListAsync();
        }

        public async Task<List<EMMS.Models.InfrustructureWorkRequest>> GetOpenInfraWorkRequestsByFacility(int? facilityId = null)
        {
            var query = _context.InfrustructureWorkRequest
                .AsNoTracking()
                .Where(w => w.WorkStatus.Name == "Open");

            if (facilityId.HasValue)
                query = query.Where(w => w.FacilityId == facilityId.Value);

            return await query.ToListAsync();
        }



        public async Task<IEnumerable<WorkDone>> GetWorkDone()
        {
            return await _context.WorkDone
                .OrderByDescending(w => w.DateCompleted)
                .ToListAsync();
        }

        public async Task<IEnumerable<LookupItem>> GetFaultReports()
        {
            return await _context.LookupItems
                .Where(x => x.LookupList.Name == "WorkRequestFaultReport" && x.RowState == RowStatus.Active)
                .ToListAsync();
        }
        public async Task<IEnumerable<LookupItem>> GetWorkStatus()
        {
            return await _context.LookupItems
                .Where(x => x.LookupList.Name == "WorkRequestStatus" && x.RowState == RowStatus.Active)
                .ToListAsync();
        }
        public async Task<IEnumerable<LookupItem>> GetTypeOfRequest()
        {
            return await _context.LookupItems
                .Where(x => x.LookupList.Name == "TypeOfRequest" && x.RowState == RowStatus.Active)
                .ToListAsync();
        }
        public async Task<IEnumerable<LookupItem>> GetOutcomes()
        {
            return await _context.LookupItems
                .Where(x => x.LookupList.Name == "WorkRequestOutcome" && x.RowState == RowStatus.Active)
                .ToListAsync();
        }

        internal async Task<IEnumerable<LookupItem>> GetCancelReasons()
        {
            return await _context.LookupItems
                .Where(x => x.LookupList.Name == "CancelReason" && x.RowState == RowStatus.Active)
                .ToListAsync();
        }
    }
}
