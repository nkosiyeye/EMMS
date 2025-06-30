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
                //Debug.WriteLine("-----------------" + (user?.UserRole?.UserType == Enumerators.UserType.Administrator).ToString());
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
