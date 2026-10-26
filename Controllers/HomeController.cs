using System.Diagnostics;
using EMMS.Auth;
using EMMS.Data;
using EMMS.Data.Repository;
using EMMS.Models;
using EMMS.Models.Admin;
using EMMS.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EMMS.Utility;
using Newtonsoft.Json;
using static EMMS.Models.Enumerators;
using Microsoft.EntityFrameworkCore;
using EMMS.CustomAttributes;
using Microsoft.AspNetCore.Mvc.Rendering;
using EMMS.Service;
using System.Threading.Tasks;

namespace EMMS.Controllers
{
    public class HomeController : BaseController
    {
        private readonly ApplicationDbContext _context;
        private readonly AssetManagementRepo _assetRepo;
        private readonly NotificationService _notificationService;
        private readonly DashboardService _dashboardService;

        public HomeController(AssetManagementRepo assetRepo, NotificationService notificationService, DashboardService dashboardService, ApplicationDbContext context)
        {
            _assetRepo = assetRepo;
            _notificationService = notificationService;
            _dashboardService = dashboardService;
            _context = context;
        }
        [AllowAnonymous]
        [NoFacilityData("Sign-up form")]
        public IActionResult UserRegistration()
        {
            ViewData["DesignationId"] = new SelectList(_context.LookupItems.Include(x => x.LookupList).Where(l => l.LookupList.Name.Contains("Desig") && l.RowState == RowStatus.Active), "Id", "Name");
            ViewData["FacilityId"] = new SelectList(_context.Facilities.Where(f => f.RowState == RowStatus.Active), "FacilityId", "FacilityName");
            ViewData["UserRoleId"] = new SelectList(_context.UserRole.Where(f => f.RowState == RowStatus.Active), "Id", "Name");
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        [NoFacilityData("Sign-up; the account waits for an admin to activate it")]
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
                TempData["UserRegistrationSuccess"] = "Registration successful. You can now log in with your credentials.";
                return RedirectToAction(nameof(Login));
            }
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            TempData["UserRegistrationError"] = string.Join("; ", errors);
            ViewData["DesignationId"] = new SelectList(_context.LookupItems.Where(f => f.LookupList.Name.Contains("Desig") && f.RowState == RowStatus.Active), "Id", "Name", user.DesignationId);
            ViewData["FacilityId"] = new SelectList(_context.Facilities.Where(f => f.RowState == RowStatus.Active), "FacilityId", "FacilityName", user.FacilityId);
            ViewData["UserRoleId"] = new SelectList(_context.UserRole.Where(f => f.RowState == RowStatus.Active), "Id", "Name", user.UserRoleId);
            return View(user);
        }


        [AllowAnonymous]
        [NoFacilityData("Sign-in")]
        public IActionResult Login()
        {
            ClearSession();
            return View(new User());
        }

        [HttpPost]
        [AllowAnonymous]
        [NoFacilityData("Sign-in")]
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
        [AllowAnonymous]
        [NoFacilityData("Password reset request")]
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
        [AllowAnonymous]
        [NoFacilityData("Sign-out")]
        public IActionResult Logout()
        {
            ClearSession();
            return RedirectToAction(nameof(Index));
        }
        [RequireLogin]
        [FacilityChecked("DashboardService.ScopeFacility pins non-admins to their facility")]
        public async Task<IActionResult> Index(int? facilityId = null)
        {
            // The facility filter only applies to admins; DashboardService pins everyone else to their own facility.
            var model = await _dashboardService.BuildAsync(CurrentUser!, facilityId);

            if (model.Role == UserType.Administrator)
                ViewData["FacilityId"] = new SelectList(await _assetRepo.GetActiveFacilitiesAsync(), "FacilityId", "FacilityName", model.FacilityId);
            return View(model);
        }
        [NoFacilityData("Static page")]
        public IActionResult Privacy()
        {
            return View();
        }

        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        [NoFacilityData("Error page")]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
