using EMMS.Models.Admin;
using EMMS.Service;
using Microsoft.AspNetCore.Mvc;
using EMMS.Models;

namespace EMMS.ViewComponents
{
    [ViewComponent(Name = "BellNotifications")]
    public class NotificationViewComponent : ViewComponent
    {
        private readonly NotificationService _notificationService;

        public NotificationViewComponent(NotificationService notificationService)
        {
            _notificationService = notificationService;
        }
        // Accept currentUser and role as parameters
        public async Task<IViewComponentResult> InvokeAsync(User currentUser, Enumerators.UserType role)
        {
            if (currentUser == null)
                return View("Index", new List<Models.Entities.Notification>()); // Return empty if user is null

            var notifications = await _notificationService.GetNotificationsAsync(currentUser, role);

            // Take top 3 notifications for the bell
            var topNotifications = notifications.Where(n => n.IsRead == false).Take(3).ToList();

            return View("Index", topNotifications);
        }
    }
}