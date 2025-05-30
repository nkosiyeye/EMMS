using System.Diagnostics;
using EMMS.Data;
using EMMS.Data.Repository;
using EMMS.Models;
using EMMS.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EMMS.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger, ApplicationDbContext context)
        {
            _logger = logger;                
            _context = context;
        }

        public IActionResult Index()
        {
            
            var indexModel = new IndexModel(new AssetManagementRepo(_context), new JobManagementRepo(_context));
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
