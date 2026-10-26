using EMMS.CustomAttributes;
using EMMS.Data.Repository;
using EMMS.Service;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using static EMMS.Models.Enumerators;

namespace EMMS.Controllers
{
    /// <summary>
    /// Preventive maintenance schedule: serviceable equipment by next service date, and raising service requests.
    /// Admins see every facility (or pick one); everyone else the equipment currently at their facility.
    /// </summary>
    [RequireLogin]
    public class ServiceScheduleController : BaseController
    {
        private readonly ServiceScheduleService _schedule;
        private readonly AssetManagementRepo _assetRepo;

        public ServiceScheduleController(ServiceScheduleService schedule, AssetManagementRepo assetRepo)
        {
            _schedule = schedule;
            _assetRepo = assetRepo;
        }

        private static bool CanRaise(UserType role) =>
            role is UserType.Administrator or UserType.FacilityManager or UserType.Biomed;

        [HttpGet]
        [FacilityChecked("Non-admins are pinned to their facility (AssetScopes)")]
        public async Task<IActionResult> Index(string? show, int? facilityId)
        {
            var scope = isAdmin ? facilityId : CurrentUser!.FacilityId;
            var model = await _schedule.BuildAsync(scope, show, DateTime.Today);
            model.CanRaise = CanRaise(CurrentUser!.UserRole.UserType);
            if (isAdmin)
                model.Facilities = new SelectList(await _assetRepo.GetActiveFacilitiesAsync(), "FacilityId", "FacilityName", facilityId);
            return View(model);
        }

        [HttpGet]
        [FacilityChecked("Non-admins are pinned to their facility (AssetScopes)")]
        public async Task<IActionResult> Calendar(string? month, int? facilityId)
        {
            var scope = isAdmin ? facilityId : CurrentUser!.FacilityId;
            var model = await _schedule.BuildCalendarAsync(scope, ServiceScheduleService.ParseMonth(month, DateTime.Today), DateTime.Today);
            if (isAdmin)
                model.Facilities = new SelectList(await _assetRepo.GetActiveFacilitiesAsync(), "FacilityId", "FacilityName", facilityId);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AuthorizeRole(nameof(UserType.Administrator), nameof(UserType.FacilityManager), nameof(UserType.Biomed))]
        [FacilityChecked("ServiceScheduleService.RaiseAsync keeps to the user's facility (AssetScopes)")]
        public async Task<IActionResult> Raise(List<Guid> assetIds, string? show, int? facilityId)
        {
            var (raised, skipped, error) = await _schedule.RaiseAsync(CurrentUser!, assetIds, isAdmin);
            if (error != null)
                TempData["Error"] = error;
            else if (raised == 0)
                TempData["Error"] = "No service requests were raised; the selected equipment already has open requests.";
            else
                TempData["Success"] = (raised == 1 ? "Service request raised." : $"{raised} service requests raised.")
                    + (skipped > 0 ? $" {skipped} skipped (already open or not at your facility)." : "");

            return RedirectToAction(nameof(Index), new { show = ServiceScheduleService.NormalizeShow(show), facilityId });
        }
    }
}
