using EMMS.Models.Entities;
using EMMS.Models.Domain;
using Microsoft.EntityFrameworkCore;
using EMMS.Data;
using EMMS.Models;
using EMMS.Models.Admin;
using static EMMS.Models.Enumerators;
using DocumentFormat.OpenXml.Spreadsheet;

namespace EMMS.Service
{
    public interface INotificationService
    {
        Task CreateMovementRequestNotification(int facilityId, Guid createdBy);
        Task CreateMovementApprovalNotification(int facilityId, Guid createdBy);
        Task CreateBatchMovementRequestNotification(int facilityId, int count, Guid createdBy);
        Task CreateBatchMovementApprovalNotification(int facilityId, int count, Guid createdBy);
        Task CreateCylinderRequestNotification(int facilityId, Guid createdBy);
        Task CreateCylinderRequestApprovedNotification(Guid createdBy);
        Task CreateUserNotification(Guid? userId, string message, string type, Guid createdBy);
        Task CreateBatchReceiptDiscrepancyNotification(int sourceFacilityId, int missing, Guid createdBy);
        Task CreateWorkRequestNotification(int facilityId, Guid createdBy);
        Task CreateJobAssignmentNotification(Guid? biomedUserId, Guid? requestorUserId, Guid createdBy);
        Task CreateJobClaimedNotification(Guid? biomedUserId, Guid? requestorUserId, Guid createdBy);
        Task CreateJobCompletedNotification(Guid? requestorUserId, Guid createdBy);
        Task CreateServiceRequestsRaisedNotification(int facilityId, int count, Guid createdBy);
        Task<bool> CreateServiceDueReminderAsync(int facilityId, string message, DateTime today);
        Task<List<Models.Entities.Notification>> GetNotificationsAsync(User currentUser, UserType role);
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

        // Movement Queue batch submitted: one notification per approver for the whole batch
        public async Task CreateBatchMovementRequestNotification(int facilityId, int count, Guid createdBy)
        {
            var message = $"A batch of {count} asset movement(s) has been submitted and requires approval.";
            await NotifyFacilityManagersAndAdmins(facilityId, message, "approval", createdBy);
        }

        // Batch approved: one notification per receiving stakeholder for the whole batch
        public async Task CreateBatchMovementApprovalNotification(int facilityId, int count, Guid createdBy)
        {
            var message = $"A batch of {count} asset(s) is on its way and requires reception.";
            await NotifyFacilityStakeholders(facilityId, message, "reception", createdBy);
        }

        // Cylinder request submitted: the facility's pharmacists approve it
        public async Task CreateCylinderRequestNotification(int facilityId, Guid createdBy)
        {
            var pharmacists = await _context.User.Include(u => u.UserRole)
                .Where(u => u.FacilityId == facilityId && u.UserRole.UserType == Enumerators.UserType.Pharmacist && u.RowState == RowStatus.Active)
                .ToListAsync();
            foreach (var user in pharmacists)
                await AddNotificationAsync("A cylinder request is waiting for your approval.", "cylinder", facilityId, user.UserId, createdBy);
        }

        // Cylinder request approved: CMS officers (central store) and admins create the order
        public async Task CreateCylinderRequestApprovedNotification(Guid createdBy)
        {
            var recipients = await _context.User.Include(u => u.UserRole).Include(u => u.Facility)
                .Where(u => u.RowState == RowStatus.Active &&
                            (u.UserRole.UserType == Enumerators.UserType.Administrator ||
                             (u.UserRole.UserType == Enumerators.UserType.CmsOfficer && u.Facility!.IsCentralStore)))
                .ToListAsync();
            foreach (var user in recipients)
                await AddNotificationAsync("An approved cylinder request is ready to be ordered.", "cylinder", null, user.UserId, createdBy);
        }

        // Direct message to one user (e.g. the requester when their request is rejected or returned)
        public async Task CreateUserNotification(Guid? userId, string message, string type, Guid createdBy)
        {
            if (userId == null) return;
            await AddNotificationAsync(message, type, null, userId, createdBy);
        }

        // Batch received with unscanned items: the sending facility follows up
        public async Task CreateBatchReceiptDiscrepancyNotification(int sourceFacilityId, int missing, Guid createdBy)
        {
            var message = $"{missing} asset(s) in a dispatched batch were not scanned at receipt and need follow-up.";
            await NotifyFacilityManagersAndAdmins(sourceFacilityId, message, "reception", createdBy);
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
        public async Task ForgetPasswordNotification(User user)
        {
            await NotifyAdmins($"Password reset requested for user: {user.FirstName} {user.LastName}","forgotPassword",user.UserId);
        }

        public async Task UserRegistrationNotification(User user)
        {
            await NotifyAdmins($"New user has been Registered: {user.FirstName} {user.LastName}", "userRegistration", user.UserId);
        }

        // Service requests raised in bulk from the service schedule: one summary instead of one per request
        public async Task CreateServiceRequestsRaisedNotification(int facilityId, int count, Guid createdBy)
        {
            var message = count == 1 ? "A service request has been raised." : $"{count} service requests have been raised.";
            await NotifyBiomedAndAdmins(facilityId, message, "work", createdBy);
        }

        // Daily service reminder for a facility's Biomeds and managers. Sent at most once per facility per day;
        // false when one was already sent today or the facility has no one to tell. Created by the system, so CreatedBy is empty.
        public async Task<bool> CreateServiceDueReminderAsync(int facilityId, string message, DateTime today)
        {
            var sent = await _context.Notifications.AnyAsync(n =>
                n.Type == ServiceType && n.FacilityId == facilityId && n.DateCreated >= today.Date);
            if (sent) return false;

            return await NotifyFacilityStakeholders(facilityId, message, ServiceType, Guid.Empty) > 0;
        }

        public const string ServiceType = "service";

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

        /// <returns>How many users were notified.</returns>
        private async Task<int> NotifyFacilityStakeholders(int facilityId, string message, string type, Guid createdBy)
        {
            var stakeholders = await _context.User.Include(u => u.UserRole).Where(u => u.FacilityId == facilityId && (u.UserRole.UserType == Enumerators.UserType.Biomed || u.UserRole.UserType == Enumerators.UserType.FacilityManager || u.UserRole.UserType == Enumerators.UserType.Administrator)).ToListAsync();

            foreach (var user in stakeholders)
            {
                await AddNotificationAsync(message, type, facilityId, user.UserId, createdBy);
            }
            return stakeholders.Count;
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
        // The notifications a user may see; also the ones they may mark as read
        private static IQueryable<Models.Entities.Notification> VisibleTo(IQueryable<Models.Entities.Notification> query, User currentUser, UserType role)
        {
            switch (role)
            {
                case UserType.Administrator:
                    // Admin sees all, no filter
                    return query;

                case UserType.FacilityManager:
                    return query.Where(n => n.FacilityId == currentUser.FacilityId);

                case UserType.Biomed:
                    return query.Where(n => (n.FacilityId == currentUser.FacilityId || n.UserId == currentUser.UserId) &&
                                             (n.Type == "work" || n.Type == "reception" || n.Type == ServiceType || (n.Type == "job" && n.UserId == currentUser.UserId)));

                default:
                    return query.Where(n => n.UserId == currentUser.UserId);
            }
        }

        public async Task<List<Models.Entities.Notification>> GetNotificationsAsync(User currentUser, UserType role)
        {
            var query = VisibleTo(_context.Notifications.Where(n => n.RowState == RowStatus.Active), currentUser, role)
                .Include(n => n.Facility)
                .OrderByDescending(n => n.DateCreated);

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



        // Returns false when the notification doesn't exist or isn't visible to this user
        public async Task<bool> MarkAsReadAsync(int id, User currentUser)
        {
            var notification = await VisibleTo(_context.Notifications, currentUser, currentUser.UserRole.UserType)
                .FirstOrDefaultAsync(n => n.Id == id);
            if (notification == null)
                return false;

            if (!notification.IsRead)
            {
                notification.IsRead = true;
                _context.Notifications.Update(notification);
                await _context.SaveChangesAsync();
            }
            return true;
        }
    }
}
