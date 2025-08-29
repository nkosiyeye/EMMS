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

namespace EMMS.Controllers
{
    public class HomeController : BaseController
    {
        private readonly ApplicationDbContext _context;
        private readonly AssetManagementRepo _assetRepo;
        private readonly JobManagementRepo _jobRepo;
        private readonly AssetService _assetService;

        public HomeController(AssetManagementRepo assetRepo, JobManagementRepo jobRepo, AssetService assetService, ApplicationDbContext context)
        {
            _assetRepo = assetRepo;
            _jobRepo = jobRepo;
            _assetService = assetService;
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
        public IActionResult Login(User user)
        {
            var searchUser = _context.User
                .Include(u => u.UserRole)
                .Include(u => u.Facility)
                .Where(u => u.Username == user.Username && u.RowState == RowStatus.Active).FirstOrDefault();

            if (searchUser != null && PasswordManager.VerifyPassword(user.Password, searchUser.Password))
                SaveUserSession(searchUser);
            else
                TempData["Notification"] = JsonConvert.SerializeObject(new ToastNotification("Invalid username or password", NotificationType.Error));

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
            var model = new IndexModel
            {
                currentUser = CurrentUser
            };

            var allNotifications = await _assetRepo.GetNotifications();
            model.notifications = isAdmin
                ? allNotifications
                : allNotifications.Where(n => n.FacilityId == CurrentUser.FacilityId).Take(5);

            var assetViewModel = await _assetService.GetAssetIndexViewModel(CurrentUser);

            // Filter due service assets (exclude decommissioned)
            var dueAssets = (await _assetService.GetAssetDueServiceViewModel()).assetViewModels
                .Where(a => a.LastMovement?.Reason != MovementReason.Decommission);

            // If not admin, filter by user's facility
            model.assets = dueAssets;

            model.TotalAssets = assetViewModel.assetViewModels.Count();

            // Completed Jobs
            var completedJobs = (await _jobRepo.GetJobfromDbs())
                .Where(j => j.EndDate != null);

            model.CompletedJobs = isAdmin
                ? completedJobs.Count()
                : completedJobs.Count(j => j.FacilityId == CurrentUser.FacilityId);

            // Pending Work Requests
            var pendingJobs = (await _jobRepo.GetWorkRequests())
                .Where(w => w.Outcome == null);

            model.PendingJobs = isAdmin
                ? pendingJobs.Count()
                : pendingJobs.Count(w => w.FacilityId == CurrentUser.FacilityId);

            return View(model);
        }

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
