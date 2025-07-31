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
using System.Drawing.Printing;
using System.Threading.Tasks;
using static EMMS.Models.Enumerators;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

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

        private AssetMovementRepo GetRepo() => new AssetMovementRepo(_context);

        private async Task<MoveAssetViewModel> LoadViewModel(MoveAsset? moveModel = null)
        {
            var assets = await _assetService.GetAssetIndexViewModel(CurrentUser);
            return new MoveAssetViewModel
            {
                MoveAsset = moveModel ?? new MoveAsset(),
                AssetIndex = assets
            };
        }

        [HttpGet]
        [RequireLogin]
        public async Task<IActionResult> Index()
        {
            var repo = GetRepo();
            var data = await LoadViewModel();
            var movements = await repo.GetAssetMovement();

            data.MoveAssets = isAdmin
                ? movements
                : movements.Where(m => m.FromId == CurrentUser.FacilityId);

            data.Conditions = await repo.GetConditions();
            return View(data);
        }

        [HttpGet]
        public async Task<IActionResult> GetFacilites(bool isOffSite)
        {
            var facilities = await _context.Facilities
                .Where(x => (x.isOffSite ?? false) == isOffSite && x.RowState == RowStatus.Active)
                .Select(x => new { x.FacilityId, x.FacilityName })
                .ToListAsync();

            return Json(facilities);
        }

        [HttpGet]
        [RequireLogin]
        public async Task<IActionResult> MoveAsset(Guid id)
        {
            var repo = GetRepo();
            var moveAsset = new MoveAsset();
            var history = await repo.GetLastMovement(id);

            if (history != null)
            {
                moveAsset.AssetId = id;
                moveAsset.FacilityId = history.FacilityId;
                moveAsset.Asset = history.Asset;
            }
            else
            {
                var asset = (await new AssetManagementRepo(_context).GetAssetsFromDb())
                    .FirstOrDefault(a => a.AssetId == id);
                if (asset == null)
                {
                    TempData["MovementError"] = "Asset not found.";
                    return RedirectToAction(nameof(Index));
                }
                moveAsset.AssetId = asset.AssetId;
                moveAsset.Asset = asset;
            }

            var viewModel = new MoveRequestViewModel
            {
                AssetTag = moveAsset.Asset.AssetTagNumber,
                MoveAsset = moveAsset,
                Facilities = await repo.GetFacilities(),
                ServicePoints = await repo.GetServicePoints(),
                Reasons = await repo.GetReasons(),
                FunctionalStatuses = await repo.GetFunctionalStatuses()
            };

            return View(viewModel);
        }

        [HttpGet]
        [RequireLogin]
        public async Task<IActionResult> Edit(Guid id)
        {
            var repo = GetRepo();
            var moveAsset = (await repo.GetAssetMovement()).FirstOrDefault(m => m.MovementId == id);

            var viewModel = new MoveRequestViewModel
            {
                AssetTag = moveAsset.Asset.AssetTagNumber,
                MoveAsset = moveAsset,
                Facilities = await repo.GetFacilities(),
                ServicePoints = await repo.GetServicePoints(),
                Reasons = await repo.GetReasons(),
                FunctionalStatuses = await repo.GetFunctionalStatuses()
            };

            return View(viewModel);
        }

        [HttpPost]
        [RequireLogin]
        public async Task<IActionResult> Edit(MoveRequestViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["MovementError"] = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                return View(model);
            }

            UpdateEntity(model.MoveAsset);
            _context.Update(model.MoveAsset);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> MoveAsset(MoveRequestViewModel model)
        {
            var assetMovement = model.MoveAsset;
            var repo = GetRepo();
            var existing = _context.AssetMovement
                .OrderByDescending(m => m.DateCreated)
                .FirstOrDefault(m => m.AssetId == assetMovement.AssetId);


            if (existing != null && existing.DateRejected == null && existing.DateReceived == null)
            {
                ModelState.AddModelError("", "Asset already has a movement pending.");
            }
            var workrequest = _context.WorkRequest.OrderByDescending(m => m.DateCreated)
                   .FirstOrDefault(m => m.AssetId == assetMovement.AssetId);
            if (workrequest != null && workrequest.CloseDate == null)
            {

                ModelState.AddModelError("", "Cant Move an Asset that has a workrequest in progress.");
            }
            

            if (!ModelState.IsValid)
            {
                TempData["MovementError"] = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                model.Facilities = await repo.GetFacilities();
                model.ServicePoints = await repo.GetServicePoints();
                model.Reasons = await repo.GetReasons();
                model.FunctionalStatuses = await repo.GetFunctionalStatuses();
                return View("MoveAsset", model);
            }

            var history = await repo.GetLastMovement(assetMovement.AssetId);
            assetMovement.FromId = history?.FacilityId ?? CurrentUser.FacilityId;
            assetMovement.MovementDate = assetMovement.MovementDate.Date
                .AddHours(DateTime.Now.Hour)
                .AddMinutes(DateTime.Now.Minute)
                .AddSeconds(DateTime.Now.Second);

            CreateEntity(assetMovement);
            _context.Add(assetMovement);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        [RequireLogin]
        public async Task<IActionResult> RecieveAsset()
        {
            var repo = GetRepo();
            var data = await LoadViewModel();
            var movements = await repo.GetAssetMovement();

            data.MoveAssets = isAdmin
                ? movements
                : movements.Where(m => m.FacilityId == CurrentUser.FacilityId);

            data.Conditions = await repo.GetConditions();
            data.Reasons = await repo.GetReasons();
            return View(data);
        }

        [HttpPost]
        public async Task<IActionResult> ReceiveAsset(MoveAssetViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                return View("RecieveAsset", await LoadViewModel(model.MoveAsset));
            }

            var movement = await _context.AssetMovement.FindAsync(model.MoveAsset.MovementId);
            if (movement == null)
            {
                ModelState.AddModelError("", "Asset movement not found.");
                return View("RecieveAsset", model);
            }

            movement.DateReceived = model.MoveAsset.DateReceived;
            movement.Condition = model.MoveAsset.Condition;
            movement.ReceivedBy = model.MoveAsset.ReceivedBy;
            movement.Remarks = model.MoveAsset.Remarks;

            _context.Update(movement);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(RecieveAsset));
        }

        [HttpPost]
        public async Task<IActionResult> RejectAsset(MoveAssetViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                return View("RecieveAsset", await LoadViewModel(model.MoveAsset));
            }

            var movement = await _context.AssetMovement.FindAsync(model.MoveAsset.MovementId);
            if (movement == null)
            {
                ModelState.AddModelError("", "Asset movement not found.");
                return View("RecieveAsset", model);
            }

            movement.DateRejected = model.MoveAsset.DateRejected;
            movement.RejectedReasonId = model.MoveAsset.RejectedReasonId;
            movement.RejectedBy = model.MoveAsset.RejectedBy;
            movement.Remarks = model.MoveAsset.Remarks;

            _context.Update(movement);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(RecieveAsset));
        }

        public async Task<IActionResult> ApproveMovement(Guid id)
        {
            var movement = await _context.AssetMovement.FindAsync(id);
            if (movement == null) return RedirectToAction(nameof(Index));

            movement.IsApproved = true;
            movement.ApprovedBy = CurrentUser.UserId;
            UpdateEntity(movement);

            var asset = await _context.Assets.FindAsync(movement.AssetId);
            var notification = new Models.Entities.Notification
            {
                Message = $"Receive Asset: {asset?.AssetTagNumber}",
                Type = "move",
                DateCreated = DateTime.Now,
                FacilityId = movement.FacilityId,
                RowState = RowStatus.Active
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

            return movement == null ? NotFound() : View("GatePass", movement);
        }

        [HttpPost]
        //[ValidateAntiForgeryToken]
        //[RequireLogin]
        public async Task<IActionResult> deleteMovement(Guid id)
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
