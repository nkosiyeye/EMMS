using Microsoft.AspNetCore.Mvc;

namespace EMMS.Controllers
{
    public class AssetMovementController : Controller
    {
        public IActionResult moveAsset()
        {
            return View();
        }

        public IActionResult recieveAsset()
        {
            return View();
        }
    }
}
