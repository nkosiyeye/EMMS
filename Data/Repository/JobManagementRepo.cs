using EMMS.Constants;
using EMMS.Models;
using EMMS.Models.Entities;
using Microsoft.EntityFrameworkCore;
using static EMMS.Models.Enumerators;

namespace EMMS.Data.Repository
{
    /// <summary>
    /// Read-only queries for the work request and job pages. Filtering happens in the database and nothing is
    /// tracked; changes go through <see cref="EMMS.Service.WorkRequestService"/>.
    /// </summary>
    public class JobManagementRepo
    {
        private readonly ApplicationDbContext _context;
        public JobManagementRepo(ApplicationDbContext context)
        {
            _context = context;
        }

        // ------------------------------------------------------------------------------------------
        // Work requests (facilityId null = every facility)
        // ------------------------------------------------------------------------------------------
        private IQueryable<WorkRequest> WorkRequestList(int? facilityId)
        {
            var query = _context.WorkRequest.AsNoTracking()
                .Include(w => w.Asset).ThenInclude(a => a!.SubCategory)
                .Include(w => w.WorkStatus)
                .Include(w => w.Outcome)
                .Include(w => w.RequestedByUser)
                .Include(w => w.Job).ThenInclude(j => j!.ExternalProvider)
                .Include(w => w.CancelReason)
                .AsSplitQuery();
            return facilityId == null ? query : query.Where(w => w.FacilityId == facilityId);
        }

        private IQueryable<InfrustructureWorkRequest> InfrastructureRequestList(int? facilityId)
        {
            var query = _context.InfrustructureWorkRequest.AsNoTracking()
                .Include(w => w.TypeOfRequest)
                .Include(w => w.WorkStatus)
                .Include(w => w.Outcome)
                .Include(w => w.RequestedByUser)
                .Include(w => w.Job).ThenInclude(j => j!.ExternalProvider)
                .Include(w => w.CancelReason)
                .AsSplitQuery();
            return facilityId == null ? query : query.Where(w => w.FacilityId == facilityId);
        }

        public async Task<List<WorkRequest>> GetWorkRequestsAsync(int? facilityId) =>
            await WorkRequestList(facilityId).OrderByDescending(w => w.RequestDate).ToListAsync();

        public async Task<List<InfrustructureWorkRequest>> GetInfrastructureRequestsAsync(int? facilityId) =>
            await InfrastructureRequestList(facilityId).OrderByDescending(w => w.RequestDate).ToListAsync();

        // Requests still waiting for a job (Manage Jobs).
        public async Task<List<WorkRequest>> GetUnassignedWorkRequestsAsync(int? facilityId) =>
            await WorkRequestList(facilityId)
                .Where(w => w.WorkStatus!.Name == WorkStatusNames.Open && w.JobId == null)
                .OrderBy(w => w.RequestDate)
                .ToListAsync();

        public async Task<List<InfrustructureWorkRequest>> GetUnassignedInfrastructureRequestsAsync(int? facilityId) =>
            await InfrastructureRequestList(facilityId)
                .Where(w => w.WorkStatus!.Name == WorkStatusNames.Open && w.JobId == null)
                .OrderBy(w => w.RequestDate)
                .ToListAsync();

        public async Task<WorkRequest?> GetWorkRequestForEditAsync(Guid id) =>
            await _context.WorkRequest.AsNoTracking()
                .Include(w => w.Asset).ThenInclude(a => a!.SubCategory)
                .Include(w => w.Facility)
                .FirstOrDefaultAsync(w => w.WorkRequestId == id);

        public async Task<WorkRequest?> GetWorkRequestDetailAsync(Guid id) =>
            await _context.WorkRequest.AsNoTracking()
                .Include(w => w.Asset).ThenInclude(a => a!.SubCategory)
                .Include(w => w.RequestedByUser)
                .Include(w => w.Outcome)
                .FirstOrDefaultAsync(w => w.WorkRequestId == id);

        public async Task<InfrustructureWorkRequest?> GetInfrastructureRequestDetailAsync(Guid id) =>
            await _context.InfrustructureWorkRequest.AsNoTracking()
                .Include(w => w.TypeOfRequest)
                .Include(w => w.RequestedByUser)
                .Include(w => w.Outcome)
                .FirstOrDefaultAsync(w => w.WorkRequestId == id);

        // ------------------------------------------------------------------------------------------
        // Jobs
        // ------------------------------------------------------------------------------------------
        private IQueryable<Job> JobList() =>
            _context.Job.AsNoTracking()
                .Include(w => w.Asset)
                .Include(w => w.InfraWorkRequest).ThenInclude(w => w!.TypeOfRequest)
                .Include(w => w.User)
                .Include(w => w.Status)
                .Include(w => w.FaultReport)
                .Include(w => w.ExternalProvider);

        // Jobs not yet closed or cancelled; assignedTo null = everyone's.
        public async Task<List<Job>> GetActiveJobsAsync(Guid? assignedTo)
        {
            var query = JobList().Where(j => j.Status!.Name != WorkStatusNames.Closed && j.Status!.Name != WorkStatusNames.Cancelled);
            if (assignedTo != null)
                query = query.Where(j => j.AssignedTo == assignedTo);
            return await query.OrderByDescending(j => j.StartDate).ToListAsync();
        }

        public async Task<Job?> GetJobAsync(Guid id) =>
            await JobList().FirstOrDefaultAsync(j => j.JobId == id);

        public async Task<List<WorkDone>> GetWorkDoneForJobAsync(Guid jobId) =>
            await _context.WorkDone.AsNoTracking()
                .Where(w => w.JobId == jobId && w.RowState == RowStatus.Active)
                .OrderByDescending(w => w.DateCompleted)
                .ToListAsync();

        public async Task<int> GetJobsCount(int facilityId, bool completed, bool isAdmin)
        {
            var query = _context.Job.AsQueryable();

            // Cancelled jobs also carry an end date, so count by status.
            if (completed)
                query = query.Where(j => j.Status!.Name == WorkStatusNames.Completed || j.Status!.Name == WorkStatusNames.Closed);
            else
                query = query.Where(j => j.Status!.Name == WorkStatusNames.InProgress);

            if (!isAdmin)
                query = query.Where(j => j.FacilityId == facilityId);

            return await query.CountAsync();
        }

        public async Task<List<WorkRequest>> GetOpenWorkRequestsByFacility(int? facilityId = null)
        {
            var query = _context.WorkRequest
                .AsNoTracking()
                .Where(w => w.WorkStatus!.Name == WorkStatusNames.Open);

            if (facilityId.HasValue)
                query = query.Where(w => w.FacilityId == facilityId.Value);

            return await query.ToListAsync();
        }

        public async Task<List<InfrustructureWorkRequest>> GetOpenInfraWorkRequestsByFacility(int? facilityId = null)
        {
            var query = _context.InfrustructureWorkRequest
                .AsNoTracking()
                .Where(w => w.WorkStatus!.Name == WorkStatusNames.Open);

            if (facilityId.HasValue)
                query = query.Where(w => w.FacilityId == facilityId.Value);

            return await query.ToListAsync();
        }

        // ------------------------------------------------------------------------------------------
        // Lookups
        // ------------------------------------------------------------------------------------------
        private async Task<List<LookupItem>> GetListAsync(string listName) =>
            await _context.LookupItems.AsNoTracking()
                .Where(x => x.LookupList!.Name == listName && x.RowState == RowStatus.Active)
                .OrderBy(x => x.SortIndex)
                .ToListAsync();

        public Task<List<LookupItem>> GetFaultReports() => GetListAsync(LookupListNames.WorkRequestFaultReport);
        public Task<List<LookupItem>> GetTypeOfRequest() => GetListAsync("TypeOfRequest");
    }
}
