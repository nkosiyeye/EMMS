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

namespace EMMS.Controllers
{
    public class HomeController : BaseController
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger, ApplicationDbContext context)
        {
            _logger = logger;                
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
        public async Task<IActionResult> UserRegistration([Bind("UserId,FirstName,MiddleName,LastName,DOB,Gender,Cellphone,DesignationId,FacilityId,Username,Password,UserRoleId,CreatedBy,DateCreated,ModifiedBy,DateModified,RowState")] User user)
        {
            if (ModelState.IsValid)
            {
                user.UserId = Guid.NewGuid();
                user.DateCreated = DateTime.Now;
                user.Password = PasswordManager.Encrypt(user.Password!);
                //user.CreatedBy = TBD 
                _context.Add(user);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
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
                TempData["Notification"] = JsonConvert.SerializeObject(new EMMS.Utility.Notification("Invalid username or password", NotificationType.Error));

            return RedirectToAction(nameof(Index));
        }
        public IActionResult Logout()
        {
            ClearSession();
            return RedirectToAction(nameof(Index));
        }
        [RequireLogin]
        public IActionResult Index()
        {
            var indexModel = new IndexModel(new AssetManagementRepo(_context), new JobManagementRepo(_context), new Service.AssetService(_context));
            indexModel.currentUser = CurrentUser!;
            indexModel.OnGet();
            return View(indexModel);
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
