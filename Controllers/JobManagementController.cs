using Microsoft.AspNetCore.Mvc;

namespace EMMS.Controllers
{
    public class JobManagementController : Controller
    {
        public IActionResult workRequest()
        {
            return View();
        }

        public IActionResult manageJobs()
        {
            return View();
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
