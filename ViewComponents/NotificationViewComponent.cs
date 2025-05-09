using EMMS.Models;
using Microsoft.AspNetCore.Mvc;

namespace EMMS.ViewComponents
{
    [ViewComponent(Name = "BellNotifications")]
    public class NotificationViewComponent : ViewComponent
    {
        public async Task<IViewComponentResult> InvokeAsync()
        {
            var notifications = new List<Notification>
            {
                new Notification
                {
                    id = 1,
                    type = "info",
                    icon = "fa fa-toolbox",
                    message = "New work request submitted.",
                    time = "1 hour ago",
                    controller = "#",
                    action = "#"
                },
                new Notification
                {
                    id = 2,
                    type = "success",
                    icon = "fas fa-box-open",
                    message = "New Asset added to inventory.",
                    time = "1 day ago",
                    controller = "#",
                    action = "#"

                },
                new Notification
                {
                    id = 3,
                    type = "warning",
                    icon = "fas fa-arrows-turn-to-dots",
                    message = "Recieve Incoming Asset",
                    time = "1 week ago",
                    controller = "#",
                    action = "#"
                }
            };

            return View("Index", notifications);
        }
    }
}