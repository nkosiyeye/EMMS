using EMMS.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace EMMS.Controllers
{
    public class AssetMovementController : Controller
    {
        [HttpGet]
        public IActionResult moveAsset(int page = 1, int pageSize = 10)
        {

            var all = GetAssetMovement();

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
            MoveAssetViewModel paginatedMovement = new MoveAssetViewModel
            {
                MoveAssets = paginated,
                MoveAsset = new MoveAsset()
            };
            return View(paginatedMovement);
        }
        private List<MoveAsset> GetAssetMovement()
        {
            return new List<MoveAsset>
    {
        new MoveAsset
        {
            MovementDate = DateTime.Now.AddDays(-10),
            AssetId = "AS-001",
            MovementType = "To Another Facility",
            From = "Lobamba Clinic",
            Facility = "Mbabane Hospital",
            ServicePoint = null,
            Reason = "Initial deployment",
            FunctionalStatus = "Working",
            IsApproved = false,
            DateReceived = null,
            Condition = null,
            ReceivedBy = null,
            Remarks = null
        },
        new MoveAsset
        {
            MovementDate = DateTime.Now.AddDays(-20),
            AssetId = "AS-002",
            MovementType = "To a Service Point",
            From = "Mbabane Hospital",
            Facility = null,
            ServicePoint = "Radiology",
            Reason = "Current equipment down, needs repair",
            FunctionalStatus = "Faulty",
            IsApproved = false,
            DateReceived = null,
            Condition = null,
            ReceivedBy = null,
            Remarks = null
        },
        new MoveAsset
        {
            MovementDate = DateTime.Now.AddDays(-30),
            AssetId = "AS-003",
            MovementType = "To Another Facility",
            From = "Manzini Hospital",
            Facility = "Siteki Clinic",
            ServicePoint = null,
            Reason = "No longer use it",
            FunctionalStatus = "Decommissioned",
            IsApproved = true,
            DateReceived = null,
            Condition = null,
            ReceivedBy = null,
            Remarks = null
        },
        new MoveAsset
        {
            MovementDate = DateTime.Now.AddDays(-40),
            AssetId = "AS-004",
            MovementType = "To a Service Point",
            From = "Siteki Clinic",
            Facility = null,
            ServicePoint = "ICU",
            Reason = "Initial deployment",
            FunctionalStatus = "Working",
            IsApproved = true,
            DateReceived = DateTime.Now.AddDays(-39),
            Condition = "Good",
            ReceivedBy = "User D",
            Remarks = "Asset deployed to ICU for patient monitoring."
        },
        new MoveAsset
        {
            MovementDate = DateTime.Now.AddDays(-50),
            AssetId = "AS-005",
            MovementType = "To Another Facility",
            From = "ICU",
            Facility = "Manzini Hospital",
            ServicePoint = null,
            Reason = "Decommissioned",
            FunctionalStatus = "Decommissioned",
            IsApproved = true,
            DateReceived = DateTime.Now.AddDays(-49),
            Condition = "Decommissioned",
            ReceivedBy = "User E",
            Remarks = "Asset moved to Manzini Hospital for decommissioning."
        }
    };
        }

        [HttpPost]
        public IActionResult MoveAsset(MoveAssetViewModel model)
        {
            
                Debug.WriteLine(model.MoveAsset.MovementType);
                GetAssetMovement().Add(model.MoveAsset);
                return RedirectToAction("moveAsset");           


            return View(model);
        }

        public IActionResult recieveAsset(int page = 1, int pageSize = 10)
        {

            var all = GetAssetMovement();

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
            MoveAssetViewModel paginatedMovement = new MoveAssetViewModel
            {
                MoveAssets = paginated,
                MoveAsset = new MoveAsset()
            };
            return View(paginatedMovement);
        }
        [HttpPost]
        public IActionResult ReceiveAsset(MoveAssetViewModel model)
        {
            
            if (ModelState.IsValid)
            {
                return RedirectToAction("recieveAsset");
            }

            return View(model);
        }
    }
}
