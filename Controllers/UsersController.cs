using EMMS.Service;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using EMMS.Data;
using EMMS.Models.Admin;
using EMMS.Auth;
using EMMS.CustomAttributes;
using static EMMS.Models.Enumerators;
using DocumentFormat.OpenXml.Spreadsheet;
using EMMS.Data.Migrations;

namespace EMMS.Controllers
{
    public class UsersController : BaseController
    {
        private readonly ApplicationDbContext _context;

        public UsersController(ApplicationDbContext context)
        {
            _context = context;
        }

        [AuthorizeRole("Administrator")]
        [RequireLogin]
        [FacilityChecked("Administrators only")]
        public async Task<IActionResult> Index()
        {
            var applicationDbContext = _context.User.Include(u => u.Designations).Include(u => u.Facility).Include(u => u.UserRole);
            return View(await applicationDbContext.ToListAsync());
        }

        [AuthorizeRole("Administrator")]
        [RequireLogin]
        [FacilityChecked("Administrators only")]
        public async Task<IActionResult> Export()
        {
            var users = await _context.User.Include(u => u.Facility).Include(u => u.UserRole)
                .OrderBy(u => u.FirstName).ThenBy(u => u.LastName).ToListAsync();

            using var workbook = new ClosedXML.Excel.XLWorkbook();
            var sheet = workbook.Worksheets.Add("Users");
            var headers = new[] { "First Name", "Last Name", "Date of Birth", "Gender", "Cellphone", "Facility", "Username", "Role", "Status" };
            for (int i = 0; i < headers.Length; i++)
            {
                sheet.Cell(1, i + 1).Value = headers[i];
                sheet.Cell(1, i + 1).Style.Font.Bold = true;
                sheet.Cell(1, i + 1).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightGray;
            }
            var row = 2;
            foreach (var u in users)
            {
                sheet.Cell(row, 1).Value = u.FirstName ?? "";
                sheet.Cell(row, 2).Value = u.LastName ?? "";
                sheet.Cell(row, 3).Value = u.DOB.ToString("yyyy-MM-dd");
                sheet.Cell(row, 4).Value = u.Gender.ToString();
                sheet.Cell(row, 5).Value = u.Cellphone ?? "";
                sheet.Cell(row, 6).Value = u.Facility?.FacilityName ?? "";
                sheet.Cell(row, 7).Value = u.Username ?? "";
                sheet.Cell(row, 8).Value = u.UserRole?.Name ?? "";
                sheet.Cell(row, 9).Value = u.RowState.ToString();
                row++;
            }
            sheet.Columns().AdjustToContents();
            EMMS.Utility.ExcelLetterhead.Apply(workbook);

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"EMMS_Users_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
        }

        // GET: Users/Details/5
        [FacilityScoped(FacilityRecord.User)]
        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var user = await _context.User
                .Include(u => u.Designations)
                .Include(u => u.Facility)
                .Include(u => u.UserRole)
                .FirstOrDefaultAsync(m => m.UserId == id);
            if (user == null)
            {
                return NotFound();
            }

            return View(user);
        }

        // GET: Users/Create
        [AuthorizeRole("Administrator")]
        [FacilityChecked("Administrators only")]
        public IActionResult Create()
        {
            ViewData["DesignationId"] = new SelectList(_context.LookupItems.Include(x => x.LookupList).Where(l => l.LookupList.Name.Contains("Desig") && l.RowState == RowStatus.Active), "Id", "Name");
            ViewData["FacilityId"] = new SelectList(_context.Facilities.Where(f => f.RowState == RowStatus.Active), "FacilityId", "FacilityName");
            ViewData["UserRoleId"] = new SelectList(_context.UserRole.Where(f => f.RowState == RowStatus.Active), "Id", "Name");
            return View();
        }

        // POST: Users/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AuthorizeRole("Administrator")]
        [FacilityChecked("Administrators only")]
        public async Task<IActionResult> Create(User user)
        {
            var usernameExists = await _context.User.FirstOrDefaultAsync(u => u.Username == user.Username);
            if (string.IsNullOrWhiteSpace(user.Password))
            {
                ModelState.AddModelError("user.Password", "User Password can not be empty.");
            }else if (usernameExists != null)
            {
                ModelState.AddModelError("user.Username", "Username already Exists in the system");

            }
            if (ModelState.IsValid)
            {
                user.UserId = Guid.NewGuid();
                user.DateCreated = DateTime.Now;
                user.Password = PasswordManager.Encrypt(user.Password!);
                CreateEntity(user);
                //user.CreatedBy = TBD 
                _context.Add(user);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            TempData["UserCreationError"] = string.Join("; ", errors);
            ViewData["DesignationId"] = new SelectList(_context.LookupItems.Where(f => f.LookupList.Name.Contains("Desig") && f.RowState == RowStatus.Active), "Id", "Name", user.DesignationId);
            ViewData["FacilityId"] = new SelectList(_context.Facilities.Where(f => f.RowState == RowStatus.Active), "FacilityId", "FacilityName", user.FacilityId);
            ViewData["UserRoleId"] = new SelectList(_context.UserRole.Where(f => f.RowState == RowStatus.Active), "Id", "Name", user.UserRoleId);
            return View(user);
        }

        // GET: Users/Edit/5
        [FacilityScoped(FacilityRecord.User)]
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var user = await _context.User.FindAsync(id);
            if (user == null)
            {
                return NotFound();
            }
            ViewData["DesignationId"] = new SelectList(_context.LookupItems.Where(f => f.LookupList.Name.Contains("Desig") && f.RowState == RowStatus.Active), "Id", "Name", user.DesignationId);
            ViewData["FacilityId"] = new SelectList(_context.Facilities.Where(f => f.RowState == RowStatus.Active), "FacilityId", "FacilityName", user.FacilityId);
            ViewData["UserRoleId"] = new SelectList(_context.UserRole.Where(f => f.RowState == RowStatus.Active), "Id", "Name", user.UserRoleId);
            return View(user);
        }


        // POST: Users/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [FacilityScoped(FacilityRecord.User)]
        public async Task<IActionResult> Edit(Guid id, User user)
        {
            if (id != user.UserId)
            {
                return NotFound();
            }
            var existingUser = await _context.User.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == id);
            if (existingUser == null)
            {
                return NotFound();
            }

            // The form only disables these for non-admins; the server is what keeps them unchanged.
            user.Username = existingUser.Username;
            user.CreatedBy = existingUser.CreatedBy;
            user.DateCreated = existingUser.DateCreated;
            if (!isAdmin)
            {
                user.UserRoleId = existingUser.UserRoleId;
                user.FacilityId = existingUser.FacilityId;
                user.DesignationId = existingUser.DesignationId;
                user.RowState = existingUser.RowState;
            }

            // A new password is hashed; an empty one keeps the current password
            user.Password = string.IsNullOrWhiteSpace(user.Password)
                ? existingUser.Password
                : PasswordManager.Encrypt(user.Password!);

            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            TempData["UserEditError"] = string.Join("; ", errors);

            if (ModelState.IsValid)
            {
                try
                {
                    UpdateEntity(user);
                    _context.Update(user);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!UserExists(user.UserId))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Details),new { id = user.UserId});
            }
            ViewData["DesignationId"] = new SelectList(_context.LookupItems.Where(f => f.LookupList.Name.Contains("Desig") && f.RowState == RowStatus.Active), "Id", "Name", user.DesignationId);
            ViewData["FacilityId"] = new SelectList(_context.Facilities.Where(f => f.RowState == RowStatus.Active), "FacilityId", "FacilityName", user.FacilityId);
            ViewData["UserRoleId"] = new SelectList(_context.UserRole.Where(f => f.RowState == RowStatus.Active), "Id", "Name", user.UserRoleId);
            return View(user);
        }

        // GET: Users/Delete/5
        [AuthorizeRole("Administrator")]
        [FacilityChecked("Administrators only")]
        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }
            var user = await _context.User
                .Include(u => u.Designations)
                .Include(u => u.Facility)
                .Include(u => u.UserRole)
                .FirstOrDefaultAsync(m => m.UserId == id);
            if (user == null)
            {
                return NotFound();
            }

            return View(user);
        }

        // POST: Users/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [AuthorizeRole("Administrator")]
        [FacilityChecked("Administrators only")]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var user = await _context.User.FindAsync(id);
            if (user != null)
            {
                _context.User.Remove(user);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool UserExists(Guid id)
        {
            return _context.User.Any(e => e.UserId == id);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AuthorizeRole("Administrator")]
        [FacilityChecked("Administrators only")]
        public IActionResult ActivateUser(Guid id)
        {
            User? user = _context.User.Find(id);
            if (user != null)
            {
                user.RowState = user.RowState == RowStatus.Active ? RowStatus.Inactive : RowStatus.Active;
                _context.User.Update(user);
                _context.SaveChanges();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
