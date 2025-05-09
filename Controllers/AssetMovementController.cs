using EMMS.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace EMMS.Controllers
{
    public class AssetMovementController : Controller
    {
        [HttpGet]
        public IActionResult moveAsset()
        {
            return View();
        }

        [HttpPost]
        public IActionResult MoveAsset(MoveAsset model)
        {


            
                Debug.WriteLine(model.MovementType);
                //return RedirectToAction("Index");
            


            return View(model);
        }

        public IActionResult recieveAsset()
        {
            return View();
        }
        [HttpPost]
        public IActionResult ReceiveAsset(RecieveAsset model)
        {
            
            if (ModelState.IsValid)
            {
                return RedirectToAction("Index");
            }

            return View(model);
        }
    }
}
