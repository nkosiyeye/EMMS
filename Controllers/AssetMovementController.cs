using EMMS.CustomAttributes;
using EMMS.Data;
using EMMS.Data.Migrations;
using EMMS.Data.Repository;
using EMMS.Models;
using EMMS.Service;
using EMMS.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Threading.Tasks;
using static EMMS.Models.Enumerators;

namespace EMMS.Controllers
{
    public class AssetMovementController : BaseController
    {
        private readonly ApplicationDbContext _context;
        private readonly AssetService _assetService;
        public AssetMovementController(ApplicationDbContext context)
        {
            _context = context;
            _assetService = new AssetService(context);
        }
        public async Task<MoveAssetViewModel> Data(MoveAsset? movemodel = null)
        {
            if (movemodel is null)
            {
                movemodel = new MoveAsset();
            }
            var _repo = new AssetMovementRepo(_context);
            var assets = await _assetService.GetAssetIndexViewModel(CurrentUser); 
            MoveAssetViewModel paginatedMovement = new MoveAssetViewModel
            {
                //MoveAssets = all,
                MoveAsset = movemodel,
                AssetIndex = assets,
                //Conditions = await _repo.GetConditions()
            };

            return paginatedMovement;

        }

        [HttpGet]
        [RequireLogin]
        public async Task<IActionResult> Index()
        {
            var _repo = new AssetMovementRepo(_context);
            var data = await Data();
            data.MoveAssets = _repo.GetAssetMovement().Result.Where(m => m.FromId == CurrentUser.FacilityId);
            data.Conditions = await _repo.GetConditions();
            return View(data);
        }

        [HttpGet]
        [RequireLogin]
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

        [HttpGet]
        [RequireLogin]
        public async Task<IActionResult> edit(Guid id)
        {

            var moveAsset = new AssetMovementRepo(_context).GetAssetMovement().Result.FirstOrDefault(m => m.MovementId == id);
            var _repo = new AssetMovementRepo(_context);
            var moveAssetViewModel = new MoveRequestViewModel()
            {
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
        [RequireLogin]
        public async Task<IActionResult> edit(MoveRequestViewModel asmove)
        {

            var assetMovement = asmove.MoveAsset;
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            TempData["Error"] = string.Join("; ", errors);
            if (ModelState.IsValid)
            {
                //Debug.WriteLine("assetId"+assetMovement.AssetId);
                //assetMovement.IsApproved = false;
                UpdateEntity(assetMovement); // TBD Replace with actual user ID
                                             //asset.CreatedBy = User.FindFirstValue(ClaimTypes.NameIdentifier);

                _context.Update(assetMovement);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            var _repo = new AssetMovementRepo(_context);
            var moveAssetViewModel = new MoveRequestViewModel()
            {
                MoveAsset = assetMovement,

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
                
                assetMovement.FromId = CurrentUser.FacilityId ?? 1;//Update with logged in facility;
                if(assetMovement.ServicePointId != null)
                {
                    assetMovement.FacilityId = CurrentUser.FacilityId;
                }

                //Debug.WriteLine("assetId"+assetMovement.AssetId);
                //assetMovement.IsApproved = false;
                CreateEntity(assetMovement); // TBD Replace with actual user ID
                //asset.CreatedBy = User.FindFirstValue(ClaimTypes.NameIdentifier);
                

                _context.Add(assetMovement);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return RedirectToAction(nameof(moveAsset));
        }
        [HttpGet]
        [RequireLogin]
        public async Task<IActionResult> recieveAsset()
        {
            var _repo = new AssetMovementRepo(_context);
            var data = await Data();
            data.MoveAssets = _repo.GetAssetMovement().Result.Where(m => m.FromId == CurrentUser.FacilityId);
            data.Conditions = await _repo.GetConditions();
            return View(data);
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
                var assetTag = _context.Assets
                .FirstOrDefault(a => a.AssetId == movement.AssetId).AssetTagNumber;

                if (movement == null)
                {
                Debug.WriteLine(movement.MovementId);
                    //ModelState.AddModelError("", "Asset movement not found.");
                    return RedirectToAction(nameof(Index));
                }

                // Update movement properties
                movement.IsApproved = true;
                movement.ApprovedBy = CurrentUser.UserId;
                UpdateEntity(movement);
                var notification = new Models.Entities.Notification
                {
                    Message = $"Recieve Asset: {assetTag}",
                    Type = "move",
                    DateCreated = DateTime.Now,
                    FacilityId = movement.FacilityId,
                    RowState = RowStatus.Active
                    // UserId = ... // Optionally set for a specific user
                };
                _context.Notifications.Add(notification);
                _context.Update(movement);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            
        }
        [HttpGet]
        [RequireLogin]
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
