using System.Diagnostics;
using EMMS.Auth;
using EMMS.Data;
using EMMS.Data.Repository;
using EMMS.Models;
using EMMS.Models.Admin;
using EMMS.ViewModels;
using Microsoft.AspNetCore.Mvc;
using EMMS.Utility;
using Newtonsoft.Json;
using static EMMS.Models.Enumerators;
using Microsoft.EntityFrameworkCore;
using EMMS.CustomAttributes;
using Microsoft.AspNetCore.Mvc.Rendering;
using EMMS.Service;
using Microsoft.EntityFrameworkCore.Infrastructure.Internal;
using DocumentFormat.OpenXml.InkML;
using Microsoft.Data.SqlClient;
using System.Threading.Tasks;

namespace EMMS.Controllers
{
    public class HomeController : BaseController
    {
        private readonly ApplicationDbContext _context;
        private readonly AssetManagementRepo _assetRepo;
        private readonly JobManagementRepo _jobRepo;
        private readonly AssetService _assetService;
        private readonly NotificationService _notificationService;

        public HomeController(AssetManagementRepo assetRepo, JobManagementRepo jobRepo, AssetService assetService, NotificationService notificationService, ApplicationDbContext context)
        {
            _assetRepo = assetRepo;
            _jobRepo = jobRepo;
            _assetService = assetService;
            _notificationService = notificationService;
            _context = context;
        }
        public IActionResult UserRegistration()
        {
            ViewData["DesignationId"] = new SelectList(_context.LookupItems.Include(x => x.LookupList).Where(l => l.LookupList.Name.Contains("Desig") && l.RowState == RowStatus.Active), "Id", "Name");
            ViewData["FacilityId"] = new SelectList(_context.Facilities.Where(f => f.RowState == RowStatus.Active), "FacilityId", "FacilityName");
            ViewData["UserRoleId"] = new SelectList(_context.UserRole.Where(f => f.RowState == RowStatus.Active), "Id", "Name");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UserRegistration(User user)
        {
            var usernameExists = await _context.User.FirstOrDefaultAsync(u => u.Username == user.Username);
            if (string.IsNullOrWhiteSpace(user.Password))
            {
                ModelState.AddModelError("user.Password", "User Password can not be empty.");
            }
            else if (usernameExists != null)
            {
                ModelState.AddModelError("user.Username", "Username already Exists in the system");

            }
            if (ModelState.IsValid)
            {
                user.UserId = Guid.NewGuid();
                user.DateCreated = DateTime.Now;
                user.Password = PasswordManager.Encrypt(user.Password!);
                user.RowState = RowStatus.Inactive;
                //user.CreatedBy = TBD 
                _context.Add(user);
                await _context.SaveChangesAsync();
                await _notificationService.UserRegistrationNotification(user);
                TempData["UserRegistrationSuccess"] = "User Registrated Successfully";
                return RedirectToAction(nameof(Login));
            }
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            TempData["UserRegistrationError"] = string.Join("; ", errors);
            ViewData["DesignationId"] = new SelectList(_context.LookupItems.Where(f => f.LookupList.Name.Contains("Desig") && f.RowState == RowStatus.Active), "Id", "Name", user.DesignationId);
            ViewData["FacilityId"] = new SelectList(_context.Facilities.Where(f => f.RowState == RowStatus.Active), "FacilityId", "FacilityName", user.FacilityId);
            ViewData["UserRoleId"] = new SelectList(_context.UserRole.Where(f => f.RowState == RowStatus.Active), "Id", "Name", user.UserRoleId);
            return View(user);
        }


        public IActionResult Login()
        {
            ClearSession();
            return View(new User());
        }

        [HttpPost]
        public async Task<IActionResult> Login(User user)
        {
            var searchUser = await _context.User
                .Include(u => u.UserRole)
                .Include(u => u.Facility)
                .Where(u => u.Username == user.Username && u.RowState == RowStatus.Active).FirstOrDefaultAsync();

            if (searchUser != null && PasswordManager.VerifyPassword(user.Password, searchUser.Password))
                SaveUserSession(searchUser);
            else
                TempData["Notification"] = JsonConvert.SerializeObject(new ToastNotification("Invalid username or password", NotificationType.Error));

            return RedirectToAction(nameof(Index));
        }
        [HttpPost]
        public async Task<IActionResult> ForgotPasswordRequest(string usernameOrNumber)
        {
            // Find user by username or number
            var searchUser = await _context.User.Where(u => u.Username == usernameOrNumber || u.Cellphone == usernameOrNumber).FirstOrDefaultAsync();
            if (searchUser != null)
            {
                await _notificationService.ForgetPasswordNotification(searchUser);
                TempData["Notification"] = JsonConvert.SerializeObject(new ToastNotification("Request Submitted", NotificationType.Success));

            }
            else
                TempData["Notification"] = JsonConvert.SerializeObject(new ToastNotification("User not Found Try again", NotificationType.Error));


            return RedirectToAction(nameof(Index));
        }
        public IActionResult Logout()
        {
            ClearSession();
            return RedirectToAction(nameof(Index));
        }
        [RequireLogin]
        public async Task<IActionResult> Index()
        {

            return View();
        }
        [HttpGet]
        public async Task<IActionResult> GetDashboardCounts()
        {
            var isAdmin = CurrentUser.UserRole?.UserType == Enumerators.UserType.Administrator;
            var isDataCollector = CurrentUser.UserRole?.UserType == Enumerators.UserType.DataCollector;
            int? facilityId = isAdmin ? null : CurrentUser.FacilityId;

            // Assets
            var allAssets = await _context.Assets
                .FromSqlRaw("EXEC sp_GetAssetsByFacility @FacilityId={0}", facilityId ?? (object)DBNull.Value)
                .AsNoTracking()
                .ToListAsync();


            // Work requests
            var allWork = await _context.WorkRequest
                .FromSqlRaw("EXEC sp_GetOpenWorkRequestsByFacility @FacilityId={0}", facilityId ?? (object)DBNull.Value)
                .AsNoTracking()
                .ToListAsync();

            var allInfraWork = await _context.InfrustructureWorkRequest
                .FromSqlRaw("EXEC sp_GetOpenInfraWorkRequestsByFacility @FacilityId={0}", facilityId ?? (object)DBNull.Value)
                .AsNoTracking()
                .ToListAsync();

            // Jobs
            var completedJobs = await GetJobCountAsync(CurrentUser.FacilityId, true, isAdmin);
            var pendingJobs = await GetJobCountAsync(CurrentUser.FacilityId, false, isAdmin);

            // Notifications (Top 5)
            var notifications = await _context.Notifications
                .FromSqlRaw("EXEC sp_GetNotificationsByFacility @FacilityId={0}, @Take={1}", facilityId ?? (object)DBNull.Value, 5)
                .AsNoTracking()
                .ToListAsync();

            return Json(new
            {
                totalAssets = isDataCollector ? allAssets.Where((a) => a.CreatedBy == CurrentUser.UserId).Count() : allAssets.Count,
                openRequests = (allWork?.Count ?? 0) + (allInfraWork?.Count ?? 0),
                completedJobs,
                pendingJobs,
                notifications
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetAssetsDueServiceJson()
        {
            var isAdmin = CurrentUser.UserRole?.UserType == Enumerators.UserType.Administrator;
            int? facilityId = isAdmin ? null : CurrentUser.FacilityId;

            var assetViewModel = await _assetService.GetAssetIndexViewModel(CurrentUser);
            var dueforService = (await _assetService.GetAssetDueServiceViewModel()).assetViewModels
                .Where(a => a.LastMovement?.Reason != MovementReason.Decommission);

            // Filter due service assets (exclude decommissioned)
            var dueAssets = isAdmin ? dueforService : dueforService.Where(a => a.LastMovement?.FacilityId == CurrentUser.FacilityId);

            return Json(dueAssets.Select(a => new
            {
                a.Asset.AssetId,
                a.Asset.AssetTagNumber,
                a.Asset.SubCategory.Name,
                a.Asset.StatusId,
                NextServiceDate = a.Asset.NextServiceDate?.ToString("yyyy-MM-dd"),
                FacilityName = a.LastMovement?.Facility?.FacilityName,
                ServicePointName = a.LastMovement?.ServicePoint?.Name,
                FunctionalStatus = a.LastMovement?.FunctionalStatus,
                OverDueService = a.Asset.NextServiceDate != null ? (DateTime.Now - a.Asset.NextServiceDate.Value).Days : 0

            }));
        }

        [HttpGet]
        public async Task<IActionResult> GetDashboardChartData()
        { 
            var vm = new DashboardViewModel(_assetService, CurrentUser);
            await vm.OnGetAsync();
            return Json(new
            {
                movementReasonCounts = vm.MovementReasonCounts,
                functionalStatusCounts = vm.FunctionalStatusCounts,
                procurementStatusCounts = vm.ProcurementStatusCounts,
                nonFunctionalByFacilityCounts = vm.DecommissionedByFacilityCounts
            });
        }

        private async Task<int> GetJobCountAsync(int facilityId, bool completed, bool isAdmin)
        {
            using (var command = _context.Database.GetDbConnection().CreateCommand())
            {
                command.CommandText = "sp_GetJobsCount";
                command.CommandType = System.Data.CommandType.StoredProcedure;

                command.Parameters.Add(new SqlParameter("@FacilityId", facilityId));
                command.Parameters.Add(new SqlParameter("@Completed", completed));
                command.Parameters.Add(new SqlParameter("@IsAdmin", isAdmin));

                await _context.Database.OpenConnectionAsync();
                var result = await command.ExecuteScalarAsync();
                return Convert.ToInt32(result);
            }
        }



        /* public async Task<IActionResult> Index()
         {
             var isAdmin = CurrentUser.UserRole?.UserType == Enumerators.UserType.Administrator;

             // Fetch work requests sequentially
             var allWork = await _jobRepo.GetOpenWorkRequestsByFacility(isAdmin ? null : CurrentUser.FacilityId);
             var allInfraWork = await _jobRepo.GetOpenInfraWorkRequestsByFacility(isAdmin ? null : CurrentUser.FacilityId);
             var notifications = await _notificationService.GetNotificationsByFacility(isAdmin ? null : CurrentUser.FacilityId, 5);

             var assetViewModel = await _assetService.GetAssetIndexViewModel(CurrentUser);
             var assetDueViewModel = await _assetService.GetAssetDueServiceViewModel();

             var model = new IndexModel
             {
                 currentUser = CurrentUser,
                 OpenWorkRequestsCount = allWork.Count + allInfraWork.Count,
                 notifications = notifications
             };

             // Assets
             var dueForService = assetDueViewModel.assetViewModels
                 .Where(a => a.LastMovement?.Reason != MovementReason.Decommission);

             var dueAssets = isAdmin ? dueForService : dueForService.Where(a => a.LastMovement?.FacilityId == CurrentUser.FacilityId);
             model.assets = dueAssets;
             model.TotalAssets = assetViewModel.assetViewModels.Count();

             // Completed and Pending Jobs
             model.CompletedJobs = await _jobRepo.GetJobsCount(CurrentUser.FacilityId, completed: true, isAdmin: isAdmin);
             model.PendingJobs = await _jobRepo.GetJobsCount(CurrentUser.FacilityId, completed: false, isAdmin: isAdmin);

             return View(model);
         }*/



        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
