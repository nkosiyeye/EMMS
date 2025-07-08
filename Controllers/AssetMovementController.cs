using EMMS.CustomAttributes;
using EMMS.Data;
using EMMS.Data.Migrations;
using EMMS.Data.Repository;
using EMMS.Models;
using EMMS.Models.Entities;
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
            };

            return paginatedMovement;

        }

        [HttpGet]
        [RequireLogin]
        public async Task<IActionResult> Index()
        {
            var _repo = new AssetMovementRepo(_context);
            var data = await Data();
            data.MoveAssets = await _repo.GetAssetMovement();//.Result.Where(m => m.FromId == CurrentUser.FacilityId);
            data.Conditions = await _repo.GetConditions();
            return View(data);
        }

        [HttpGet]
        [RequireLogin]
        public async Task<IActionResult> moveAsset(Guid id)
        {
            var _repo = new AssetMovementRepo(_context);
            var moveAsset = new MoveAsset();

            var history = await _repo.GetLastMovement(id);
            if (history != null)
            {
                moveAsset.AssetId = id;
                moveAsset.FacilityId = history.FacilityId;
                moveAsset.Asset = history.Asset;
            }
            else
            {
                var assetRepo = new AssetManagementRepo(_context);
                var assets = await assetRepo.GetAssetsFromDb();
                var asset = assets.FirstOrDefault(a => a.AssetId == id);
                if (asset == null)
                {
                    TempData["MovementError"] = "Asset not found.";
                    return RedirectToAction(nameof(Index));
                }
                moveAsset.AssetId = asset.AssetId;
                moveAsset.Asset = asset;
            }

            var moveAssetViewModel = new MoveRequestViewModel()
            {
                AssetTag = moveAsset!.Asset!.AssetTagNumber,
                MoveAsset = moveAsset,
                // MovementTypes = await _repo.GetMovementTypes(),
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
                AssetTag = moveAsset!.Asset!.AssetTagNumber,
                MoveAsset = moveAsset,

                // MovementTypes = await _repo.GetMovementTypes(),
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
            if (ModelState.IsValid)
            {
                UpdateEntity(assetMovement!); 

                _context.Update(assetMovement!);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            TempData["MovementError"] = string.Join("; ", errors);
            var _repo = new AssetMovementRepo(_context);
            var moveAssetViewModel = new MoveRequestViewModel()
            {
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

            var assetMovement = asmove.MoveAsset;
            var movement = _context.AssetMovement.OrderByDescending(m => m.DateCreated)
                   .FirstOrDefault(m => m.AssetId == asmove.MoveAsset!.AssetId);
            if (movement != null)
            {
                if(movement.DateRejected == null && movement.DateReceived == null) ModelState.AddModelError("", "Asset Already has a movement pending.");
            }

            if (ModelState.IsValid)
            {
                var _repo = new AssetMovementRepo(_context);

                assetMovement.FromId = CurrentUser!.FacilityId ?? 1; 
                movement.MovementDate = assetMovement.MovementDate.Date
                                            .AddHours(DateTime.Now.Hour)
                                            .AddMinutes(DateTime.Now.Minute)
                                            .AddSeconds(DateTime.Now.Second);

                CreateEntity(assetMovement);

                _context.Add(assetMovement);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }


            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            TempData["MovementError"] = string.Join("; ", errors);

            // Repopulate dropdowns for the view
            var _repoReload = new AssetMovementRepo(_context);
            var moveAssetViewModel = new MoveRequestViewModel()
            {
                MoveAsset = assetMovement,
                Facilities = await _repoReload.GetFacilities(),
                ServicePoints = await _repoReload.GetServicePoints(),
                Reasons = await _repoReload.GetReasons(),
                FunctionalStatuses = await _repoReload.GetFunctionalStatuses(),
            };

            return View("moveAsset", moveAssetViewModel);
        }

        [HttpGet]
        [RequireLogin]
        public async Task<IActionResult> recieveAsset()
        {
            var _repo = new AssetMovementRepo(_context);
            var data = await Data();
            data.MoveAssets = _repo.GetAssetMovement().Result.Where(m => m.FromId == CurrentUser.FacilityId);
            data.Conditions = await _repo.GetConditions();
            data.Reasons = await _repo.GetReasons();
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

        [HttpPost]
        public async Task<IActionResult> rejectAsset(MoveAssetViewModel model)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            TempData["Error"] = string.Join("; ", errors);

            if (ModelState.IsValid)
            {
                var movement = await _context.AssetMovement
                    .FirstOrDefaultAsync(m => m.MovementId == model.MoveAsset.MovementId);

                if (movement == null)
                {
                    ModelState.AddModelError("", "Asset movement not found.");
                    return View("recieveAsset", model);
                }
                var move = movement;

                // Update movement properties
                movement.DateRejected = model.MoveAsset.DateRejected;
                movement.RejectedReasonId = model.MoveAsset.RejectedReasonId;
                movement.RejectedBy = model.MoveAsset.RejectedBy;
                movement.Remarks = model.MoveAsset.Remarks;

                _context.Update(movement);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(recieveAsset));
            }

            return View("recieveAsset", Data(movemodel: model.MoveAsset).Result);
        }
        public async Task<IActionResult> approveMovement(Guid id)
        {
             
                var movement = _context.AssetMovement
                    .FirstOrDefault(m => m.MovementId == id);
                var assetTag = _context.Assets
                .FirstOrDefault(a => a.AssetId == movement.AssetId).AssetTagNumber;

                if (movement == null)
                {
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
                .FirstOrDefaultAsync(m => m.MovementId == id);

            if (movement == null)
                return NotFound();

            return View("GatePass", movement);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireLogin]
        public async Task<IActionResult> deteleMovement(Guid id)
        {
            var movement = await _context.AssetMovement.FindAsync(id);
            if (movement == null)
            {
                TempData["Error"] = "Asset movement not found.";
                return RedirectToAction(nameof(Index));
            }

            _context.AssetMovement.Remove(movement);
            await _context.SaveChangesAsync();
            TempData["MovementSuccess"] = "Asset movement deleted successfully.";
            return RedirectToAction(nameof(Index));
        }


    }
}
