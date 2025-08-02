using EMMS.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EMMS.Controllers
{
    public class NotificationController : BaseController
    {
        private readonly ApplicationDbContext _context;
        public NotificationController(ApplicationDbContext context)
        {
            _context = context;
        }
        public async Task<IActionResult> Index()
        {
            var allNotifications = await _context.Notifications
                .Where(n => n.RowState == Models.Enumerators.RowStatus.Active)
                .Include(n => n.Facility)
                .OrderByDescending(n => n.DateCreated)
                .ToListAsync();
            var filteredNotifications = allNotifications
                .Where(n => n.FacilityId == CurrentUser?.FacilityId)
                .ToList();
            var notifications = isAdmin
                ? allNotifications
                : filteredNotifications;
            return View(notifications);
        }
    }
}
