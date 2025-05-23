using System.Diagnostics;
using System.Threading.Tasks;
using EMMS.Data;
using EMMS.Data.Repository;
using EMMS.Models;
using EMMS.Models.Entities;
using EMMS.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static EMMS.Models.Enumerators;

namespace EMMS.Controllers
{
    public class JobManagementController : Controller
    {
        private readonly ApplicationDbContext _context;
        public JobManagementController(ApplicationDbContext context)
        {
            _context = context;
        }
        

        public async Task<WorkRequestViewModel> WorkRequestData( WorkRequest? workmodel = null)
        {
            if (workmodel is null)
            {
                workmodel = new WorkRequest();
            }
            var _repo = new JobManagementRepo(_context);
            var assets = await new AssetManagement(_context).assetViewModel();
            WorkRequestViewModel paginatedWorkRequest = new WorkRequestViewModel
            {
                WorkRequests = await _repo.GetWorkRequests(),
                WorkRequest = workmodel,
                AssetIndex = assets,
                Outcomes = await _repo.GetOutcomes(),
                WorkStatuses = await _repo.GetWorkStatus(),
                //Assets = await _Assetrepo.GetAssetsFromDb(),
            };

            return paginatedWorkRequest;

        }

        public async Task<JobViewModel> JobData(Job? jobmodel = null)
        {
            if (jobmodel is null)
            {
                jobmodel = new Job();
            }
            var _repo = new JobManagementRepo(_context);
            var _Assetrepo = new AssetManagementRepo(_context);
            var all = await _repo.GetJobfromDbs();
            JobViewModel paginatedJob = new JobViewModel
            {
                Jobs = await _repo.GetJobfromDbs(),
                WorkRequests = await _repo.GetWorkRequests(),
                Job = jobmodel,
                //Asset = await assets.GetAssetsFromDb(),
                FaultReports = await _repo.GetFaultReports(),
                WorkStatuses = await _repo.GetWorkStatus(),
                Outcomes = await _repo.GetOutcomes(),
                ServiceProviders = await _Assetrepo.GetServiceProviders(),
            };

            return paginatedJob;

        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            return View(await WorkRequestData());
        }
        [HttpGet]
        public async Task<IActionResult> workRequest(Guid id)
        {
            var _arepo = new AssetManagementRepo(_context).GetAssetsFromDb().Result.FirstOrDefault(a => a.AssetId == id);
            var _repo = new JobManagementRepo(_context);
            var workRequest = new WorkRequest()
            {
                AssetId = id

            };
            var workAssetViewModel = new WorkRequestViewModel()
            {
                AssetTag = _arepo.AssetTagNumber,
                WorkRequest = workRequest,
                FaultReports = await _repo.GetFaultReports(),
                WorkStatuses = await _repo.GetWorkStatus(),

            };


            return View(workAssetViewModel);
        }
        [HttpPost]
        public async Task<IActionResult> WorkRequest(WorkRequestViewModel workRequestView)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            TempData["Error"] = string.Join("; ", errors);
            if (ModelState.IsValid)
            {
                var work = workRequestView.WorkRequest;
                Debug.WriteLine($"WorkRequest: {work!.FaultReportId}");


                work.FacilityId = 2; //TBD Update with logged in facility;
                work.DateCreated = DateTime.Now;
                work.RowState = RowStatus.Active;
                work.RequestedBy = Guid.NewGuid();
                work.CreatedBy = Guid.NewGuid(); // TBD Replace with actual user ID

                _context.Add(work);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return RedirectToAction(nameof(workRequest),new { id = workRequestView.WorkRequest!.AssetId });
        }

        public async Task<IActionResult> manageJobs()
        {
            return View(await JobData());
        }
        public async Task<IActionResult> createJobCard(JobViewModel jobView)
        {
            var _repo = new JobManagementRepo(_context);
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            TempData["Error"] = string.Join("; ", errors);
            if (ModelState.IsValid)
            {
                var job = jobView.Job;
                var wRequest = _repo.GetWorkRequests().Result.FirstOrDefault(w => w.WorkRequestId == job.WorkRequestId);
                wRequest.DateModified = DateTime.Now;
                wRequest.WorkStatusId = job.StatusId;


                job.FacilityId = 2; //TBD Update with logged in facility;
                job.DateCreated = DateTime.Now;
                job.RowState = RowStatus.Active;
                job.CreatedBy = Guid.NewGuid(); // TBD Replace with actual user ID

                _context.Add(job);
                _context.Update(wRequest);

                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(manageJobs));
            }
            return View("manageJobs",await JobData());
        }
        public async Task<IActionResult> jobCard(int id)
        {
            var _repo = new JobManagementRepo(_context);
            var job = _repo.GetJobfromDbs().Result.FirstOrDefault(j => j.JobId == id);
            JobViewModel jobView = new JobViewModel()
            {
                WorkRequest = _repo.GetWorkRequests().Result.FirstOrDefault(w => w.WorkRequestId == job!.WorkRequestId),
                Job = job!,
                WorkStatuses = await _repo.GetWorkStatus(),
                WorkDoneList = _repo.GetWorkDone().Result.Where(w => w.JobId == job.JobId),
                WorkDone = new WorkDone(),
                //ExWorkDoneList = GetExWorkDone().Result.Where(w => w.JobId == job.JobId),
                //ExWorkDone = new ExternalWorkDone(),

            };
            return View(jobView);
        }
        [HttpPost]
        public async Task<IActionResult> updateJobCard(JobViewModel jobView)
        {

            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            TempData["Error"] = string.Join("; ", errors);

            if (ModelState.IsValid)
            {
                var _repo = new JobManagementRepo(_context);
                var job = _repo.GetJobfromDbs().Result.FirstOrDefault(j => j.JobId == jobView.Job!.JobId);
                //var job =  jobView.Job;
                var workRequest = _repo.GetWorkRequests().Result.FirstOrDefault(w => w.WorkRequestId == job!.WorkRequestId);
                workRequest!.WorkStatusId = jobView.Job.StatusId;
                job.StatusId = jobView.Job.StatusId;
                job.DateModified = DateTime.Now;
                job.Remarks = jobView.Job.Remarks;
                _context.Update(job);
                _context.Update(workRequest);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(manageJobs));

            }
            return RedirectToAction(nameof(jobCard), new { id = jobView.Job!.JobId });

        }
        [HttpPost]
        public async Task<IActionResult> workDone(JobViewModel wView)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            TempData["Error"] = string.Join("; ", errors);

            if (ModelState.IsValid)
            {
                //var _job = wView.Job;
                var workDone = wView.WorkDone;
                workDone.WorkDoneId = Guid.NewGuid();
                workDone.JobId = wView.WorkDone.JobId;
                workDone.DateCreated = DateTime.Now;
                workDone.RowState = RowStatus.Active;
                workDone.CreatedBy = Guid.NewGuid(); // TBD Replace with actual user ID
                _context.Add(workDone);
                await _context.SaveChangesAsync();
                // Stay on the same job card
                return RedirectToAction(nameof(jobCard), new { id = wView.WorkDone.JobId });
            }

            return RedirectToAction(nameof(jobCard), new { id = wView.WorkDone!.JobId });
        }
        [HttpPost]
        public async Task<IActionResult> ExWorkDone(JobViewModel ExView)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            TempData["Error"] = string.Join("; ", errors);

            if (ModelState.IsValid)
            {
                //var _job = wView.Job;
                var exworkDone = ExView.ExWorkDone;
                exworkDone.ExternalWorkDoneId = Guid.NewGuid();
                exworkDone.DateCreated = DateTime.Now;
                exworkDone.RowState = RowStatus.Active;
                exworkDone.CreatedBy = Guid.NewGuid(); // TBD Replace with actual user ID
                _context.Add(exworkDone);
                await _context.SaveChangesAsync();
                // Stay on the same job card
                return RedirectToAction(nameof(externalJobCard), new { id = ExView.ExWorkDone.JobId });
            }

            return RedirectToAction(nameof(externalJobCard), new { id = ExView.ExWorkDone.JobId });
        }
        public async Task<IActionResult> externalJobCard(int id)
        {
            var _repo = new JobManagementRepo(_context);
            var job = _repo.GetJobfromDbs().Result.FirstOrDefault(j => j.JobId == id);
            JobViewModel jobView = new JobViewModel()
            {
                WorkRequest = _repo.GetWorkRequests().Result.FirstOrDefault(w => w.WorkRequestId == job!.WorkRequestId),
                Job = job!,
                WorkStatuses = await _repo.GetWorkStatus(),
               // WorkDoneList = await GetWorkDone(),
                //WorkDone = new WorkDone(),
                ExWorkDoneList = _repo.GetExWorkDone().Result.Where(w => w.JobId == job.JobId),
                ExWorkDone = new ExternalWorkDone(),

            };
            return View(jobView);
        }
        [HttpPost]
        public async Task<IActionResult> closeRequest(WorkRequestViewModel workRequestView)
        {
            var id = workRequestView.WorkRequest.WorkRequestId;
            var _repo = new JobManagementRepo(_context);
            var work = _repo.GetWorkRequests().Result.FirstOrDefault(w => w.WorkRequestId == id);
            var job = _repo.GetJobfromDbs().Result.FirstOrDefault(j => j.WorkRequestId == id);
            work.OutcomeId = workRequestView.WorkRequest.OutcomeId;
            work.CloseDate = workRequestView.WorkRequest.CloseDate;
            work.WorkStatusId = workRequestView.WorkRequest.WorkStatusId;
            job.EndDate = workRequestView.WorkRequest.CloseDate;
            job.StatusId = workRequestView.WorkRequest.WorkStatusId;
            work.DateModified = DateTime.Now;
            job.DateModified = DateTime.Now;

            _context.Update(job!);
            _context.Update(work!);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }
    }
}
