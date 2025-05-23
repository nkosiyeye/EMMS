using EMMS.Data;
using EMMS.Data.Migrations;
using EMMS.Data.Repository;
using EMMS.Models;
using EMMS.Models.Entities;
using EMMS.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using static EMMS.Models.Enumerators;

namespace EMMS.Controllers
{
    public class AssetMovementController : Controller
    {
        private readonly ApplicationDbContext _context;
        public AssetMovementController(ApplicationDbContext context)
        {
            _context = context;
        }
        public async Task<MoveAssetViewModel> Data(MoveAsset? movemodel = null)
        {
            if (movemodel is null)
            {
                movemodel = new MoveAsset();
            }
            var _repo = new AssetMovementRepo(_context);
            var assets = await new AssetManagement(_context).assetViewModel();

            var all = await _repo.GetAssetMovement();
            MoveAssetViewModel paginatedMovement = new MoveAssetViewModel
            {
                MoveAssets = all,
                MoveAsset = movemodel,
                AssetIndex = assets,
                Conditions = await _repo.GetConditions()
            };

            return paginatedMovement;

        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var model = await Data();
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> moveAsset(Guid id)
        {
            var _arepo = new AssetManagementRepo(_context).GetAssetsFromDb().Result.FirstOrDefault(a => a.AssetId == id);
            var _repo = new AssetMovementRepo(_context);
            var moveAsset = new MoveAsset()
            {
                AssetId = id,

            };
            var moveAssetViewModel = new MoveRequestViewModel()
            {
                AssetTag = _arepo.AssetTagNumber,
                MoveAsset = moveAsset,

                MovementTypes = await _repo.GetMovementTypes(),
                Facilities = await _repo.GetFacilities(),
                ServicePoints = await _repo.GetServicePoints(),
                Reasons = await _repo.GetReasons(),
                FunctionalStatuses = await _repo.GetFunctionalStatuses(),

            };


            return View(moveAssetViewModel);
        }


        [HttpPost]
        public async Task<IActionResult> MoveAsset(MoveRequestViewModel asmove)
        {

            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            TempData["Error"] = string.Join("; ", errors);
            if (ModelState.IsValid)
            {
                var assetMovement = asmove.MoveAsset;
                assetMovement.FromId = 2;//Update with logged in facility;

                //Debug.WriteLine("assetId"+assetMovement.AssetId);
                //assetMovement.IsApproved = false;
                assetMovement.DateCreated = DateTime.Now;
                assetMovement.RowState = RowStatus.Active;
                assetMovement.CreatedBy = Guid.NewGuid(); // TBD Replace with actual user ID
                //asset.CreatedBy = User.FindFirstValue(ClaimTypes.NameIdentifier);

                _context.Add(assetMovement);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return RedirectToAction(nameof(moveAsset));
        }
        [HttpGet]
        public async Task<IActionResult> recieveAsset(int page = 1, int pageSize = 10)
        {

            
            return View(await Data());
        }
        
        [HttpPost]
        public async Task<IActionResult> ReceiveAsset(MoveAssetViewModel model)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            TempData["Error"] = string.Join("; ", errors);

            if (ModelState.IsValid)
            { 
                var movement = _context.AssetMovement
                    .FirstOrDefault(m => m.MovementId == model.MoveAsset.MovementId); 

                if (movement == null)
                {
                    ModelState.AddModelError("", "Asset movement not found.");
                    return View("recieveAsset", model);
                }

                // Update movement properties
                movement.DateReceived = model.MoveAsset.DateReceived;
                movement.Condition = model.MoveAsset.Condition;
                movement.ReceivedBy = model.MoveAsset.ReceivedBy;
                movement.Remarks = model.MoveAsset.Remarks;

                _context.Update(movement);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(recieveAsset));
            }

            return View("recieveAsset",Data(movemodel:model.MoveAsset).Result);
        }
        public async Task<IActionResult> approveMovement(Guid id)
        {
            Debug.WriteLine(id);

             
                var movement = _context.AssetMovement
                    .FirstOrDefault(m => m.MovementId == id); 

                if (movement == null)
                {
                Debug.WriteLine(movement.MovementId);
                    //ModelState.AddModelError("", "Asset movement not found.");
                    return RedirectToAction(nameof(Index));
                }

                // Update movement properties
                movement.IsApproved = true;
                movement.ApprovedBy = Guid.NewGuid();
                _context.Update(movement);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            
        }
        [HttpGet]
        public async Task<IActionResult> GatePass(Guid id)
        {
            
            var movement = await _context.AssetMovement
                .Include(m => m.Asset)
                .Include(m => m.Facility)
                .Include(m => m.From)
                .Include(m => m.ServicePoint)
                .Include(m => m.FunctionalStatus)
                .FirstOrDefaultAsync(m => m.MovementId == id);

            if (movement == null)
                return NotFound();

            return View("GatePass", movement);
        }


    }
}
