using EMMS.Constants;
using EMMS.Data;
using EMMS.Models;
using Microsoft.EntityFrameworkCore;
using static EMMS.Models.Enumerators;

namespace EMMS.Service
{
    /// <summary>
    /// The only writer of <see cref="Asset.FunctionalStatus"/>. Each change adds an <see cref="AssetStatusChange"/> row
    /// in the same save. Movements are applied automatically by <see cref="ApplicationDbContext"/> when received;
    /// the job workflow calls <see cref="Change"/> directly.
    /// </summary>
    public static class AssetStatusTracker
    {
        // Rules (pure, unit-tested)
        public const FunctionalStatus OnJobOpened = FunctionalStatus.UnderMaintenance;

        public static FunctionalStatus? StatusAfterClose(string? outcomeName) => outcomeName switch
        {
            WorkOutcomeNames.Successful => FunctionalStatus.Functional,
            WorkOutcomeNames.Unsuccessful => FunctionalStatus.NonFunctional,
            _ => null
        };

        // Cancelling a job puts back the status it found, unless something else has changed the asset since.
        public static FunctionalStatus? StatusAfterCancel(FunctionalStatus current, AssetStatusChange? jobOpened) =>
            jobOpened != null && current == jobOpened.ToStatus ? jobOpened.FromStatus ?? FunctionalStatus.Unknown : null;

        /// <summary>Sets the status and logs it; does nothing when it is unchanged. Saved by the caller's SaveChanges.</summary>
        public static bool Change(ApplicationDbContext context, Asset asset, FunctionalStatus to, StatusChangeSource source,
            Guid? changedBy, Guid? jobId = null, Guid? movementId = null)
        {
            context.AllowStatusWrite(asset.AssetId);
            if (asset.FunctionalStatus == to) return false;

            context.AssetStatusChanges.Add(new AssetStatusChange
            {
                AssetStatusChangeId = Guid.NewGuid(),
                AssetId = asset.AssetId,
                FromStatus = context.Entry(asset).State == EntityState.Added ? null : asset.FunctionalStatus,
                ToStatus = to,
                Source = source,
                JobId = jobId,
                MovementId = movementId,
                ChangedBy = changedBy,
                DateChanged = DateTime.Now,
            });
            asset.FunctionalStatus = to;
            return true;
        }

        public static Task<AssetStatusChange?> GetJobOpenedAsync(ApplicationDbContext context, Guid jobId) =>
            context.AssetStatusChanges.AsNoTracking()
                .Where(c => c.JobId == jobId && c.Source == StatusChangeSource.JobOpened)
                .OrderByDescending(c => c.DateChanged)
                .FirstOrDefaultAsync();

        public static Task<List<AssetStatusChange>> GetHistoryAsync(ApplicationDbContext context, Guid assetId) =>
            context.AssetStatusChanges.AsNoTracking()
                .Include(c => c.ChangedByUser)
                .Include(c => c.Job)
                .Where(c => c.AssetId == assetId)
                .OrderByDescending(c => c.DateChanged)
                .ToListAsync();
    }
}
