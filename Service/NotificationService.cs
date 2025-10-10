using EMMS.Models.Entities;
using EMMS.Models.Domain;
using Microsoft.EntityFrameworkCore;
using EMMS.Data;
using EMMS.Models;
using EMMS.Models.Admin;
using static EMMS.Models.Enumerators;

namespace EMMS.Service
{
    public interface INotificationService
    {
        Task CreateMovementRequestNotification(int facilityId, Guid createdBy);
        Task CreateMovementApprovalNotification(int facilityId, Guid createdBy);
        Task CreateWorkRequestNotification(int facilityId, Guid createdBy);
        Task CreateJobAssignmentNotification(Guid? biomedUserId, Guid? requestorUserId, Guid createdBy);
        Task CreateJobClaimedNotification(Guid? biomedUserId, Guid? requestorUserId, Guid createdBy);
        Task CreateJobCompletedNotification(Guid? requestorUserId, Guid createdBy);
    }

    public class NotificationService : INotificationService
    {
        private readonly ApplicationDbContext _context;

        public NotificationService(ApplicationDbContext context)
        {
            _context = context;
        }

        private async Task AddNotificationAsync(string message, string type, int? facilityId, Guid? userId, Guid createdBy)
        {
            var notification = new Models.Entities.Notification
            {
                Message = message,
                Type = type,
                FacilityId = facilityId,
                UserId = userId,
                CreatedBy = createdBy,
                DateCreated = DateTime.Now,
                RowState = Enumerators.RowStatus.Active
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();
        }

        // Movement request: alert Facility Manager + Admin
        public async Task CreateMovementRequestNotification(int facilityId, Guid createdBy)
        {
            var message = "A movement request has been submitted and requires approval.";
            await NotifyFacilityManagersAndAdmins(facilityId, message, "approval", createdBy);
        }

        // Movement approved: notify receiving facility (Biomed, Manager, Admin)
        public async Task CreateMovementApprovalNotification(int facilityId, Guid createdBy)
        {
            var message = "An equipment is on its way and requires reception.";
            await NotifyFacilityStakeholders(facilityId, message, "reception", createdBy);
        }

        // Work request created: notify Biomed in facility + Admin
        public async Task CreateWorkRequestNotification(int facilityId, Guid createdBy)
        {
            var message = "A new work request has been created.";
            await NotifyBiomedAndAdmins(facilityId, message, "work", createdBy);
        }

        // Admin assigns job: notify assigned Biomed + requestor
        public async Task CreateJobAssignmentNotification(Guid? biomedUserId, Guid? requestorUserId, Guid createdBy)
        {
            await AddNotificationAsync("A job card has been assigned to you.", "job", null, biomedUserId, createdBy);
            await AddNotificationAsync("Your work request has been assigned to a Biomed.", "job", null, requestorUserId, createdBy);
        }

        // Biomed claims job: notify requestor + Admin
        public async Task CreateJobClaimedNotification(Guid? biomedUserId, Guid? requestorUserId, Guid createdBy)
        {
            await AddNotificationAsync("A Biomed has claimed your work request.", "job", null, requestorUserId, createdBy);
            await NotifyAdmins("A Biomed has claimed a work request.", "job", createdBy);
        }

        // Job completed: notify requestor + Admin
        public async Task CreateJobCompletedNotification(Guid? requestorUserId, Guid createdBy)
        {
            await AddNotificationAsync("Your work request has been completed.", "job", null, requestorUserId, createdBy);
            await NotifyAdmins("A job card has been completed.", "job", createdBy);
        }

        // Helpers
        private async Task NotifyFacilityManagersAndAdmins(int facilityId, string message, string type, Guid createdBy)
        {
            var managers = await _context.User.Include(u => u.UserRole).Where(u => u.FacilityId == facilityId && u.UserRole.UserType == Enumerators.UserType.FacilityManager).ToListAsync();
            var admins = await _context.User.Include(u => u.UserRole).Where(u => u.UserRole.UserType == Enumerators.UserType.Administrator).ToListAsync();

            foreach (var user in managers.Concat(admins))
            {
                await AddNotificationAsync(message, type, facilityId, user.UserId, createdBy);
            }
        }

        private async Task NotifyFacilityStakeholders(int facilityId, string message, string type, Guid createdBy)
        {
            var stakeholders = await _context.User.Include(u => u.UserRole).Where(u => u.FacilityId == facilityId && (u.UserRole.UserType == Enumerators.UserType.Biomed || u.UserRole.UserType == Enumerators.UserType.FacilityManager || u.UserRole.UserType == Enumerators.UserType.Administrator)).ToListAsync();

            foreach (var user in stakeholders)
            {
                await AddNotificationAsync(message, type, facilityId, user.UserId, createdBy);
            }
        }

        private async Task NotifyBiomedAndAdmins(int facilityId, string message, string type, Guid createdBy)
        {
            var biomeds = await _context.User.Include(u => u.UserRole).Where(u => u.FacilityId == facilityId && u.UserRole.UserType == Enumerators.UserType.Biomed).ToListAsync();
            var admins = await _context.User.Include(u => u.UserRole).Where(u => u.UserRole.UserType == Enumerators.UserType.Administrator).ToListAsync();

            foreach (var user in biomeds.Concat(admins))
            {
                await AddNotificationAsync(message, type, facilityId, user.UserId, createdBy);
            }
        }

        private async Task NotifyAdmins(string message, string type, Guid createdBy)
        {
            var admins = await _context.User.Include(u => u.UserRole).Where(u => u.UserRole.UserType == Enumerators.UserType.Administrator).ToListAsync();

            foreach (var admin in admins)
            {
                await AddNotificationAsync(message, type, null, admin.UserId, createdBy);
            }
        }
        public async Task<List<Models.Entities.Notification>> GetNotificationsAsync(User currentUser, UserType role)
        {
            IQueryable<Models.Entities.Notification> query = _context.Notifications
                .Where(n => n.RowState == RowStatus.Active)
                .Include(n => n.Facility)
                .OrderByDescending(n => n.DateCreated);

            switch (role)
            {
                case UserType.Administrator:
                    // Admin sees all, no filter
                    break;

                case UserType.FacilityManager:
                    query = query.Where(n => n.FacilityId == currentUser.FacilityId);
                    break;

                case UserType.Biomed:
                    query = query.Where(n => (n.FacilityId == currentUser.FacilityId || n.UserId == currentUser.UserId) &&
                                             (n.Type == "work" || n.Type == "reception" || (n.Type == "job" && n.UserId == currentUser.UserId)));
                    break;

                default:
                    query = query.Where(n => n.UserId == currentUser.UserId);
                    break;
            }

            // ✅ Deduplicate by Id before returning
            var results = await query.Take(50).ToListAsync(); // fetch a bit more in case duplicates are trimmed

            return results
                .GroupBy(n => n.Message)
                .Select(g => g.First())
                .OrderByDescending(n => n.DateCreated)
                .Take(20)
                .ToList();
        }
        public async Task<List<EMMS.Models.Entities.Notification>> GetNotificationsByFacility(int? facilityId = null, int take = 5)
        {
            var query = _context.Notifications.AsNoTracking().OrderByDescending(n => n.DateCreated);

            if (facilityId.HasValue)
                query = (IOrderedQueryable<Models.Entities.Notification>)query.Where(n => n.FacilityId == facilityId.Value);

            return await query.Take(take).ToListAsync();
        }



        public async Task MarkAsReadAsync(int id)
        {
            var notification = await _context.Notifications.FirstOrDefaultAsync(n => n.Id == id);
            if (notification != null && !notification.IsRead)
            {
                notification.IsRead = true;
                _context.Notifications.Update(notification);
                await _context.SaveChangesAsync();
            }
        }
    }
}
