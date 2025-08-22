using System.Diagnostics;
using System.Linq;
using DocumentFormat.OpenXml.Office2010.Excel;
using EMMS.CustomAttributes;
using EMMS.Data;
using EMMS.Data.Migrations;
using EMMS.Models.Admin;
using EMMS.Models.Entities;
using EMMS.ViewModels;
using EMMS.ViewModels.Admin;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static EMMS.Models.Enumerators;

namespace EMMS.Controllers.Admin
{
    public class AdminController : BaseController
    {
        private readonly ApplicationDbContext _context;
        public AdminController(ApplicationDbContext context)
        {
            _context = context;

        }
        public async Task<LookupViewModel> loadLookupViewModel(LookupItem lookupItem,int id)
        {
            var lookupList = await _context.LookupLists.FirstOrDefaultAsync(l => l.LookupListId == id);
            var viewModel = new LookupViewModel
            {
                _lookupItem = lookupItem,
                _lookupItems = lookupList.Name == "SubCategory"
                    ? await _context.LookupItems.Include(l => l.LookupList).Where(l => l.LookupList!.Name == "Category").ToListAsync()
                    : null,
                _facilities = lookupList.Name.ToLower().Contains("service")
                    ? await _context.Facilities.ToListAsync()
                    : null,
                LookupList = lookupList
            };
            return viewModel;

        }
        public async Task<IActionResult> LookupList()
        {
            var viewModel = new LookupViewModel()
            {
                _lookupLists = await _context.LookupLists
                .OrderBy(l => l.Name)
                .ToListAsync()
            };
            return View(viewModel);
        }
        [AuthorizeRole(nameof(UserType.Administrator))]
        public async Task<IActionResult> Index(int id)
        {
            var viewModel = new LookupViewModel();
            var lookupList = await _context.LookupLists
                .FirstOrDefaultAsync(l => l.LookupListId == id);
            var lookups = await _context.LookupItems
                .Include(l => l.LookupList)
                .Include(l => l.Parent)
                .Include(l => l.ParentFacility)
                .Where(l => l.LookupListId == id)
                .OrderByDescending(l => l.DateCreated)
                .ToListAsync();
            viewModel._lookupItems = lookups;
            viewModel.LookupList = lookupList;
            return View("_LookupTable", viewModel);
        }
        [HttpGet]
        [AuthorizeRole(nameof(UserType.Administrator))]
        public async Task<IActionResult> AddLookupItem(int id)
        {
            var viewModel = await loadLookupViewModel(new LookupItem(),id);

            return View(viewModel);
        }

        [HttpPost]
        [AuthorizeRole(nameof(UserType.Administrator))]
        public async Task<IActionResult> AddLookupItem(LookupViewModel vModel)
        {
            var lookup = vModel._lookupItem;
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            TempData["LookupError"] = string.Join("; ", errors);
            if (ModelState.IsValid)
            {
                CreateEntity(lookup!);
                _context.LookupItems.Add(lookup!);
                await _context.SaveChangesAsync();
                return RedirectToAction("Index", new { id = lookup!.LookupListId });
            }

            return RedirectToAction(nameof(AddLookupItem), new { id = lookup!.LookupListId });
        }
        [AuthorizeRole(nameof(UserType.Administrator))]
        public async Task<IActionResult> EditLookupItem(int id)
        {
            var lookupItem = await _context.LookupItems
                .FirstOrDefaultAsync(l => l.Id == id);
            if (lookupItem == null)
            {
                return NotFound();
            }
            var viewModel = await loadLookupViewModel(lookupItem, lookupItem.LookupListId);
            return View(viewModel);
        }
        [HttpPost]
        //[RequireLogin]
        public async Task<IActionResult> EditLookupItem(LookupViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["MovementError"] = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                return RedirectToAction(nameof(EditLookupItem), new { id = model._lookupItem.Id });
            }
            var rowState = model._lookupItem.RowState;

            UpdateEntity(model._lookupItem);
            model._lookupItem.RowState = rowState;
            _context.Update(model._lookupItem);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index), new { id = model._lookupItem.LookupListId });
        }
        [AuthorizeRole(nameof(UserType.Administrator))]
        public async Task<IActionResult> UserRoles()
        {
            var userRoles = await _context.UserRole
                .OrderBy(r => r.Name)
                .ToListAsync();
            return View(userRoles);
        }

        [HttpGet]
        [AuthorizeRole(nameof(UserType.Administrator))]
        public async Task<IActionResult> createUserRole()
        {
            var userRole = new UserRole();       
            return View(userRole);
        }

        [HttpPost]
        [AuthorizeRole(nameof(UserType.Administrator))]
        public async Task<IActionResult> createUserRole(UserRole uRole)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            TempData["Error"] = string.Join("; ", errors);
            if (ModelState.IsValid)
            {
                _context.UserRole.Add(uRole);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(UserRoles));
            }
            return View(uRole);
        }
    }
}
