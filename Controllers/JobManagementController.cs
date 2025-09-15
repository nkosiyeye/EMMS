using EMMS.CustomAttributes;
using EMMS.Data;
using EMMS.Data.Repository;
using EMMS.Models;
using EMMS.Models.Entities;
using EMMS.Service;
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
        private readonly NotificationService _notificationService;
        public JobManagementController(ApplicationDbContext context, NotificationService notificationService)
        {
            _context = context;
            _assetService = new AssetService(context);
            _notificationService = notificationService;
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
        [RequireLogin]
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
                _context.Add(work);
                await _context.SaveChangesAsync();
                await _notificationService.CreateWorkRequestNotification(CurrentUser.FacilityId,CurrentUser.UserId);

                return RedirectToAction(nameof(Index));
            }

            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            TempData["WorkRequestError"] = string.Join("; ", errors);
            return RedirectToAction(nameof(workRequest),new { id = workRequestView.WorkRequest!.AssetId });
        }
        [HttpGet]
        [RequireLogin]
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
        public async Task<IActionResult> infrustructureWorkRequest(WorkRequestViewModel workRequestView)
        {

            var work = workRequestView.InfrustructureWorkRequest;
            if (ModelState.IsValid)
            {
                work.RequestedBy = CurrentUser.UserId;
                CreateEntity(work);
                _context.Add(work);
                await _context.SaveChangesAsync();
                await _notificationService.CreateWorkRequestNotification(CurrentUser.FacilityId, CurrentUser.UserId);

                return RedirectToAction(nameof(Index));
            }

            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            TempData["WorkRequestError"] = string.Join("; ", errors);
            return RedirectToAction(nameof(infrustructureWorkRequest));
        }
        [HttpGet]
        [RequireLogin]
        public async Task<IActionResult> editWorkRequest(Guid id)
        {
            var _repo = new JobManagementRepo(_context);
            var workRequest = await _repo.GetWorkRequests();
            var workAssetViewModel = new WorkRequestViewModel()
            {
                WorkRequest =workRequest.FirstOrDefault(w => w.WorkRequestId == id),
                FaultReports = await _repo.GetFaultReports(),
                WorkStatuses = await _repo.GetWorkStatus(),
            };

            return View(workAssetViewModel);
        }
        [HttpPost]
        //[RequireLogin]
        public async Task<IActionResult> editWorkRequest(WorkRequestViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["MovementError"] = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                return RedirectToAction(nameof(editWorkRequest), new { id = model.WorkRequest.WorkRequestId });
            }

            UpdateEntity(model.WorkRequest);
            _context.Update(model.WorkRequest);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
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
                FacilityId = (int?)assetLocation ?? CurrentUser.FacilityId,
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

            await _notificationService.CreateWorkRequestNotification(workRequest.FacilityId, CurrentUser.UserId);
            return RedirectToAction(nameof(Index));
        }

        [RequireLogin]
        [AuthorizeRole(nameof(UserType.Administrator), nameof(UserType.Biomed))]
        public async Task<IActionResult> manageJobs()
        {
            ViewData["Biomed"] = new SelectList(_context.User.Where(f => f.RowState == RowStatus.Active && f.UserRole.UserType == Enumerators.UserType.Biomed), "UserId", "FirstName");
            return View(await JobData());
        }
        public async Task<IActionResult> createJobCard(JobViewModel jobView)
        {
            var _repo = new JobManagementRepo(_context);
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            TempData["JobError"] = string.Join("; ", errors);

            if (ModelState.IsValid)
            {
                var job = jobView.Job;
                job.JobId = Guid.NewGuid();

                var facilityCode = CurrentUser.Facility.FacilityCode;
                job.JobNumber = $"{facilityCode}Jb-" + (_repo.GetJobfromDbs().Result.Count() + 1).ToString("D3");

                WorkRequest? wRequest = null;
                InfrustructureWorkRequest? infraRequest = null;

                if (job.AssetId != null)
                {
                    // ----- Asset Work Request -----
                    wRequest = _repo.GetWorkRequests().Result
                                    .FirstOrDefault(w => w.WorkRequestId == job.WorkRequestId);

                    if (wRequest != null)
                    {
                        wRequest.DateModified = DateTime.Now;
                        wRequest.WorkStatusId = job.StatusId;
                        wRequest.JobId = job.JobId;
                        job.FacilityId = wRequest.FacilityId;
                    }
                }
                else if (job.InfrastructureWorkRequestId != null)
                {
                    // ----- Infrastructure Work Request -----
                    infraRequest = _repo.GetInfrustructureWorkRequests().Result
                                        .FirstOrDefault(i => i.WorkRequestId == job.InfrastructureWorkRequestId);

                    if (infraRequest != null)
                    {
                        infraRequest.DateModified = DateTime.Now;
                        infraRequest.WorkStatusId = job.StatusId;
                        infraRequest.JobId = job.JobId;
                        job.FacilityId = infraRequest.FacilityId;
                    }
                }

                CreateEntity(job); // set CreatedBy, DateCreated, etc.
                _context.Add(job);

                if (wRequest != null) _context.Update(wRequest);
                if (infraRequest != null) _context.Update(infraRequest);

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

            return View("manageJobs", await JobData());
        }


        public async Task<IActionResult> jobCard(Guid id)
        {
            var _repo = new JobManagementRepo(_context);
            var job = (await _repo.GetJobfromDbs())
                .FirstOrDefault(j => j.JobId == id);

            if (job == null)
                return NotFound();

            var jobView = new JobViewModel
            {
                Job = job,
                WorkStatuses = await _repo.GetWorkStatus(),
                WorkDoneList = (await _repo.GetWorkDone()).Where(w => w.JobId == job.JobId),
                WorkDone = new WorkDone()
            };

            // ✅ Load work request differently depending on type
            if (job.AssetId != null) // Asset Job
            {
                jobView.WorkRequest = (await _repo.GetWorkRequests())
                    .FirstOrDefault(w => w.WorkRequestId == job.WorkRequestId);
            }
            else if (job.InfrastructureWorkRequestId != null) // Infrastructure Job
            {
                jobView.InfraWorkRequest = (await _repo.GetInfrustructureWorkRequests())
                    .FirstOrDefault(w => w.WorkRequestId == job.InfrastructureWorkRequestId);
            }

            return View(jobView);
        }


        [HttpPost]
        public async Task<IActionResult> updateJobCard(JobViewModel jobView)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();

            TempData["Error"] = string.Join("; ", errors);

            if (ModelState.IsValid)
            {
                var _repo = new JobManagementRepo(_context);
                var job = (await _repo.GetJobfromDbs())
                    .FirstOrDefault(j => j.JobId == jobView.Job!.JobId);

                if (job == null)
                    return NotFound();

                // ✅ Differentiate between Asset vs Infrastructure work requests
                if (job.AssetId != null)
                {
                    var workRequest = (await _repo.GetWorkRequests())
                        .FirstOrDefault(w => w.WorkRequestId == job.WorkRequestId);

                    if (workRequest != null)
                    {
                        workRequest.WorkStatusId = jobView.Job.StatusId;
                        _context.Update(workRequest);
                    }
                }
                else if (job.InfrastructureWorkRequestId != null)
                {
                    var infraRequest = (await _repo.GetInfrustructureWorkRequests())
                        .FirstOrDefault(w => w.WorkRequestId == job.InfrastructureWorkRequestId);

                    if (infraRequest != null)
                    {
                        infraRequest.WorkStatusId = jobView.Job.StatusId;
                        _context.Update(infraRequest);
                    }
                }

                // ✅ Update job itself
                job.StatusId = jobView.Job.StatusId;
                job.DateModified = DateTime.Now;
                job.Remarks = jobView.Job.Remarks;

                if (job.IsExternalProvider)
                {
                    job.Amount = jobView.Job.Amount;
                    job.InvoiceNo = jobView.Job.InvoiceNo;
                }

                _context.Update(job);
                await _context.SaveChangesAsync();

                // 🔔 Notifications (TBD when service ready)
                // if (jobView.Job.StatusId != null && job.Status.Name == "Completed")
                //     await _notificationService.CreateJobCompletedNotification(workRequest.RequestedBy, CurrentUser.UserId);

                return RedirectToAction(nameof(manageJobs));
            }

            // Redirect back to correct job card type
            if (jobView.Job!.IsExternalProvider)
            {
                return RedirectToAction(nameof(externalJobCard), new { id = jobView.Job.JobId });
            }

            return RedirectToAction(nameof(jobCard), new { id = jobView.Job.JobId });
        }

        [HttpPost]
        public async Task<IActionResult> workDone(JobViewModel wView)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            TempData["WorkDoneError"] = string.Join("; ", errors);

            if (ModelState.IsValid)
            {
                var workDone = wView.WorkDone;

                if (workDone.WorkDoneId == Guid.Empty || workDone.WorkDoneId == default)
                {
                    // ✅ New work
                    workDone.WorkDoneId = Guid.NewGuid();
                    workDone.RowState = RowStatus.Active;
                    CreateEntity(workDone);
                    _context.Add(workDone);
                }
                else
                {
                    // ✅ Edit existing
                    var existingWork = await _context.WorkDone.FindAsync(workDone.WorkDoneId);
                    if (existingWork != null)
                    {
                        existingWork.DateCompleted = workDone.DateCompleted;
                        existingWork.Hours = workDone.Hours;
                        existingWork.Details = workDone.Details;
                        UpdateEntity(existingWork); // your audit/update tracking
                        _context.Update(existingWork);
                    }
                }

                await _context.SaveChangesAsync();

                // Stay on same job card
                return RedirectToAction(nameof(jobCard), new { id = workDone.JobId });
            }

            return RedirectToAction(nameof(jobCard), new { id = wView.WorkDone!.JobId });
        }

        [HttpPost]
        public async Task<IActionResult> ExWorkDone(JobViewModel wView)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            TempData["WorkDoneError"] = string.Join("; ", errors);

            if (ModelState.IsValid)
            {
                var workDone = wView.WorkDone;

                if (workDone.WorkDoneId == Guid.Empty || workDone.WorkDoneId == default)
                {
                    // ✅ New work
                    workDone.WorkDoneId = Guid.NewGuid();
                    workDone.RowState = RowStatus.Active;
                    CreateEntity(workDone);
                    _context.Add(workDone);
                }
                else
                {
                    // ✅ Edit existing
                    var existingWork = await _context.WorkDone.FindAsync(workDone.WorkDoneId);
                    if (existingWork != null)
                    {
                        existingWork.DateCompleted = workDone.DateCompleted;
                        existingWork.Hours = workDone.Hours;
                        existingWork.Details = workDone.Details;
                        UpdateEntity(existingWork); // your audit/update tracking
                        _context.Update(existingWork);
                    }
                }

                await _context.SaveChangesAsync();

                // Stay on same job card
                return RedirectToAction(nameof(externalJobCard), new { id = wView.WorkDone.JobId });
            }
            return RedirectToAction(nameof(externalJobCard), new { id = wView.WorkDone.JobId });
        }

        [RequireLogin]
        public async Task<IActionResult> externalJobCard(Guid id)
        {
            var _repo = new JobManagementRepo(_context);
            var job = (await _repo.GetJobfromDbs())
                .FirstOrDefault(j => j.JobId == id);

            if (job == null)
                return NotFound();

            var jobView = new JobViewModel
            {
                Job = job,
                WorkStatuses = await _repo.GetWorkStatus(),
                WorkDoneList = (await _repo.GetWorkDone()).Where(w => w.JobId == job.JobId),
                WorkDone = new WorkDone()
            };

            // Load work request differently depending on type
            if (job.AssetId != null) // Asset Job
            {
                jobView.WorkRequest = (await _repo.GetWorkRequests())
                    .FirstOrDefault(w => w.WorkRequestId == job.WorkRequestId);
            }
            else if (job.InfrastructureWorkRequestId != null) // Infrastructure Job
            {
                jobView.InfraWorkRequest = (await _repo.GetInfrustructureWorkRequests())
                    .FirstOrDefault(w => w.WorkRequestId == job.InfrastructureWorkRequestId);
            }
            return View(jobView);
        }
        [HttpPost]
        public async Task<IActionResult> closeRequest(WorkRequestViewModel workRequestView)
        {
            var id = workRequestView.WorkRequest.WorkRequestId;
            var _repo = new JobManagementRepo(_context);

            // Try normal work request first
            var work = (await _repo.GetWorkRequests())
                .FirstOrDefault(w => w.WorkRequestId == id);

            // If not found, try infrastructure work request
            var infraWork = work == null
                ? (await _repo.GetInfrustructureWorkRequests())
                    .FirstOrDefault(i => i.WorkRequestId == id)
                : null;

            // Get job linked to either request type
            var job = work != null ? (await _repo.GetJobfromDbs())
                .FirstOrDefault(j => j.WorkRequestId == id)
                : (await _repo.GetJobfromDbs())
                .FirstOrDefault(j => j.InfrastructureWorkRequestId == id);

            if (job != null)
            {
                job.EndDate = workRequestView.WorkRequest.CloseDate ?? DateTime.Now;
                job.StatusId = workRequestView.WorkRequest.WorkStatusId;
                job.DateModified = DateTime.Now;
                _context.Update(job);
            }

            // Update whichever request type was found
            if (work != null)
            {
                work.OutcomeId = workRequestView.WorkRequest.OutcomeId;
                work.CloseDate = workRequestView.WorkRequest.CloseDate;
                work.WorkStatusId = workRequestView.WorkRequest.WorkStatusId;
                work.DateModified = DateTime.Now;

                _context.Update(work);
            }
            else if (infraWork != null)
            {
                infraWork.OutcomeId = workRequestView.WorkRequest.OutcomeId;
                infraWork.CloseDate = workRequestView.WorkRequest.CloseDate;
                infraWork.WorkStatusId = workRequestView.WorkRequest.WorkStatusId;
                infraWork.DateModified = DateTime.Now;

                _context.Update(infraWork);
            }

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> cancelRequest(WorkRequestViewModel workRequestView)
        {
            
           try {
             var id = workRequestView.WorkRequest.WorkRequestId;
            var _repo = new JobManagementRepo(_context);
                // normal work request first
                var work = (await _repo.GetWorkRequests())
                    .FirstOrDefault(w => w.WorkRequestId == id);

                // If not found, try infrastructure work request
                var infraWork = work == null
                    ? (await _repo.GetInfrustructureWorkRequests())
                        .FirstOrDefault(i => i.WorkRequestId == id)
                    : null;

                // Get job linked to either request type
                var job = work != null ? (await _repo.GetJobfromDbs())
                    .FirstOrDefault(j => j.WorkRequestId == id)
                    : (await _repo.GetJobfromDbs())
                    .FirstOrDefault(j => j.InfrastructureWorkRequestId == id);
            if(work != null)
                {
                    work.OutcomeId = workRequestView.WorkRequest.OutcomeId;
                    work.CloseDate = workRequestView.WorkRequest.CloseDate;
                    work.WorkStatusId = workRequestView.WorkRequest.WorkStatusId;
                    work.CancelReasonId = workRequestView.WorkRequest.CancelReasonId;
                    UpdateEntity(work); // Assuming UpdateEntity handles the update logic

                    _context.Update(work!);

                }else if(infraWork != null)
                {

                    infraWork.OutcomeId = workRequestView.WorkRequest.OutcomeId;
                    infraWork.CloseDate = workRequestView.WorkRequest.CloseDate;
                    infraWork.WorkStatusId = workRequestView.WorkRequest.WorkStatusId;
                    infraWork.CancelReasonId = workRequestView.WorkRequest.CancelReasonId;
                    UpdateEntity(infraWork); // Assuming UpdateEntity handles the update logic

                    _context.Update(infraWork!);

                }
                    await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                return RedirectToAction("Error", "Home");
            }
        }
    }
}
