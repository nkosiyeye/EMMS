using System.Data;
using System.Globalization;
using EMMS.Constants;
using EMMS.Data;
using EMMS.Models;
using EMMS.Models.Admin;
using EMMS.Models.Domain;
using EMMS.Models.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using static EMMS.Models.Enumerators;

namespace EMMS.Service
{
    public enum WorkState { Unknown, Open, InProgress, Completed, Cancelled, Closed }

    /// <summary>
    /// Work request (asset or infrastructure) -> Job (claimed by / assigned to a Biomed) -> Completed by the Biomed
    /// -> Closed by the requester with an outcome. A request can be withdrawn by its requester while it has no job,
    /// and cancelled by an admin until it is closed. Every status change goes through this class.
    /// </summary>
    public class WorkRequestService
    {
        public const string JobNumberSequence = "JobNumberSeq";
        public const string ServiceRequestDescription = "Service request for asset";

        private readonly ApplicationDbContext _context;
        private readonly INotificationService _notificationService;

        public WorkRequestService(ApplicationDbContext context, INotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        // ------------------------------------------------------------------------------------------
        // Rules (pure, unit-tested)
        // ------------------------------------------------------------------------------------------
        public static bool IsAdmin(User u) => u.UserRole?.UserType == UserType.Administrator;
        public static bool IsBiomed(User u) => u.UserRole?.UserType == UserType.Biomed;
        public static bool IsFacilityManager(User u) => u.UserRole?.UserType == UserType.FacilityManager;

        public static bool CanAccessFacility(User u, int facilityId) => IsAdmin(u) || u.FacilityId == facilityId;
        public static bool CanViewJob(User u, Job job) => CanAccessFacility(u, job.FacilityId) || job.AssignedTo == u.UserId;

        public static bool IsFinal(WorkState s) => s is WorkState.Closed or WorkState.Cancelled;

        public static bool CanEditRequest(WorkState status, bool hasJob) => status == WorkState.Open && !hasJob;
        public static bool CanCreateJob(WorkState status, bool hasJob) => status == WorkState.Open && !hasJob;

        // The requester closes a request once the Biomed has completed the job.
        public static bool CanClose(WorkState requestStatus, WorkState? jobStatus) =>
            requestStatus == WorkState.Completed && jobStatus == WorkState.Completed;

        // Admins cancel anything not yet final; requesters may only withdraw before a job exists.
        public static bool CanCancel(bool isAdmin, bool isRequester, WorkState status, bool hasJob) =>
            !IsFinal(status) && (isAdmin || (isRequester && status == WorkState.Open && !hasJob));

        // The assignee moves a running job between In Progress and Completed; Completed is final for them.
        public static bool CanUpdateJob(WorkState jobStatus, DateTime? endDate) =>
            jobStatus == WorkState.InProgress && endDate == null;
        // Admins reassign anywhere, facility managers within their facility; only a running job, to someone new.
        public static bool CanReassignJob(User u, Job job, WorkState jobStatus) =>
            (IsAdmin(u) || (IsFacilityManager(u) && u.FacilityId == job.FacilityId))
            && CanUpdateJob(jobStatus, job.EndDate);

        // An entry on a job (work done, part) is changed only by the Biomed who recorded it.
        public static bool CanEditJobEntry(User u, Guid? entryBiomedId) => entryBiomedId == u.UserId;

        public static bool IsAllowedJobStatus(WorkState s) => s is WorkState.InProgress or WorkState.Completed;

        public static DateTime? NextServiceDate(bool isServiceRequest, bool successful, bool serviceable, int? intervalMonths, DateTime closeDate) =>
            isServiceRequest && successful && serviceable && intervalMonths is > 0
                ? closeDate.Date.AddMonths(intervalMonths.Value)
                : null;

        // A part needs a name (picked or typed), a positive quantity and a non-negative unit cost.
        public static string? ValidateJobPart(string? partName, decimal quantity, decimal unitCost)
        {
            if (string.IsNullOrWhiteSpace(partName)) return "Select or enter a part.";
            if (quantity <= 0) return "Quantity must be greater than zero.";
            if (unitCost < 0) return "Unit cost cannot be negative.";
            return null;
        }

        public static decimal PartsTotal(IEnumerable<JobPart> parts) => parts.Sum(p => p.LineTotal);

        public static string FormatJobNumber(string? facilityCode, long number) =>
            $"{facilityCode}Jb-{number.ToString("D3", CultureInfo.InvariantCulture)}";

        public static WorkState ToState(string? statusName) => statusName switch
        {
            WorkStatusNames.Open => WorkState.Open,
            WorkStatusNames.InProgress => WorkState.InProgress,
            WorkStatusNames.Completed => WorkState.Completed,
            WorkStatusNames.Cancelled => WorkState.Cancelled,
            WorkStatusNames.Closed => WorkState.Closed,
            _ => WorkState.Unknown
        };

        // ------------------------------------------------------------------------------------------
        // Lookups
        // ------------------------------------------------------------------------------------------
        private Task<List<LookupItem>> GetListAsync(string listName) =>
            _context.LookupItems.AsNoTracking()
                .Where(l => l.LookupList!.Name == listName && l.RowState == RowStatus.Active)
                .OrderBy(l => l.SortIndex)
                .ToListAsync();

        public Task<List<LookupItem>> GetStatusesAsync() => GetListAsync(LookupListNames.WorkRequestStatus);
        public Task<List<LookupItem>> GetOutcomesAsync() => GetListAsync(LookupListNames.WorkRequestOutcome);
        public Task<List<LookupItem>> GetCancelReasonsAsync() => GetListAsync(LookupListNames.CancelReason);
        public Task<List<LookupItem>> GetSparePartsAsync() => GetListAsync(LookupListNames.SparePart);

        /// <summary>Status lookup id &lt;-&gt; workflow state, loaded once per operation.</summary>
        public sealed class StatusMap
        {
            private readonly Dictionary<WorkState, int> _ids;
            private readonly Dictionary<int, WorkState> _states;

            public StatusMap(IEnumerable<LookupItem> statuses)
            {
                _ids = new();
                _states = new();
                foreach (var s in statuses)
                {
                    var state = ToState(s.Name);
                    if (state == WorkState.Unknown || _ids.ContainsKey(state)) continue;
                    _ids[state] = s.Id;
                    _states[s.Id] = state;
                }
            }

            public bool IsComplete => _ids.Count == WorkStatusNames.All.Length;
            public int Id(WorkState s) => _ids[s];
            public WorkState State(int statusId) => _states.TryGetValue(statusId, out var s) ? s : WorkState.Unknown;
        }

        private async Task<StatusMap?> LoadStatusMapAsync()
        {
            var map = new StatusMap(await GetStatusesAsync());
            return map.IsComplete ? map : null;
        }

        private const string StatusesMissing = "The work request statuses (Open, In Progress, Completed, Cancelled, Closed) are not all configured.";

        private static void Touch(BaseEntity e, User u)
        {
            e.ModifiedBy = u.UserId;
            e.DateModified = DateTime.Now;
        }

        private static void Stamp(BaseEntity e, User u)
        {
            e.CreatedBy = u.UserId;
            e.DateCreated = DateTime.Now;
            e.RowState = RowStatus.Active;
        }

        // ------------------------------------------------------------------------------------------
        // Requests
        // ------------------------------------------------------------------------------------------

        // An asset may only have one request that has not been closed or cancelled.
        public async Task<bool> HasOpenRequestAsync(Guid assetId)
        {
            var latest = await _context.WorkRequest.AsNoTracking()
                .Where(w => w.AssetId == assetId)
                .OrderByDescending(w => w.DateCreated)
                .FirstOrDefaultAsync();
            return latest != null && latest.CloseDate == null;
        }

        public async Task<string?> CreateAssetRequestAsync(User user, Guid assetId, DateTime requestDate, string? description)
        {
            if (!await _context.Assets.AnyAsync(a => a.AssetId == assetId && a.RowState == RowStatus.Active))
                return "Asset not found.";
            return await AddAssetRequestAsync(user, assetId, requestDate, description, user.FacilityId, isServiceRequest: false);
        }

        public async Task<string?> CreateServiceRequestAsync(User user, Guid assetId)
        {
            if (!await _context.Assets.AnyAsync(a => a.AssetId == assetId && a.RowState == RowStatus.Active))
                return "Asset not found.";
            var location = await _context.AssetCurrentLocations
                .Where(l => l.AssetId == assetId)
                .Select(l => (int?)l.FacilityId)
                .FirstOrDefaultAsync();
            return await AddAssetRequestAsync(user, assetId, DateTime.Now, ServiceRequestDescription, location ?? user.FacilityId, isServiceRequest: true);
        }

        private async Task<string?> AddAssetRequestAsync(User user, Guid assetId, DateTime requestDate, string? description, int facilityId, bool isServiceRequest)
        {
            if (string.IsNullOrWhiteSpace(description)) return "Description is required.";
            if (await HasOpenRequestAsync(assetId)) return "An active work request exists for this asset.";
            var statuses = await LoadStatusMapAsync();
            if (statuses == null) return StatusesMissing;

            var work = new WorkRequest
            {
                WorkRequestId = Guid.NewGuid(),
                AssetId = assetId,
                RequestDate = requestDate,
                Description = description.Trim(),
                IsServiceRequest = isServiceRequest,
                FacilityId = facilityId,
                WorkStatusId = statuses.Id(WorkState.Open),
                RequestedBy = user.UserId,
            };
            Stamp(work, user);
            _context.WorkRequest.Add(work);
            await _context.SaveChangesAsync();
            await _notificationService.CreateWorkRequestNotification(work.FacilityId, user.UserId);
            return null;
        }

        public async Task<string?> CreateInfrastructureRequestAsync(User user, int? typeOfRequestId, DateTime requestDate, string? description)
        {
            if (string.IsNullOrWhiteSpace(description)) return "Description is required.";
            if (typeOfRequestId == null || !await _context.LookupItems.AnyAsync(l => l.Id == typeOfRequestId && l.LookupList!.Name == "TypeOfRequest"))
                return "Select a type of request.";
            var statuses = await LoadStatusMapAsync();
            if (statuses == null) return StatusesMissing;

            var work = new InfrustructureWorkRequest
            {
                WorkRequestId = Guid.NewGuid(),
                TypeOfRequestId = typeOfRequestId,
                RequestDate = requestDate,
                Description = description.Trim(),
                FacilityId = user.FacilityId,
                WorkStatusId = statuses.Id(WorkState.Open),
                RequestedBy = user.UserId,
            };
            Stamp(work, user);
            _context.InfrustructureWorkRequest.Add(work);
            await _context.SaveChangesAsync();
            await _notificationService.CreateWorkRequestNotification(work.FacilityId, user.UserId);
            return null;
        }

        public async Task<string?> EditRequestAsync(User user, Guid workRequestId, DateTime requestDate, string? description)
        {
            if (string.IsNullOrWhiteSpace(description)) return "Description is required.";
            var work = await _context.WorkRequest.FirstOrDefaultAsync(w => w.WorkRequestId == workRequestId);
            if (work == null) return "Work request not found.";
            if (!CanAccessFacility(user, work.FacilityId)) return "You do not have access to that request.";
            var statuses = await LoadStatusMapAsync();
            if (statuses == null) return StatusesMissing;
            if (!CanEditRequest(statuses.State(work.WorkStatusId), work.JobId != null))
                return "Only open requests without a job can be edited.";

            work.RequestDate = requestDate;
            work.Description = description.Trim();
            Touch(work, user);
            await _context.SaveChangesAsync();
            return null;
        }

        // ------------------------------------------------------------------------------------------
        // Jobs
        // ------------------------------------------------------------------------------------------
        private async Task<long> NextJobNumberAsync()
        {
            var value = new SqlParameter("@value", SqlDbType.BigInt) { Direction = ParameterDirection.Output };
            await _context.Database.ExecuteSqlRawAsync($"SET @value = NEXT VALUE FOR dbo.{JobNumberSequence}", value);
            return (long)value.Value;
        }

        /// <param name="workRequestId">Asset work request, or</param>
        /// <param name="infrastructureWorkRequestId">infrastructure work request.</param>
        public async Task<(Job? job, string? error)> CreateJobAsync(User user, Guid? workRequestId, Guid? infrastructureWorkRequestId,
            Guid? assignedTo, int faultReportId, DateTime startDate, bool isExternal, int? externalProviderId)
        {
            if (!IsAdmin(user) && !IsBiomed(user)) return (null, "Only an Admin or Biomed can create a job.");

            // A Biomed can only claim a job for themselves.
            if (!IsAdmin(user))
                assignedTo = user.UserId;
            else if (!await _context.User.AnyAsync(u => u.UserId == assignedTo && u.RowState == RowStatus.Active && u.UserRole!.UserType == UserType.Biomed))
                return (null, "Select a Biomed to assign the job to.");

            if (!await _context.LookupItems.AnyAsync(l => l.Id == faultReportId && l.LookupList!.Name == LookupListNames.WorkRequestFaultReport && l.RowState == RowStatus.Active))
                return (null, "Select a fault report.");
            if (isExternal && externalProviderId == null)
                return (null, "Select a service provider.");

            var statuses = await LoadStatusMapAsync();
            if (statuses == null) return (null, StatusesMissing);

            WorkRequest? wRequest = null;
            InfrustructureWorkRequest? infraRequest = null;
            int facilityId;
            Guid? requestedBy;

            if (workRequestId != null)
            {
                wRequest = await _context.WorkRequest.FirstOrDefaultAsync(w => w.WorkRequestId == workRequestId);
                if (wRequest == null || !CanAccessFacility(user, wRequest.FacilityId)) return (null, "Work request not found.");
                bool hasJob = wRequest.JobId != null || await _context.Job.AnyAsync(j => j.WorkRequestId == wRequest.WorkRequestId);
                if (!CanCreateJob(statuses.State(wRequest.WorkStatusId), hasJob)) return (null, "This work request is no longer open for a new job.");
                facilityId = wRequest.FacilityId;
                requestedBy = wRequest.RequestedBy;
            }
            else if (infrastructureWorkRequestId != null)
            {
                infraRequest = await _context.InfrustructureWorkRequest.FirstOrDefaultAsync(i => i.WorkRequestId == infrastructureWorkRequestId);
                if (infraRequest == null || !CanAccessFacility(user, infraRequest.FacilityId)) return (null, "Work request not found.");
                bool hasJob = infraRequest.JobId != null || await _context.Job.AnyAsync(j => j.InfrastructureWorkRequestId == infraRequest.WorkRequestId);
                if (!CanCreateJob(statuses.State(infraRequest.WorkStatusId), hasJob)) return (null, "This work request is no longer open for a new job.");
                facilityId = infraRequest.FacilityId;
                requestedBy = infraRequest.RequestedBy;
            }
            else
            {
                return (null, "No work request was selected.");
            }

            var facilityCode = await _context.Facilities.Where(f => f.FacilityId == facilityId).Select(f => f.FacilityCode).FirstOrDefaultAsync();
            var inProgress = statuses.Id(WorkState.InProgress);
            var job = new Job
            {
                JobId = Guid.NewGuid(),
                JobNumber = FormatJobNumber(facilityCode, await NextJobNumberAsync()),
                WorkRequestId = wRequest?.WorkRequestId,
                InfrastructureWorkRequestId = infraRequest?.WorkRequestId,
                AssetId = wRequest?.AssetId,
                AssignedTo = assignedTo,
                StartDate = startDate,
                FaultReportId = faultReportId,
                IsExternalProvider = isExternal,
                ExternalProviderId = isExternal ? externalProviderId : null,
                StatusId = inProgress,
                FacilityId = facilityId,
            };
            Stamp(job, user);
            _context.Job.Add(job);

            if (wRequest != null)
            {
                wRequest.WorkStatusId = inProgress;
                wRequest.JobId = job.JobId;
                Touch(wRequest, user);

                var asset = await _context.Assets.FirstOrDefaultAsync(a => a.AssetId == wRequest.AssetId);
                if (asset != null)
                    AssetStatusTracker.Change(_context, asset, AssetStatusTracker.OnJobOpened, StatusChangeSource.JobOpened, user.UserId, jobId: job.JobId);
            }
            else
            {
                infraRequest!.WorkStatusId = inProgress;
                infraRequest.JobId = job.JobId;
                Touch(infraRequest, user);
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Someone else created a job for this request at the same moment (unique index on the request id).
                if (await RequestHasJobAsync(workRequestId, infrastructureWorkRequestId))
                    return (null, "This work request already has a job.");
                throw;
            }

            if (IsAdmin(user))
                await _notificationService.CreateJobAssignmentNotification(job.AssignedTo, requestedBy, user.UserId);
            else
                await _notificationService.CreateJobClaimedNotification(job.AssignedTo, requestedBy, user.UserId);

            return (job, null);
        }

        private async Task<bool> RequestHasJobAsync(Guid? workRequestId, Guid? infrastructureWorkRequestId)
        {
            _context.ChangeTracker.Clear();
            return workRequestId != null
                ? await _context.Job.AnyAsync(j => j.WorkRequestId == workRequestId)
                : await _context.Job.AnyAsync(j => j.InfrastructureWorkRequestId == infrastructureWorkRequestId);
        }

        public async Task<(Job? job, string? error)> UpdateJobAsync(User user, Guid jobId, int statusId, string? remarks, int? amount, string? invoiceNo)
        {
            var job = await _context.Job.FirstOrDefaultAsync(j => j.JobId == jobId);
            if (job == null) return (null, "Job not found.");
            if (job.AssignedTo != user.UserId) return (job, "Only the assigned Biomed can update this job.");
            var statuses = await LoadStatusMapAsync();
            if (statuses == null) return (job, StatusesMissing);
            if (!CanUpdateJob(statuses.State(job.StatusId), job.EndDate)) return (job, "This job can no longer be changed.");
            var newState = statuses.State(statusId);
            if (!IsAllowedJobStatus(newState)) return (job, "Select a valid job status.");
            if (job.IsExternalProvider && amount < 0) return (job, "Amount cannot be negative.");

            job.StatusId = statusId;
            job.Remarks = remarks;
            if (job.IsExternalProvider)
            {
                job.Amount = amount;
                job.InvoiceNo = invoiceNo;
            }
            if (newState == WorkState.Completed)
                job.EndDate = DateTime.Now;
            Touch(job, user);

            // Keep the linked request's status in step with the job.
            Guid? requestedBy = null;
            if (job.WorkRequestId != null)
            {
                var work = await _context.WorkRequest.FirstOrDefaultAsync(w => w.WorkRequestId == job.WorkRequestId);
                if (work != null) { work.WorkStatusId = statusId; Touch(work, user); requestedBy = work.RequestedBy; }
            }
            else if (job.InfrastructureWorkRequestId != null)
            {
                var infra = await _context.InfrustructureWorkRequest.FirstOrDefaultAsync(w => w.WorkRequestId == job.InfrastructureWorkRequestId);
                if (infra != null) { infra.WorkStatusId = statusId; Touch(infra, user); requestedBy = infra.RequestedBy; }
            }

            await _context.SaveChangesAsync();

            if (newState == WorkState.Completed)
                await _notificationService.CreateJobCompletedNotification(requestedBy, user.UserId);

            return (job, null);
        }

        public async Task<(Job? job, string? error)> ReassignJobAsync(User user, Guid jobId, Guid? newAssignee)
        {
            var job = await _context.Job.FirstOrDefaultAsync(j => j.JobId == jobId);
            if (job == null) return (null, "Job not found.");
            var statuses = await LoadStatusMapAsync();
            if (statuses == null) return (job, StatusesMissing);
            if (!CanReassignJob(user, job, statuses.State(job.StatusId)))
                return (job, "Only an Admin or the facility manager can reassign this job, and only while it is In Progress.");

            var assignee = await _context.User.AsNoTracking().FirstOrDefaultAsync(u =>
                u.UserId == newAssignee && u.RowState == RowStatus.Active && u.UserRole!.UserType == UserType.Biomed);
            if (assignee == null) return (job, "Select a Biomed to assign the job to.");
            if (!IsAdmin(user) && assignee.FacilityId != user.FacilityId) return (job, "Select a Biomed from your facility.");
            if (assignee.UserId == job.AssignedTo) return (job, "The job is already assigned to that Biomed.");

            var previous = job.AssignedTo;
            job.AssignedTo = assignee.UserId;
            Touch(job, user);
            await _context.SaveChangesAsync();

            await _notificationService.CreateUserNotification(assignee.UserId, $"Job {job.JobNumber} has been reassigned to you.", "job", user.UserId);
            await _notificationService.CreateUserNotification(previous, $"Job {job.JobNumber} has been reassigned to another Biomed.", "job", user.UserId);
            return (job, null);
        }

        // Work done can only be recorded by the assignee while the job is running.
        private async Task<string?> CheckWorkDoneAllowedAsync(User user, Job job)
        {
            if (job.AssignedTo != user.UserId) return "Only the assigned Biomed can record work on this job.";
            var statuses = await LoadStatusMapAsync();
            if (statuses == null) return StatusesMissing;
            return CanUpdateJob(statuses.State(job.StatusId), job.EndDate) ? null : "This job is no longer open.";
        }

        public async Task<(Job? job, string? error)> SaveWorkDoneAsync(User user, WorkDone posted)
        {
            var job = await _context.Job.FirstOrDefaultAsync(j => j.JobId == posted.JobId);
            if (job == null) return (null, "Job not found.");
            var error = await CheckWorkDoneAllowedAsync(user, job);
            if (error != null) return (job, error);
            if (string.IsNullOrWhiteSpace(posted.Details)) return (job, "Details are required.");
            if (posted.Hours < 0) return (job, "Hours cannot be negative.");

            if (posted.WorkDoneId == Guid.Empty)
            {
                var work = new WorkDone
                {
                    WorkDoneId = Guid.NewGuid(),
                    JobId = job.JobId,
                    BiomedId = user.UserId,
                    DateCompleted = posted.DateCompleted,
                    Hours = posted.Hours,
                    Details = posted.Details,
                };
                Stamp(work, user);
                _context.WorkDone.Add(work);
            }
            else
            {
                var existing = await _context.WorkDone.FirstOrDefaultAsync(w =>
                    w.WorkDoneId == posted.WorkDoneId && w.JobId == job.JobId && w.RowState == RowStatus.Active);
                if (existing == null) return (job, "Work done entry not found.");
                if (!CanEditJobEntry(user, existing.BiomedId)) return (job, "You can only edit work you recorded.");
                existing.DateCompleted = posted.DateCompleted;
                existing.Hours = posted.Hours;
                existing.Details = posted.Details;
                Touch(existing, user);
            }

            await _context.SaveChangesAsync();
            return (job, null);
        }

        public async Task<(Job? job, string? error)> DeleteWorkDoneAsync(User user, Guid workDoneId)
        {
            var work = await _context.WorkDone.FirstOrDefaultAsync(w => w.WorkDoneId == workDoneId && w.RowState == RowStatus.Active);
            if (work == null) return (null, "Work done entry not found.");
            var job = await _context.Job.FirstOrDefaultAsync(j => j.JobId == work.JobId);
            if (job == null) return (null, "Job not found.");
            var error = await CheckWorkDoneAllowedAsync(user, job);
            if (error != null) return (job, error);

            if (!CanEditJobEntry(user, work.BiomedId)) return (job, "You can only delete work you recorded.");

            work.RowState = RowStatus.Deleted;
            Touch(work, user);
            await _context.SaveChangesAsync();
            return (job, null);
        }

        // Parts follow the same rule as work done: only the assignee, only while the job is running.
        public async Task<(Job? job, string? error)> SaveJobPartAsync(User user, JobPart posted)
        {
            var job = await _context.Job.FirstOrDefaultAsync(j => j.JobId == posted.JobId);
            if (job == null) return (null, "Job not found.");
            var error = await CheckWorkDoneAllowedAsync(user, job);
            if (error != null) return (job, error);

            // A picked part takes its name from the lookup; otherwise the typed name is used.
            var name = posted.PartName?.Trim();
            if (posted.PartId != null)
            {
                var part = await _context.LookupItems.AsNoTracking().FirstOrDefaultAsync(l =>
                    l.Id == posted.PartId && l.LookupList!.Name == LookupListNames.SparePart && l.RowState == RowStatus.Active);
                if (part == null) return (job, "Part not found.");
                name = part.Name;
            }
            error = ValidateJobPart(name, posted.Quantity, posted.UnitCost);
            if (error != null) return (job, error);

            if (posted.JobPartId == Guid.Empty)
            {
                var jobPart = new JobPart
                {
                    JobPartId = Guid.NewGuid(),
                    JobId = job.JobId,
                    BiomedId = user.UserId,
                    PartId = posted.PartId,
                    PartName = name,
                    Quantity = posted.Quantity,
                    UnitCost = posted.UnitCost,
                };
                Stamp(jobPart, user);
                _context.JobPart.Add(jobPart);
            }
            else
            {
                var existing = await _context.JobPart.FirstOrDefaultAsync(p =>
                    p.JobPartId == posted.JobPartId && p.JobId == job.JobId && p.RowState == RowStatus.Active);
                if (existing == null) return (job, "Part entry not found.");
                if (!CanEditJobEntry(user, existing.BiomedId)) return (job, "You can only edit parts you recorded.");
                existing.PartId = posted.PartId;
                existing.PartName = name;
                existing.Quantity = posted.Quantity;
                existing.UnitCost = posted.UnitCost;
                Touch(existing, user);
            }

            await _context.SaveChangesAsync();
            return (job, null);
        }

        public async Task<(Job? job, string? error)> DeleteJobPartAsync(User user, Guid jobPartId)
        {
            var jobPart = await _context.JobPart.FirstOrDefaultAsync(p => p.JobPartId == jobPartId && p.RowState == RowStatus.Active);
            if (jobPart == null) return (null, "Part entry not found.");
            var job = await _context.Job.FirstOrDefaultAsync(j => j.JobId == jobPart.JobId);
            if (job == null) return (null, "Job not found.");
            var error = await CheckWorkDoneAllowedAsync(user, job);
            if (error != null) return (job, error);

            if (!CanEditJobEntry(user, jobPart.BiomedId)) return (job, "You can only delete parts you recorded.");

            jobPart.RowState = RowStatus.Deleted;
            Touch(jobPart, user);
            await _context.SaveChangesAsync();
            return (job, null);
        }

        // ------------------------------------------------------------------------------------------
        // Close and cancel (asset or infrastructure request, found by id)
        // ------------------------------------------------------------------------------------------
        private sealed class RequestRef
        {
            public WorkRequest? Asset;
            public InfrustructureWorkRequest? Infra;
            public BaseEntity Entity => (BaseEntity?)Asset ?? Infra!;
            public int FacilityId => Asset?.FacilityId ?? Infra!.FacilityId;
            public Guid? RequestedBy => Asset != null ? Asset.RequestedBy : Infra!.RequestedBy;
            public int WorkStatusId { get => Asset?.WorkStatusId ?? Infra!.WorkStatusId; set { if (Asset != null) Asset.WorkStatusId = value; else Infra!.WorkStatusId = value; } }
            public int? OutcomeId { set { if (Asset != null) Asset.OutcomeId = value; else Infra!.OutcomeId = value; } }
            public DateTime? CloseDate { set { if (Asset != null) Asset.CloseDate = value; else Infra!.CloseDate = value; } }
            public int? CancelReasonId { set { if (Asset != null) Asset.CancelReasonId = value; else Infra!.CancelReasonId = value; } }
        }

        private async Task<(RequestRef? request, Job? job)> LoadRequestAsync(Guid id)
        {
            var asset = await _context.WorkRequest.FirstOrDefaultAsync(w => w.WorkRequestId == id);
            var infra = asset == null ? await _context.InfrustructureWorkRequest.FirstOrDefaultAsync(i => i.WorkRequestId == id) : null;
            if (asset == null && infra == null) return (null, null);

            var job = asset != null
                ? await _context.Job.FirstOrDefaultAsync(j => j.WorkRequestId == id)
                : await _context.Job.FirstOrDefaultAsync(j => j.InfrastructureWorkRequestId == id);
            return (new RequestRef { Asset = asset, Infra = infra }, job);
        }

        public async Task<string?> CloseRequestAsync(User user, Guid workRequestId, DateTime? closeDate, int? outcomeId)
        {
            var (request, job) = await LoadRequestAsync(workRequestId);
            if (request == null) return "Work request not found.";
            if (!IsAdmin(user) && request.RequestedBy != user.UserId) return "Only the requester or an admin can close this request.";
            var statuses = await LoadStatusMapAsync();
            if (statuses == null) return StatusesMissing;
            if (!CanClose(statuses.State(request.WorkStatusId), job == null ? null : statuses.State(job.StatusId)))
                return "A request can only be closed once its job has been completed.";

            var outcome = (await GetOutcomesAsync()).FirstOrDefault(o => o.Id == outcomeId);
            if (outcome == null || outcome.Name == WorkOutcomeNames.Pending) return "Select an outcome.";

            var date = closeDate ?? DateTime.Now;
            var closed = statuses.Id(WorkState.Closed);

            job!.StatusId = closed;
            job.EndDate ??= date;
            Touch(job, user);

            request.WorkStatusId = closed;
            request.OutcomeId = outcome.Id;
            request.CloseDate = date;
            Touch(request.Entity, user);

            var asset = request.Asset?.AssetId == null ? null
                : await _context.Assets.FirstOrDefaultAsync(a => a.AssetId == request.Asset.AssetId);
            if (asset != null && AssetStatusTracker.StatusAfterClose(outcome.Name) is FunctionalStatus after)
                AssetStatusTracker.Change(_context, asset, after, StatusChangeSource.JobClosed, user.UserId, jobId: job.JobId);

            if (asset != null && request.Asset!.IsServiceRequest)
            {
                var next = NextServiceDate(true, outcome.Name == WorkOutcomeNames.Successful, asset.IsServiceable, asset.ServiceInterval, date);
                if (next != null)
                {
                    asset.NextServiceDate = next;
                    Touch(asset, user);
                }
            }

            await _context.SaveChangesAsync();
            return null;
        }

        public async Task<string?> CancelRequestAsync(User user, Guid workRequestId, DateTime? closeDate, int? outcomeId, int? cancelReasonId)
        {
            var (request, job) = await LoadRequestAsync(workRequestId);
            if (request == null) return "Work request not found.";
            if (!IsAdmin(user) && request.RequestedBy != user.UserId) return "Only the requester or an admin can cancel this request.";
            var statuses = await LoadStatusMapAsync();
            if (statuses == null) return StatusesMissing;
            if (!CanCancel(IsAdmin(user), request.RequestedBy == user.UserId, statuses.State(request.WorkStatusId), job != null))
                return IsAdmin(user) ? "This request has already been closed." : "Only an open request without a job can be withdrawn.";

            if (!(await GetCancelReasonsAsync()).Any(r => r.Id == cancelReasonId)) return "Select a cancel reason.";
            if (outcomeId != null && !(await GetOutcomesAsync()).Any(o => o.Id == outcomeId)) return "Select a valid outcome.";

            var date = closeDate ?? DateTime.Now;
            var cancelled = statuses.Id(WorkState.Cancelled);

            // A cancelled request must not leave its job running.
            if (job != null && !IsFinal(statuses.State(job.StatusId)))
            {
                job.StatusId = cancelled;
                job.EndDate ??= date;
                Touch(job, user);

                var asset = request.Asset?.AssetId == null ? null
                    : await _context.Assets.FirstOrDefaultAsync(a => a.AssetId == request.Asset.AssetId);
                if (asset != null
                    && AssetStatusTracker.StatusAfterCancel(asset.FunctionalStatus, await AssetStatusTracker.GetJobOpenedAsync(_context, job.JobId)) is FunctionalStatus restored)
                    AssetStatusTracker.Change(_context, asset, restored, StatusChangeSource.JobCancelled, user.UserId, jobId: job.JobId);
            }

            request.WorkStatusId = cancelled;
            request.OutcomeId = outcomeId;
            request.CancelReasonId = cancelReasonId;
            request.CloseDate = date;
            Touch(request.Entity, user);

            await _context.SaveChangesAsync();
            return null;
        }
    }
}
