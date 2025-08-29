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
                .Take(20)
                .ToListAsync();
            var filteredNotifications = await _context.Notifications
                .Where(n => n.RowState == Models.Enumerators.RowStatus.Active && n.FacilityId == CurrentUser!.FacilityId)
                .Include(n => n.Facility)
                .OrderByDescending(n => n.DateCreated)
                .Take(20)
                .ToListAsync();
            var notifications = isAdmin
                ? allNotifications
                : filteredNotifications;
            return View(notifications);
        }
    }
}
