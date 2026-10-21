using EMMS.Constants;
using EMMS.CustomAttributes;
using EMMS.Data;
using EMMS.Data.Repository;
using EMMS.Models;
using EMMS.Models.Entities;
using EMMS.Service;
using EMMS.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using static EMMS.Models.Enumerators;

namespace EMMS.Controllers
{
    /// <summary>
    /// Pages for work requests and jobs. Every status change is made by <see cref="WorkRequestService"/>.
    /// </summary>
    [RequireLogin]
    public class JobManagementController : BaseController
    {
        private readonly ApplicationDbContext _context;
        private readonly AssetService _assetService;
        private readonly AssetManagementRepo _assetManagementRepo;
        private readonly WorkRequestService _workRequests;
        private readonly JobManagementRepo _repo;
        public JobManagementController(ApplicationDbContext context, AssetService assetService, AssetManagementRepo assetMovementRepo, WorkRequestService workRequests, JobManagementRepo repo)
        {
            _context = context;
            _assetService = assetService;
            _assetManagementRepo = assetMovementRepo;
            _workRequests = workRequests;
            _repo = repo;
        }

        // Admins see every facility; everyone else their own.
        private int? FacilityScope => isAdmin ? null : CurrentUser!.FacilityId;

        private IActionResult AccessDenied()
        {
            TempData["Error"] = "You do not have access to that record.";
            return RedirectToAction("Index", "Home");
        }

        private string? ModelStateErrors()
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            return errors.Count == 0 ? null : string.Join("; ", errors);
        }

        private IActionResult RedirectToJobCard(Job job) =>
            RedirectToAction(job.IsExternalProvider ? nameof(externalJobCard) : nameof(jobCard), new { id = job.JobId });

        private IActionResult RedirectBack(string? returnTo) =>
            RedirectToAction(returnTo == nameof(manageJobs) ? nameof(manageJobs) : nameof(Index));

        private async Task<JobViewModel> JobData()
        {
            return new JobViewModel
            {
                Jobs = await _repo.GetActiveJobsAsync(isAdmin ? null : CurrentUser!.UserId),
                WorkRequests = await _repo.GetUnassignedWorkRequestsAsync(FacilityScope),
                InfrastructureWorkRequests = await _repo.GetUnassignedInfrastructureRequestsAsync(FacilityScope),
                Job = new Job(),
                FaultReports = await _repo.GetFaultReports(),
                WorkStatuses = await _workRequests.GetStatusesAsync(),
                Outcomes = await _workRequests.GetOutcomesAsync(),
                ServiceProviders = await _assetManagementRepo.GetServiceProviders(),
                CancelReasons = await _workRequests.GetCancelReasonsAsync(),
            };
        }

        // ------------------------------------------------------------------------------------------
        // Work requests
        // ------------------------------------------------------------------------------------------
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var data = new WorkRequestViewModel
            {
                WorkRequest = new WorkRequest(),
                AssetIndex = await _assetService.GetAssetIndexViewModel(CurrentUser!),
                WorkRequests = await _repo.GetWorkRequestsAsync(FacilityScope),
                InfrastructureWorkRequests = await _repo.GetInfrastructureRequestsAsync(FacilityScope),
                Outcomes = await _workRequests.GetOutcomesAsync(),
                WorkStatuses = await _workRequests.GetStatusesAsync(),
                CancelReasons = await _workRequests.GetCancelReasonsAsync(),
            };
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

            var workAssetViewModel = new WorkRequestViewModel()
            {
                Asset = asset,
                WorkRequest = new WorkRequest() { AssetId = id },
                WorkStatuses = await _workRequests.GetStatusesAsync(),
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

            var error = ModelStateErrors()
                ?? await _workRequests.CreateAssetRequestAsync(CurrentUser!, work.AssetId.Value, work.RequestDate, work.Description);
            if (error == null)
                return RedirectToAction(nameof(Index));

            TempData["WorkRequestError"] = error;
            return RedirectToAction(nameof(workRequest), new { id = work.AssetId });
        }

        [HttpGet]
        public async Task<IActionResult> infrustructureWorkRequest()
        {
            var workAssetViewModel = new WorkRequestViewModel()
            {
                InfrustructureWorkRequest = new InfrustructureWorkRequest(),
                TypeOfRequests = await _repo.GetTypeOfRequest(),
                WorkStatuses = await _workRequests.GetStatusesAsync(),
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

            var error = ModelStateErrors()
                ?? await _workRequests.CreateInfrastructureRequestAsync(CurrentUser!, work.TypeOfRequestId, work.RequestDate, work.Description);
            if (error == null)
                return RedirectToAction(nameof(Index));

            TempData["WorkRequestError"] = error;
            return RedirectToAction(nameof(infrustructureWorkRequest));
        }

        [HttpGet]
        public async Task<IActionResult> editWorkRequest(Guid id)
        {
            var workRequest = await _repo.GetWorkRequestForEditAsync(id);

            if (workRequest == null)
                return NotFound();
            if (!WorkRequestService.CanAccessFacility(CurrentUser!, workRequest.FacilityId))
                return AccessDenied();

            var workAssetViewModel = new WorkRequestViewModel()
            {
                WorkRequest = workRequest,
                WorkStatuses = await _workRequests.GetStatusesAsync(),
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

            var error = ModelStateErrors()
                ?? await _workRequests.EditRequestAsync(CurrentUser!, posted.WorkRequestId, posted.RequestDate, posted.Description);
            if (error == null)
                return RedirectToAction(nameof(Index));

            TempData["WorkRequestError"] = error;
            return RedirectToAction(nameof(editWorkRequest), new { id = posted.WorkRequestId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AutomateServiceRequest(Guid id)
        {
            var error = await _workRequests.CreateServiceRequestAsync(CurrentUser!, id);
            if (error != null)
                TempData["Error"] = error;
            else
                TempData["Success"] = "Service request created.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> closeRequest(WorkRequestViewModel workRequestView, string? returnTo)
        {
            var posted = workRequestView.WorkRequest;
            if (posted == null)
                return BadRequest();

            var error = await _workRequests.CloseRequestAsync(CurrentUser!, posted.WorkRequestId, posted.CloseDate, posted.OutcomeId);
            if (error != null)
                TempData["Error"] = error;
            return RedirectBack(returnTo);
        }

        // Admins cancel any request that is not final; requesters withdraw their own before a job exists.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> cancelRequest(WorkRequestViewModel workRequestView, string? returnTo)
        {
            var posted = workRequestView.WorkRequest;
            if (posted == null)
                return BadRequest();

            var error = await _workRequests.CancelRequestAsync(CurrentUser!, posted.WorkRequestId, posted.CloseDate, posted.OutcomeId, posted.CancelReasonId);
            if (error != null)
                TempData["Error"] = error;
            return RedirectBack(returnTo);
        }

        // ------------------------------------------------------------------------------------------
        // Jobs
        // ------------------------------------------------------------------------------------------
        [AuthorizeRole(nameof(UserType.Administrator), nameof(UserType.Biomed))]
        public async Task<IActionResult> manageJobs()
        {
            ViewData["Biomed"] = new SelectList(await _context.User.AsNoTracking()
                .Where(f => f.RowState == RowStatus.Active && f.UserRole!.UserType == UserType.Biomed)
                .OrderBy(f => f.FirstName)
                .ToListAsync(), "UserId", "FirstName");
            return View(await JobData());
        }

        // Open jobs per technician. Admins see every facility, facility managers their own.
        [AuthorizeRole(nameof(UserType.Administrator), nameof(UserType.FacilityManager))]
        public async Task<IActionResult> workload()
        {
            var model = WorkloadViewModel.Build(await _repo.GetOpenJobsAsync(FacilityScope), await _repo.GetBiomedsAsync(FacilityScope));
            model.ReassignTo = await _repo.GetBiomedsAsync(FacilityScope);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AuthorizeRole(nameof(UserType.Administrator), nameof(UserType.FacilityManager))]
        public async Task<IActionResult> reassignJob(Guid jobId, Guid? assignedTo)
        {
            var (job, error) = await _workRequests.ReassignJobAsync(CurrentUser!, jobId, assignedTo);
            if (job == null)
                return NotFound();
            if (error != null)
                TempData["Error"] = error;
            else
                TempData["Success"] = $"Job {job.JobNumber} reassigned.";
            return RedirectToAction(nameof(workload));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AuthorizeRole(nameof(UserType.Administrator), nameof(UserType.Biomed))]
        public async Task<IActionResult> createJobCard(JobViewModel jobView)
        {
            var job = jobView.Job;
            if (job == null)
                return BadRequest();

            var (_, error) = await _workRequests.CreateJobAsync(CurrentUser!, job.WorkRequestId, job.InfrastructureWorkRequestId,
                job.AssignedTo, job.FaultReportId, job.StartDate, job.IsExternalProvider, job.ExternalProviderId);
            if (error != null)
                TempData["JobError"] = error;

            return RedirectToAction(nameof(manageJobs));
        }

        private async Task<JobViewModel?> LoadJobCard(Guid id)
        {
            var job = await _repo.GetJobAsync(id);

            if (job == null)
                return null;

            var jobView = new JobViewModel
            {
                Job = job,
                WorkStatuses = await _workRequests.GetStatusesAsync(),
                WorkDoneList = await _repo.GetWorkDoneForJobAsync(job.JobId),
                WorkDone = new WorkDone(),
                Parts = await _repo.GetPartsForJobAsync(job.JobId),
                JobPart = new JobPart(),
                SpareParts = await _workRequests.GetSparePartsAsync()
            };

            // Load work request differently depending on type
            if (job.WorkRequestId != null) // Asset Job
            {
                jobView.WorkRequest = await _repo.GetWorkRequestDetailAsync(job.WorkRequestId.Value);
            }
            else if (job.InfrastructureWorkRequestId != null) // Infrastructure Job
            {
                jobView.InfraWorkRequest = await _repo.GetInfrastructureRequestDetailAsync(job.InfrastructureWorkRequestId.Value);
            }

            return jobView;
        }

        public async Task<IActionResult> jobCard(Guid id)
        {
            var jobView = await LoadJobCard(id);
            if (jobView == null)
                return NotFound();
            if (!WorkRequestService.CanViewJob(CurrentUser!, jobView.Job!))
                return AccessDenied();

            return View(jobView);
        }

        public async Task<IActionResult> externalJobCard(Guid id)
        {
            var jobView = await LoadJobCard(id);
            if (jobView == null)
                return NotFound();
            if (!WorkRequestService.CanViewJob(CurrentUser!, jobView.Job!))
                return AccessDenied();

            return View(jobView);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> updateJobCard(JobViewModel jobView)
        {
            var posted = jobView.Job;
            if (posted == null)
                return BadRequest();

            // Only the fields the assignee may change are read from the form.
            var (job, error) = await _workRequests.UpdateJobAsync(CurrentUser!, posted.JobId, posted.StatusId, posted.Remarks, posted.Amount, posted.InvoiceNo);
            if (job == null)
                return NotFound();
            if (error != null)
            {
                TempData["Error"] = error;
                return RedirectToJobCard(job);
            }

            return RedirectToAction(nameof(manageJobs));
        }

        private async Task<IActionResult> SaveWorkDone(WorkDone? workDone)
        {
            if (workDone == null)
                return BadRequest();

            // Binding errors (e.g. an unreadable date) stop the save before the service's own checks.
            if (ModelStateErrors() is string modelErrors)
            {
                var current = await _context.Job.AsNoTracking().FirstOrDefaultAsync(j => j.JobId == workDone.JobId);
                if (current == null)
                    return NotFound();
                TempData["WorkDoneError"] = modelErrors;
                return RedirectToJobCard(current);
            }

            var (job, error) = await _workRequests.SaveWorkDoneAsync(CurrentUser!, workDone);
            if (job == null)
                return NotFound();
            if (error != null)
                TempData["WorkDoneError"] = error;
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
            var (job, error) = await _workRequests.DeleteWorkDoneAsync(CurrentUser!, id);
            if (job == null)
                return NotFound();
            if (error != null)
                TempData["WorkDoneError"] = error;
            return RedirectToJobCard(job);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> jobPart(JobViewModel jView)
        {
            var posted = jView.JobPart;
            if (posted == null)
                return BadRequest();

            if (ModelStateErrors() is string modelErrors)
            {
                var current = await _context.Job.AsNoTracking().FirstOrDefaultAsync(j => j.JobId == posted.JobId);
                if (current == null)
                    return NotFound();
                TempData["JobPartError"] = modelErrors;
                return RedirectToJobCard(current);
            }

            var (job, error) = await _workRequests.SaveJobPartAsync(CurrentUser!, posted);
            if (job == null)
                return NotFound();
            if (error != null)
                TempData["JobPartError"] = error;
            return RedirectToJobCard(job);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> deleteJobPart(Guid id)
        {
            var (job, error) = await _workRequests.DeleteJobPartAsync(CurrentUser!, id);
            if (job == null)
                return NotFound();
            if (error != null)
                TempData["JobPartError"] = error;
            return RedirectToJobCard(job);
        }
    }
}
