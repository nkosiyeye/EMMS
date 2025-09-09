using EMMS.Service;
using Microsoft.AspNetCore.Mvc;

namespace EMMS.Controllers
{
    public class NotificationController : BaseController
    {
        private readonly NotificationService _notificationService;

        public NotificationController(NotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        public async Task<IActionResult> Index()
        {
            var userRole = ViewBag._currrentUserRole;  // set in BaseController
            var notifications = await _notificationService
                .GetNotificationsAsync(CurrentUser!, userRole.UserType);

            return View(notifications);
        }

        [HttpPost]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            await _notificationService.MarkAsReadAsync(id);
            return Ok();
        }
    }
}
