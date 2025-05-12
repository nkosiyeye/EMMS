using EMMS.Models;
using Microsoft.AspNetCore.Mvc;

namespace EMMS.Controllers
{
    public class JobManagementController : Controller
    {
        private List<WorkRequest> GetWorkRequests()
        {
            return new List<WorkRequest>
    {
        new WorkRequest
        {
            WorkRequestId = 1,
            AssetId = "AS-001",
            RequestDate = DateTime.Now.AddDays(-5),
            Title = "Repair Power Supply",
            Description = "The power supply unit of the asset is malfunctioning and needs immediate repair.",
            Status = "In Progress",
            Outcome = null,
            RequestedBy = "John Doe"
        },
        new WorkRequest
        {
            WorkRequestId = 2,
            AssetId = "AS-002",
            RequestDate = DateTime.Now.AddDays(-10),
            Title = "Routine Maintenance",
            Description = "Scheduled maintenance for the asset to ensure optimal performance.",
            Status = "Pending",
            Outcome = null,
            RequestedBy = "Jane Smith"
        },
        new WorkRequest
        {
            WorkRequestId = 3,
            AssetId = "AS-003",
            RequestDate = DateTime.Now.AddDays(-15),
            Title = "Replace Battery",
            Description = "The battery of the asset is no longer holding a charge and needs replacement.",
            Status = "Closed",
            Outcome = "Battery replaced successfully",
            RequestedBy = "Michael Johnson"
        },
        new WorkRequest
        {
            WorkRequestId = 4,
            AssetId = "AS-004",
            RequestDate = DateTime.Now.AddDays(-10),
            Title = "Routine Maintenance",
            Description = "Scheduled maintenance for the asset to ensure optimal performance.",
            Status = "Pending",
            Outcome = null,
            RequestedBy = "Jane Smith"
        },
    };
        }
        public IActionResult workRequest(int page = 1, int pageSize = 10)
        {

            var all = GetWorkRequests();

            // Calculate total items and apply pagination
            int totalItems = all.Count();
            var paginated = all
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            // Create the pagination model
            var pagination = new Pagination(totalItems, page, pageSize);

            // Pass data to the view
            ViewData["Pagination"] = pagination;
            ViewData["PageSize"] = pageSize;
            WorkRequestViewModel paginatedWorkRequest = new WorkRequestViewModel
            {
                WorkRequests = paginated,
                WorkRequest = new WorkRequest()
            };
            return View(paginatedWorkRequest);
        }
        private List<Job> GetJobs()
        {
            return new List<Job>
    {
        new Job
        {
            JobId = 1,
            WorkRequestId = 1,
            AssignedTo = "John Doe",
            StartDate = DateTime.Now.AddDays(-5),
            EndDate = null, // Job is still in progress
            Status = "In-Progress",
            Remarks = "Diagnosing the issue with the power supply."
        },
        new Job
        {
            JobId = 2,
            WorkRequestId = 3,
            AssignedTo = "John Doe",
            StartDate = DateTime.Now.AddDays(-5),
            EndDate = DateTime.Now, // Job is still in progress
            Status = "Closed",
            Remarks = "Diagnosing the issue with the power supply."
        },
    };
        }

        public IActionResult manageJobs(int page = 1, int pageSize = 10)
        {

            var allWorkRequest = GetWorkRequests().Where(w => !GetJobs().Any(j => j.WorkRequestId == w.WorkRequestId));

            // Calculate total items and apply pagination
            int totalItems = allWorkRequest.Count();
            var paginated = GetJobs()
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            // Create the pagination model
            var pagination = new Pagination(totalItems, page, pageSize);

            // Pass data to the view
            ViewData["Pagination"] = pagination;
            ViewData["PageSize"] = pageSize;
            JobViewModel paginatedJobHistory = new JobViewModel
            {
                Jobs = paginated,
                WorkRequests = allWorkRequest,
                Job = new Job()
            };
            return View(paginatedJobHistory);
        }
        public IActionResult jobCard()
        {
            return View();
        }
        public IActionResult externalJobCard()
        {
            return View();
        }
    }
}
