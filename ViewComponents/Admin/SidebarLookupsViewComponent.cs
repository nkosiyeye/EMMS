using EMMS.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EMMS.ViewComponents.Admin
{
    public class SidebarLookupsViewComponent : ViewComponent
    {
        private readonly ApplicationDbContext _context;
        public SidebarLookupsViewComponent(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var lookups = await _context.LookupLists
                .OrderBy(l => l.Name)
                .ToListAsync();
            return View(lookups);
        }
    }
}
