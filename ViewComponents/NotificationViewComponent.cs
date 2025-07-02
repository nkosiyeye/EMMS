using EMMS.Models;
using Microsoft.AspNetCore.Mvc;

namespace EMMS.ViewComponents
{
    [ViewComponent(Name = "BellNotifications")]
    public class NotificationViewComponent : ViewComponent
    {
        public async Task<IViewComponentResult> InvokeAsync()
        {
            var notifications = new List<Notification>();

            return View("Index", notifications);
        }
    }
}