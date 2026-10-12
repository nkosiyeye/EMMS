using EMMS.CustomAttributes;
using EMMS.Data;
using EMMS.Data.Repository;
using EMMS.Models;
using EMMS.Models.Entities;
using EMMS.Service;
using EMMS.Utility;
using EMMS.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using static EMMS.Models.Enumerators;

namespace EMMS.Controllers
{
    [RequireLogin]
    public class JobManagementController : BaseController
    {
        private const string AccessDeniedMessage = "You do not have access to that record.";

        private readonly ApplicationDbContext _context;
        private readonly AssetService _assetService;
        private readonly AssetManagementRepo _assetManagementRepo;
        private readonly NotificationService _notificationService;
        public JobManagementController(ApplicationDbContext context, NotificationService notificationService, AssetService assetService, AssetManagementRepo assetMovementRepo)
        {
            _context = context;
            _assetService = assetService;
            _notificationService = notificationService;
            _assetManagementRepo = assetMovementRepo;
        }

        // ----- Access helpers -----
        private bool CanAccessFacility(int facilityId) => isAdmin || facilityId == CurrentUser!.FacilityId;
        private bool IsAssignee(Job job) => job.AssignedTo == CurrentUser!.UserId;
        private bool CanViewJob(Job job) => CanAccessFacility(job.FacilityId) || IsAssignee(job);
        private bool CanCloseRequest(Guid? requestedBy) => isAdmin || requestedBy == CurrentUser!.UserId;

        private IActionResult AccessDenied()
        {
            TempData["Error"] = AccessDeniedMessage;
            return RedirectToAction("Index", "Home");
        }

        private string? ModelStateErrors()
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            return errors.Count == 0 ? null : string.Join("; ", errors);
        }

        private async Task<int?> GetWorkStatusId(string name)
        {
            var statuses = await new JobManagementRepo(_context).GetWorkStatus();
            return statuses.FirstOrDefault(s => s.Name == name)?.Id;
        }

        // An asset may only have one work request that has not been closed or cancelled.
        private async Task<bool> HasOpenWorkRequest(Guid assetId)
        {
            var latest = await _context.WorkRequest
                .AsNoTracking()
                .Where(w => w.AssetId == assetId)
                .OrderByDescending(w => w.DateCreated)
                .FirstOrDefaultAsync();
            return latest != null && latest.CloseDate == null;
        }

        private async Task SaveNewWorkRequest(WorkRequest work)
        {
            work.RequestedBy = CurrentUser!.UserId;
            CreateEntity(work);
            _context.Add(work);
            await _context.SaveChangesAsync();
            await _notificationService.CreateWorkRequestNotification(work.FacilityId, CurrentUser.UserId);
        }

        private async Task<WorkRequestViewModel> WorkRequestData( WorkRequest? workmodel = null)
        {
            if (workmodel is null)
            {
                workmodel = new WorkRequest();
            }

            var assets = await _assetService.GetAssetIndexViewModel(CurrentUser);
            WorkRequestViewModel paginatedWorkRequest = new WorkRequestViewModel
            {
                WorkRequest = workmodel,
                AssetIndex = assets,
            };

            return paginatedWorkRequest;

        }

        private async Task<JobViewModel> JobData(Job? jobmodel = null)
        {
            if (jobmodel is null)
            {
                jobmodel = new Job();
            }
            var _repo = new JobManagementRepo(_context);
            var _Assetrepo = _assetManagementRepo;
            var all = await _repo.GetJobfromDbs();
            var filteredJobs = all.Where(w => w.AssignedTo == CurrentUser.UserId);
            var allInfWork = await _repo.GetInfrustructureWorkRequests();
            var filteredInfWork = allInfWork.Where(w => w.FacilityId == CurrentUser.FacilityId);
            var allWork = await _repo.GetWorkRequests();
            var filteredWork = allWork.Where(w => w.FacilityId == CurrentUser.FacilityId);
            JobViewModel paginatedJob = new JobViewModel
            {
                Jobs = !isAdmin ? filteredJobs : all,
                WorkRequests = !isAdmin ? filteredWork : allWork,
                InfrastructureWorkRequests = !isAdmin ? filteredInfWork : allInfWork,
                Job = jobmodel,
                //Asset = await assets.GetAssetsFromDb(),
                FaultReports = await _repo.GetFaultReports(),
                WorkStatuses = await _repo.GetWorkStatus(),
                Outcomes = await _repo.GetOutcomes(),
                ServiceProviders = await _Assetrepo.GetServiceProviders(),
                CancelReasons = await _repo.GetCancelReasons(),
            };

            return paginatedJob;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var _repo = new JobManagementRepo(_context);
            var allWork = await _repo.GetWorkRequests();
            var filteredWork = allWork.Where(w => w.FacilityId == CurrentUser.FacilityId);
            var allInfWork = await _repo.GetInfrustructureWorkRequests();
            var filteredInfWork = allInfWork.Where(w => w.FacilityId == CurrentUser.FacilityId);
            var data = await WorkRequestData();
            data.WorkRequests = !isAdmin ? filteredWork : allWork;//.Result.Where(w => w.FacilityId == CurrentUser.FacilityId);
            data.InfrastructureWorkRequests = !isAdmin ? filteredInfWork : allInfWork;//.Result.Where(w => w.FacilityId == CurrentUser.FacilityId);
            data.Outcomes = await _repo.GetOutcomes();
            data.WorkStatuses = await _repo.GetWorkStatus();
            return View(data);
        }

        [HttpGet]
        public async Task<IActionResult> workRequest(Guid id)
        {
            var asset = await _context.Assets
                .AsNoTracking()
                .Include(a => a.SubCategory)
                .FirstOrDefaultAsync(a => a.AssetId == id && a.RowState == RowStatus.Active);
            if (asset == null)
                return NotFound();

            var _repo = new JobManagementRepo(_context);
            var workRequest = new WorkRequest()
            {
                AssetId = id
            };
            var workAssetViewModel = new WorkRequestViewModel()
            {
                Asset = asset,
                WorkRequest = workRequest,
                FaultReports = await _repo.GetFaultReports(),
                WorkStatuses = await _repo.GetWorkStatus(),
            };

            return View(workAssetViewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> WorkRequest(WorkRequestViewModel workRequestView)
        {
            var work = workRequestView.WorkRequest;
            if (work?.AssetId == null)
                return BadRequest();

            var openStatusId = await GetWorkStatusId("Open");
            if (openStatusId == null)
                ModelState.AddModelError("", "The 'Open' work status is not configured.");
            if (await HasOpenWorkRequest(work.AssetId.Value))
                ModelState.AddModelError("", "Asset Already has a workrequest in progress.");

            if (ModelState.IsValid)
            {
                // Status and facility are decided here, not by the form.
                work.WorkStatusId = openStatusId!.Value;
                work.FacilityId = CurrentUser!.FacilityId;
                await SaveNewWorkRequest(work);

                return RedirectToAction(nameof(Index));
            }

            TempData["WorkRequestError"] = ModelStateErrors();
            return RedirectToAction(nameof(workRequest), new { id = work.AssetId });
        }

        [HttpGet]
        public async Task<IActionResult> infrustructureWorkRequest()
        {
            var _repo = new JobManagementRepo(_context);
            var workAssetViewModel = new WorkRequestViewModel()
            {
                InfrustructureWorkRequest = new InfrustructureWorkRequest(),
                TypeOfRequests = await _repo.GetTypeOfRequest(),
                WorkStatuses = await _repo.GetWorkStatus(),
            };

            return View(workAssetViewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> infrustructureWorkRequest(WorkRequestViewModel workRequestView)
        {
            var work = workRequestView.InfrustructureWorkRequest;
            if (work == null)
                return BadRequest();

            var openStatusId = await GetWorkStatusId("Open");
            if (openStatusId == null)
                ModelState.AddModelError("", "The 'Open' work status is not configured.");

            if (ModelState.IsValid)
            {
                work.WorkStatusId = openStatusId!.Value;
                work.FacilityId = CurrentUser!.FacilityId;
                work.RequestedBy = CurrentUser.UserId;
                CreateEntity(work);
                _context.Add(work);
                await _context.SaveChangesAsync();
                await _notificationService.CreateWorkRequestNotification(work.FacilityId, CurrentUser.UserId);

                return RedirectToAction(nameof(Index));
            }

            TempData["WorkRequestError"] = ModelStateErrors();
            return RedirectToAction(nameof(infrustructureWorkRequest));
        }

        [HttpGet]
        public async Task<IActionResult> editWorkRequest(Guid id)
        {
            var _repo = new JobManagementRepo(_context);
            var workRequest = await _context.WorkRequest
                .Include(w => w.Asset).ThenInclude(a => a!.SubCategory)
                .Include(w => w.Facility)
                .FirstOrDefaultAsync(w => w.WorkRequestId == id);

            if (workRequest == null)
                return NotFound();
            if (!CanAccessFacility(workRequest.FacilityId))
                return AccessDenied();

            var workAssetViewModel = new WorkRequestViewModel()
            {
                WorkRequest = workRequest,
                FaultReports = await _repo.GetFaultReports(),
                WorkStatuses = await _repo.GetWorkStatus(),
            };

            return View(workAssetViewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> editWorkRequest(WorkRequestViewModel model)
        {
            var posted = model.WorkRequest;
            if (posted == null)
                return BadRequest();

            var existing = await _context.WorkRequest.FirstOrDefaultAsync(w => w.WorkRequestId == posted.WorkRequestId);
            if (existing == null)
                return NotFound();
            if (!CanAccessFacility(existing.FacilityId))
                return AccessDenied();

            var openStatusId = await GetWorkStatusId("Open");
            if (existing.WorkStatusId != openStatusId || existing.JobId != null)
                ModelState.AddModelError("", "Only open requests without a job can be edited.");

            if (!ModelState.IsValid)
            {
                TempData["WorkRequestError"] = ModelStateErrors();
                return RedirectToAction(nameof(editWorkRequest), new { id = existing.WorkRequestId });
            }

            // Only the user-editable fields are copied; everything else stays as stored.
            existing.RequestDate = posted.RequestDate;
            existing.Description = posted.Description;
            UpdateEntity(existing);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AutomateServiceRequest(Guid id)
        {
            if (await HasOpenWorkRequest(id))
            {
                TempData["Notification"] = JsonConvert.SerializeObject(new ToastNotification("An active work request exists for this asset.", NotificationType.Error));
                return RedirectToAction("Index", "Home");
            }

            var asset = await _assetManagementRepo.GetAssetById(id);
            if (asset == null)
                return NotFound();

            var openStatusId = await GetWorkStatusId("Open");
            if (openStatusId == null)
            {
                TempData["Error"] = "The 'Open' work status is not configured.";
                return RedirectToAction(nameof(Index));
            }

            var assetLocation = await _assetManagementRepo.GetAssetLocation(id);
            var workRequest = new WorkRequest()
            {
                AssetId = id,
                RequestDate = DateTime.Now,
                Description = "Service request for asset",
                FacilityId = assetLocation ?? CurrentUser!.FacilityId,
                WorkStatusId = openStatusId.Value,
            };

            await SaveNewWorkRequest(workRequest);
            return RedirectToAction(nameof(Index));
        }

        [AuthorizeRole(nameof(UserType.Administrator), nameof(UserType.Biomed))]
        public async Task<IActionResult> manageJobs()
        {
            ViewData["Biomed"] = new SelectList(_context.User.Where(f => f.RowState == RowStatus.Active && f.UserRole.UserType == Enumerators.UserType.Biomed), "UserId", "FirstName");
            return View(await JobData());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AuthorizeRole(nameof(UserType.Administrator), nameof(UserType.Biomed))]
        public async Task<IActionResult> createJobCard(JobViewModel jobView)
        {
            var job = jobView.Job;
            if (job == null)
                return BadRequest();

            var _repo = new JobManagementRepo(_context);

            // A Biomed can only claim a job for themselves.
            if (!isAdmin)
                job.AssignedTo = CurrentUser!.UserId;
            else if (!await _context.User.AnyAsync(u => u.UserId == job.AssignedTo && u.RowState == RowStatus.Active && u.UserRole.UserType == UserType.Biomed))
                ModelState.AddModelError("Job.AssignedTo", "Select a Biomed to assign the job to.");

            if (!(await _repo.GetFaultReports()).Any(f => f.Id == job.FaultReportId))
                ModelState.AddModelError("Job.FaultReportId", "Select a fault report.");

            if (!job.IsExternalProvider)
                job.ExternalProviderId = null;
            else if (job.ExternalProviderId == null)
                ModelState.AddModelError("Job.ExternalProviderId", "Select a service provider.");

            var openStatusId = await GetWorkStatusId("Open");
            var inProgressStatusId = await GetWorkStatusId("In Progress");
            if (openStatusId == null || inProgressStatusId == null)
                ModelState.AddModelError("", "The 'Open' and 'In Progress' work statuses must be configured.");

            WorkRequest? wRequest = null;
            InfrustructureWorkRequest? infraRequest = null;
            const string notOpenMessage = "This work request is no longer open for a new job.";

            if (job.WorkRequestId != null)
            {
                // ----- Asset Work Request -----
                wRequest = await _context.WorkRequest.FirstOrDefaultAsync(w => w.WorkRequestId == job.WorkRequestId);
                if (wRequest == null || !CanAccessFacility(wRequest.FacilityId))
                    ModelState.AddModelError("", "Work request not found.");
                else if (wRequest.WorkStatusId != openStatusId || wRequest.JobId != null
                         || await _context.Job.AnyAsync(j => j.WorkRequestId == wRequest.WorkRequestId))
                    ModelState.AddModelError("", notOpenMessage);
            }
            else if (job.InfrastructureWorkRequestId != null)
            {
                // ----- Infrastructure Work Request -----
                infraRequest = await _context.InfrustructureWorkRequest.FirstOrDefaultAsync(i => i.WorkRequestId == job.InfrastructureWorkRequestId);
                if (infraRequest == null || !CanAccessFacility(infraRequest.FacilityId))
                    ModelState.AddModelError("", "Work request not found.");
                else if (infraRequest.WorkStatusId != openStatusId || infraRequest.JobId != null
                         || await _context.Job.AnyAsync(j => j.InfrastructureWorkRequestId == infraRequest.WorkRequestId))
                    ModelState.AddModelError("", notOpenMessage);
            }
            else
            {
                ModelState.AddModelError("", "No work request was selected.");
            }

            if (!ModelState.IsValid)
            {
                TempData["JobError"] = ModelStateErrors();
                return RedirectToAction(nameof(manageJobs));
            }

            job.JobId = Guid.NewGuid();
            job.StatusId = inProgressStatusId!.Value;

            var facilityCode = CurrentUser!.Facility?.FacilityCode;
            job.JobNumber = $"{facilityCode}Jb-" + (await _context.Job.CountAsync() + 1).ToString("D3");

            if (wRequest != null)
            {
                job.AssetId = wRequest.AssetId;
                job.InfrastructureWorkRequestId = null;
                job.FacilityId = wRequest.FacilityId;
                wRequest.WorkStatusId = job.StatusId;
                wRequest.JobId = job.JobId;
                UpdateEntity(wRequest);
            }
            else
            {
                job.AssetId = null;
                job.WorkRequestId = null;
                job.FacilityId = infraRequest!.FacilityId;
                infraRequest.WorkStatusId = job.StatusId;
                infraRequest.JobId = job.JobId;
                UpdateEntity(infraRequest);
            }

            CreateEntity(job); // set CreatedBy, DateCreated, etc.
            _context.Add(job);

            await _context.SaveChangesAsync();

            // ----- Notifications -----
            if (isAdmin)
            {
                await _notificationService.CreateJobAssignmentNotification(job.AssignedTo,
                                                                           wRequest?.RequestedBy ?? infraRequest?.RequestedBy,
                                                                           CurrentUser.UserId);
            }
            else
            {
                await _notificationService.CreateJobClaimedNotification(job.AssignedTo,
                                                                        wRequest?.RequestedBy ?? infraRequest?.RequestedBy,
                                                                        CurrentUser.UserId);
            }

            return RedirectToAction(nameof(manageJobs));
        }

        private async Task<JobViewModel?> LoadJobCard(Guid id)
        {
            var _repo = new JobManagementRepo(_context);
            var job = await _context.Job
                .Include(w => w.Asset)
                .Include(w => w.InfraWorkRequest).ThenInclude(w => w!.TypeOfRequest)
                .Include(w => w.User)
                .Include(w => w.Status)
                .Include(w => w.FaultReport)
                .Include(w => w.ExternalProvider)
                .FirstOrDefaultAsync(j => j.JobId == id);

            if (job == null)
                return null;

            var jobView = new JobViewModel
            {
                Job = job,
                WorkStatuses = await _repo.GetWorkStatus(),
                WorkDoneList = await _context.WorkDone
                    .Where(w => w.JobId == job.JobId && w.RowState == RowStatus.Active)
                    .OrderByDescending(w => w.DateCompleted)
                    .ToListAsync(),
                WorkDone = new WorkDone()
            };

            // Load work request differently depending on type
            if (job.WorkRequestId != null) // Asset Job
            {
                jobView.WorkRequest = await _context.WorkRequest
                    .Include(w => w.Asset).ThenInclude(a => a!.SubCategory)
                    .Include(w => w.RequestedByUser)
                    .Include(w => w.Outcome)
                    .FirstOrDefaultAsync(w => w.WorkRequestId == job.WorkRequestId);
            }
            else if (job.InfrastructureWorkRequestId != null) // Infrastructure Job
            {
                jobView.InfraWorkRequest = await _context.InfrustructureWorkRequest
                    .Include(w => w.TypeOfRequest)
                    .Include(w => w.RequestedByUser)
                    .Include(w => w.Outcome)
                    .FirstOrDefaultAsync(w => w.WorkRequestId == job.InfrastructureWorkRequestId);
            }

            return jobView;
        }

        public async Task<IActionResult> jobCard(Guid id)
        {
            var jobView = await LoadJobCard(id);
            if (jobView == null)
                return NotFound();
            if (!CanViewJob(jobView.Job!))
                return AccessDenied();

            return View(jobView);
        }

        public async Task<IActionResult> externalJobCard(Guid id)
        {
            var jobView = await LoadJobCard(id);
            if (jobView == null)
                return NotFound();
            if (!CanViewJob(jobView.Job!))
                return AccessDenied();

            return View(jobView);
        }

        private IActionResult RedirectToJobCard(Job job) =>
            RedirectToAction(job.IsExternalProvider ? nameof(externalJobCard) : nameof(jobCard), new { id = job.JobId });

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> updateJobCard(JobViewModel jobView)
        {
            var posted = jobView.Job;
            if (posted == null)
                return BadRequest();

            var job = await _context.Job.FirstOrDefaultAsync(j => j.JobId == posted.JobId);
            if (job == null)
                return NotFound();
            if (!IsAssignee(job) || job.EndDate != null)
                return AccessDenied();

            // Only the fields the assignee may change are read from the form,
            // so the rest of the posted job is not validated.
            var allowedStatusIds = (await new JobManagementRepo(_context).GetWorkStatus())
                .Where(s => s.Name == "In Progress" || s.Name == "Completed")
                .Select(s => s.Id);
            if (!allowedStatusIds.Contains(posted.StatusId))
            {
                TempData["Error"] = "Select a valid job status.";
                return RedirectToJobCard(job);
            }
            if (job.IsExternalProvider && posted.Amount < 0)
            {
                TempData["Error"] = "Amount cannot be negative.";
                return RedirectToJobCard(job);
            }

            // ✅ Keep the linked work request's status in step with the job
            if (job.WorkRequestId != null)
            {
                var workRequest = await _context.WorkRequest.FirstOrDefaultAsync(w => w.WorkRequestId == job.WorkRequestId);
                if (workRequest != null)
                {
                    workRequest.WorkStatusId = posted.StatusId;
                    UpdateEntity(workRequest);
                }
            }
            else if (job.InfrastructureWorkRequestId != null)
            {
                var infraRequest = await _context.InfrustructureWorkRequest.FirstOrDefaultAsync(w => w.WorkRequestId == job.InfrastructureWorkRequestId);
                if (infraRequest != null)
                {
                    infraRequest.WorkStatusId = posted.StatusId;
                    UpdateEntity(infraRequest);
                }
            }

            // ✅ Update job itself
            job.StatusId = posted.StatusId;
            job.Remarks = posted.Remarks;

            if (job.IsExternalProvider)
            {
                job.Amount = posted.Amount;
                job.InvoiceNo = posted.InvoiceNo;
            }

            UpdateEntity(job);
            await _context.SaveChangesAsync();

            // 🔔 Notifications (TBD when service ready)
            // if (jobView.Job.StatusId != null && job.Status.Name == "Completed")
            //     await _notificationService.CreateJobCompletedNotification(workRequest.RequestedBy, CurrentUser.UserId);

            return RedirectToAction(nameof(manageJobs));
        }

        // Work done can only be recorded by the assignee while the job is still open.
        private async Task<bool> CanRecordWorkDone(Job job)
        {
            if (!IsAssignee(job) || job.EndDate != null)
                return false;
            var completedStatusId = await GetWorkStatusId("Completed");
            return job.StatusId != completedStatusId;
        }

        private async Task<IActionResult> SaveWorkDone(WorkDone? workDone)
        {
            if (workDone == null)
                return BadRequest();

            var job = await _context.Job.FirstOrDefaultAsync(j => j.JobId == workDone.JobId);
            if (job == null)
                return NotFound();
            if (!await CanRecordWorkDone(job))
                return AccessDenied();

            if (!ModelState.IsValid)
            {
                TempData["WorkDoneError"] = ModelStateErrors();
                return RedirectToJobCard(job);
            }

            if (workDone.WorkDoneId == Guid.Empty)
            {
                // ✅ New work
                workDone.WorkDoneId = Guid.NewGuid();
                workDone.JobId = job.JobId;
                CreateEntity(workDone);
                _context.Add(workDone);
            }
            else
            {
                // ✅ Edit existing, only when it belongs to this job
                var existingWork = await _context.WorkDone.FirstOrDefaultAsync(w =>
                    w.WorkDoneId == workDone.WorkDoneId && w.JobId == job.JobId && w.RowState == RowStatus.Active);
                if (existingWork == null)
                    return NotFound();

                existingWork.DateCompleted = workDone.DateCompleted;
                existingWork.Hours = workDone.Hours;
                existingWork.Details = workDone.Details;
                UpdateEntity(existingWork); // your audit/update tracking
            }

            await _context.SaveChangesAsync();

            // Stay on same job card
            return RedirectToJobCard(job);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> workDone(JobViewModel wView) => SaveWorkDone(wView.WorkDone);

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> ExWorkDone(JobViewModel wView) => SaveWorkDone(wView.WorkDone);

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> deleteWorkDone(Guid id)
        {
            var work = await _context.WorkDone.FirstOrDefaultAsync(w => w.WorkDoneId == id && w.RowState == RowStatus.Active);
            if (work == null)
                return NotFound();

            var job = await _context.Job.FirstOrDefaultAsync(j => j.JobId == work.JobId);
            if (job == null)
                return NotFound();
            if (!await CanRecordWorkDone(job))
                return AccessDenied();

            UpdateEntity(work);
            work.RowState = RowStatus.Deleted;
            await _context.SaveChangesAsync();

            return RedirectToJobCard(job);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> closeRequest(WorkRequestViewModel workRequestView)
        {
            var posted = workRequestView.WorkRequest;
            if (posted == null)
                return BadRequest();

            var id = posted.WorkRequestId;
            var _repo = new JobManagementRepo(_context);

            // Try normal work request first, then infrastructure
            var work = await _context.WorkRequest.FirstOrDefaultAsync(w => w.WorkRequestId == id);
            var infraWork = work == null
                ? await _context.InfrustructureWorkRequest.FirstOrDefaultAsync(i => i.WorkRequestId == id)
                : null;

            if (work == null && infraWork == null)
                return NotFound();

            var requestedBy = work != null ? work.RequestedBy : infraWork!.RequestedBy;
            var alreadyClosed = work != null
                ? work.CloseDate != null || work.OutcomeId != null
                : infraWork!.CloseDate != null || infraWork.OutcomeId != null;

            if (!CanCloseRequest(requestedBy))
                return AccessDenied();

            var closedStatusId = await GetWorkStatusId("Closed");
            string? error = null;
            if (alreadyClosed)
                error = "This request has already been closed.";
            else if (closedStatusId == null)
                error = "The 'Closed' work status is not configured.";
            else if (!(await _repo.GetOutcomes()).Any(o => o.Id == posted.OutcomeId))
                error = "Select an outcome.";

            if (error != null)
            {
                TempData["Error"] = error;
                return RedirectToAction(nameof(Index));
            }

            var closeDate = posted.CloseDate ?? DateTime.Now;

            // Get job linked to either request type
            var job = work != null
                ? await _context.Job.FirstOrDefaultAsync(j => j.WorkRequestId == id)
                : await _context.Job.FirstOrDefaultAsync(j => j.InfrastructureWorkRequestId == id);

            if (job != null)
            {
                job.EndDate = closeDate;
                job.StatusId = closedStatusId!.Value;
                UpdateEntity(job);
            }

            // Update whichever request type was found
            if (work != null)
            {
                work.OutcomeId = posted.OutcomeId;
                work.CloseDate = closeDate;
                work.WorkStatusId = closedStatusId!.Value;
                UpdateEntity(work);

                if(work.Description!.ToLower() == "service request for asset")
                {
                    var asset = await _context.Assets.FirstOrDefaultAsync(a => a.AssetId == work.AssetId);
                    if (asset is not null && asset.IsServiceable && asset.ServiceInterval != null)
                    {
                        asset.NextServiceDate = DateTime.Now.AddMonths((int)asset.ServiceInterval);
                        UpdateEntity(asset);
                    }
                }
            }
            else
            {
                infraWork!.OutcomeId = posted.OutcomeId;
                infraWork.CloseDate = closeDate;
                infraWork.WorkStatusId = closedStatusId!.Value;
                UpdateEntity(infraWork);
            }

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AuthorizeRole(nameof(UserType.Administrator))]
        public async Task<IActionResult> cancelRequest(WorkRequestViewModel workRequestView)
        {
            var posted = workRequestView.WorkRequest;
            if (posted == null)
                return BadRequest();

            var id = posted.WorkRequestId;
            var _repo = new JobManagementRepo(_context);

            // normal work request first, then infrastructure
            var work = await _context.WorkRequest.FirstOrDefaultAsync(w => w.WorkRequestId == id);
            var infraWork = work == null
                ? await _context.InfrustructureWorkRequest.FirstOrDefaultAsync(i => i.WorkRequestId == id)
                : null;

            if (work == null && infraWork == null)
                return NotFound();

            var alreadyClosed = work != null ? work.CloseDate != null : infraWork!.CloseDate != null;
            var cancelledStatusId = await GetWorkStatusId("Cancelled");
            string? error = null;
            if (alreadyClosed)
                error = "This request has already been closed.";
            else if (cancelledStatusId == null)
                error = "The 'Cancelled' work status is not configured.";
            else if (!(await _repo.GetOutcomes()).Any(o => o.Id == posted.OutcomeId))
                error = "Select an outcome.";
            else if (!(await _repo.GetCancelReasons()).Any(r => r.Id == posted.CancelReasonId))
                error = "Select a cancel reason.";

            if (error != null)
            {
                TempData["Error"] = error;
                return RedirectToAction(nameof(Index));
            }

            var closeDate = posted.CloseDate ?? DateTime.Now;

            // A cancelled request must not leave its job running.
            var job = work != null
                ? await _context.Job.FirstOrDefaultAsync(j => j.WorkRequestId == id)
                : await _context.Job.FirstOrDefaultAsync(j => j.InfrastructureWorkRequestId == id);
            if (job != null && job.EndDate == null)
            {
                job.StatusId = cancelledStatusId!.Value;
                job.EndDate = closeDate;
                UpdateEntity(job);
            }

            if (work != null)
            {
                work.OutcomeId = posted.OutcomeId;
                work.CloseDate = closeDate;
                work.WorkStatusId = cancelledStatusId!.Value;
                work.CancelReasonId = posted.CancelReasonId;
                UpdateEntity(work);
            }
            else
            {
                infraWork!.OutcomeId = posted.OutcomeId;
                infraWork.CloseDate = closeDate;
                infraWork.WorkStatusId = cancelledStatusId!.Value;
                infraWork.CancelReasonId = posted.CancelReasonId;
                UpdateEntity(infraWork);
            }

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }
    }
}
