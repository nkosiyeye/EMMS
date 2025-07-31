using EMMS.CustomAttributes;
using EMMS.Data;
using EMMS.Data.Repository;
using EMMS.Models;
using EMMS.Models.Entities;
using EMMS.Service;
using EMMS.Utility;
using EMMS.ViewModels;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Threading.Tasks;
using static EMMS.Models.Enumerators;

namespace EMMS.Controllers
{
    public class JobManagementController : BaseController
    {
        private readonly ApplicationDbContext _context;
        private readonly AssetService _assetService;
        public JobManagementController(ApplicationDbContext context)
        {
            _context = context;
            _assetService = new AssetService(context);
        }
        
        public async Task<WorkRequestViewModel> WorkRequestData( WorkRequest? workmodel = null)
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

        public async Task<JobViewModel> JobData(Job? jobmodel = null)
        {
            if (jobmodel is null)
            {
                jobmodel = new Job();
            }
            var _repo = new JobManagementRepo(_context);
            var _Assetrepo = new AssetManagementRepo(_context);
            var all = await _repo.GetJobfromDbs();
            var filteredJobs = all.Where(w => w.FacilityId == CurrentUser.FacilityId || w.AssignedTo == CurrentUser.UserId);
            var allWork = await _repo.GetWorkRequests();
            var filteredWork = allWork.Where(w => w.FacilityId == CurrentUser.FacilityId);
            JobViewModel paginatedJob = new JobViewModel
            {
                Jobs = !isAdmin ? filteredJobs : all,
                WorkRequests = !isAdmin ? filteredWork : allWork,
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
        [RequireLogin]
        public async Task<IActionResult> Index()
        {
            var _repo = new JobManagementRepo(_context);
            var allWork = await _repo.GetWorkRequests();
            var filteredWork = allWork.Where(w => w.FacilityId == CurrentUser.FacilityId);
            var data = await WorkRequestData();
            data.WorkRequests = !isAdmin ? filteredWork : allWork;//.Result.Where(w => w.FacilityId == CurrentUser.FacilityId);
            data.Outcomes = await _repo.GetOutcomes();
            data.WorkStatuses = await _repo.GetWorkStatus();
            return View(data);
        }

        [HttpGet]
        [RequireLogin]
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

            var work = workRequestView.WorkRequest;
            var workrequest = _context.WorkRequest.OrderByDescending(m => m.DateCreated)
                   .FirstOrDefault(m => m.AssetId == work!.AssetId);
            if (workrequest != null)
            {
                if (workrequest.CloseDate == null) ModelState.AddModelError("", "Asset Already has a workrequest in progress.");
            }
            if (ModelState.IsValid)
            {
                work.RequestedBy = CurrentUser.UserId;
                CreateEntity(work);
                var assetTag = new AssetManagementRepo(_context).GetAssetsFromDb().Result.FirstOrDefault(a => a.AssetId == work.AssetId)!.AssetTagNumber;

                var notification = new Models.Entities.Notification
                {
                    Message = $"New work requested for: {assetTag}",
                    Type = "work",
                    DateCreated = DateTime.Now,
                    FacilityId = work.FacilityId,
                    RowState = RowStatus.Active
                };
                _context.Notifications.Add(notification);

                _context.Add(work);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            TempData["WorkRequestError"] = string.Join("; ", errors);
            return RedirectToAction(nameof(workRequest),new { id = workRequestView.WorkRequest!.AssetId });
        }

        public async Task<IActionResult> AutomateServiceRequest(Guid id)
        {
            var _arepo = new AssetManagementRepo(_context);
            var _repo = new JobManagementRepo(_context);
            
            var activeRequest = await _repo.GetWorkRequestByAssetId(id);    
            if (activeRequest != null)
            {
                TempData["Notification"] = JsonConvert.SerializeObject(new ToastNotification("An active work request exists for this asset.", NotificationType.Success));
                return RedirectToAction("Index", "Home");

            }

            var asset = await _arepo.GetAssetById(id);
            var assetLocation = await _arepo.GetAssetLocation(id);
            var WorkStatuses = await _repo.GetWorkStatus();
            var workStatus = WorkStatuses.FirstOrDefault(w => w.Name == "Open");

            var workRequest = new WorkRequest()
            {
                AssetId = id,
                RequestDate = DateTime.Now,
                Description = "Service request for asset",
                FacilityId = (int)assetLocation,
                WorkStatusId = workStatus.Id,
                RequestedBy = CurrentUser!.UserId,
                CreatedBy = CurrentUser!.UserId,
                DateCreated = DateTime.Now,
                RowState = RowStatus.Active,
            };

            var workAssetViewModel = new WorkRequestViewModel()
            {
                AssetTag = asset?.AssetTagNumber,
                WorkRequest = workRequest,
            };

            await WorkRequest(workAssetViewModel);
            return RedirectToAction(nameof(Index));
        }

        [RequireLogin]
        [AuthorizeRole(nameof(UserType.Administrator), nameof(UserType.Biomed))]
        public async Task<IActionResult> manageJobs()
        {
            ViewData["Biomed"] = new SelectList(_context.User.Where(f => f.RowState == RowStatus.Active && f.UserRole.UserType == Enumerators.UserType.Biomed), "UserId", "FirstName");
            return View(await JobData());
        }

        [RequireLogin]
        public async Task<IActionResult> createJobCard(JobViewModel jobView)
        {
            var _repo = new JobManagementRepo(_context);
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            TempData["Error"] = string.Join("; ", errors);
            if (ModelState.IsValid)
            {
                var job = jobView.Job;
                job.JobId = Guid.NewGuid();
                var wRequest = _repo.GetWorkRequests().Result.FirstOrDefault(w => w.WorkRequestId == job.WorkRequestId);
                wRequest.DateModified = DateTime.Now;
                wRequest.WorkStatusId = job.StatusId;
                wRequest.JobId = job.JobId;
                job.FacilityId = wRequest.FacilityId; //TBD Update with logged in facility;
                CreateEntity(job); // TBD Replace with actual user ID

                _context.Add(job);
                _context.Update(wRequest);

                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(manageJobs));
            }
            return View("manageJobs",await JobData());
        }

        public async Task<IActionResult> jobCard(Guid id)
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
                if (job.IsExternalProvider)
                {
                    job.Amount = jobView.Job.Amount;
                    job.InvoiceNo = jobView.Job.InvoiceNo;
                }
                _context.Update(job);
                _context.Update(workRequest);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(manageJobs));

            }
            if (jobView.Job!.IsExternalProvider)
            {
                return RedirectToAction(nameof(externalJobCard), new { id = jobView.Job!.JobId });
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
                CreateEntity(workDone); // TBD Replace with actual user ID
                _context.Add(workDone);
                await _context.SaveChangesAsync();
                // Stay on the same job card
                return RedirectToAction(nameof(jobCard), new { id = wView.WorkDone.JobId });
            }

            return RedirectToAction(nameof(jobCard), new { id = wView.WorkDone!.JobId });
        }

        [HttpPost]
        public async Task<IActionResult> ExWorkDone(JobViewModel wView)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            TempData["Error"] = string.Join("; ", errors);

            if (ModelState.IsValid)
            {
                //var _job = wView.Job;
                var workDone = wView.WorkDone;
                workDone.WorkDoneId = Guid.NewGuid();
                workDone.JobId = wView.WorkDone.JobId;
                CreateEntity(workDone); // TBD Replace with actual user ID
                _context.Add(workDone);
                await _context.SaveChangesAsync();
                // Stay on the same job card
                return RedirectToAction(nameof(externalJobCard), new { id = wView.WorkDone.JobId });
            }

            return RedirectToAction(nameof(externalJobCard), new { id = wView.WorkDone!.JobId });
        }

        [RequireLogin]
        public async Task<IActionResult> externalJobCard(Guid id)
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
                WorkDoneList = _repo.GetWorkDone().Result.Where(w => w.JobId == job.JobId),
                WorkDone = new WorkDone(),

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
            if (job != null)
            {
                job.EndDate = workRequestView.WorkRequest.CloseDate ?? DateTime.Now;
                job.StatusId = workRequestView.WorkRequest.WorkStatusId;
                job.DateModified = DateTime.Now;
                _context.Update(job!);
            }
            work.OutcomeId = workRequestView.WorkRequest.OutcomeId;
            work.CloseDate = workRequestView.WorkRequest.CloseDate;
            work.WorkStatusId = workRequestView.WorkRequest.WorkStatusId;
            work.DateModified = DateTime.Now;

            _context.Update(work!);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }
    }
}
