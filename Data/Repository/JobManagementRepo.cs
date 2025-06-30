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
                .Include(w => w.WorkStatus)
                .Include(w => w.Outcome)
                .Include(w => w.FaultReport)
                .Include(w => w.Job)
                .ToListAsync();
        }
        public async Task<IEnumerable<Job>> GetJobfromDbs()
        {
            return await _context.Job
                .Include(w => w.Asset)
                .Include(w => w.User)
                .Include(w => w.Status)
                .Include(w => w.ExternalProvider)
                .ToListAsync();
        }

        public async Task<IEnumerable<WorkDone>> GetWorkDone()
        {
            return await _context.WorkDone
                .ToListAsync();
        }
        public async Task<IEnumerable<ExternalWorkDone>> GetExWorkDone()
        {
            return await _context.ExternalWorkDone
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
        public async Task<IEnumerable<LookupItem>> GetOutcomes()
        {
            return await _context.LookupItems
                .Where(x => x.LookupList.Name == "WorkRequestOutcome" && x.RowState == RowStatus.Active)
                .ToListAsync();
        }
    }
}
