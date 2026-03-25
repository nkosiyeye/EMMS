using EMMS.Models.Admin;
using EMMS.Models;
using EMMS.Service;
using Microsoft.AspNetCore.Mvc;
using EMMS.Utility;

namespace EMMS.Controllers
{
    [Route("api/notifications")]
    public class NotificationApiController : BaseController
    {
        private readonly INotificationService _notificationService;
        public NotificationApiController(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        [HttpGet("current")]
        public async Task<IActionResult> GetCurrent()
        {
            var currentUser = CurrentUser;
            var role = CurrentUser.UserRole.UserType;
            var notifications = await _notificationService.GetNotificationsAsync(currentUser!, role);

            return Json(new
            {
                unreadCount = notifications.Count(n => !n.IsRead),
                //html = await this.RenderViewAsync("_NotificationsListPartial", notifications, true)
            });
        }
    }
}
