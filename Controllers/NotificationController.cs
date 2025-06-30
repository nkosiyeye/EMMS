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
            var notifications = await _context.Notifications
            .OrderByDescending(n => n.DateCreated)
            .ToListAsync();
            return View(notifications);
        }
    }
}
