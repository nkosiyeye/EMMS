using EMMS.Data;
using EMMS.Models;
using EMMS.Models.Admin;
using Microsoft.EntityFrameworkCore;
using static EMMS.Models.Enumerators;

namespace EMMS.Service
{
    /// <summary>The kinds of record an action can be scoped to (see <see cref="CustomAttributes.FacilityScopedAttribute"/>).</summary>
    public enum FacilityRecord { Asset, Movement, Batch, WorkRequest, Job, User }

    /// <summary>What the action does with the record; changing needs more than looking.</summary>
    public enum FacilityAccessLevel { View, Change, Approve, Receive }

    public enum AccessResult { Allowed, Denied, Missing }

    /// <summary>Where an asset is, for the facility rules. Ids are facility ids.</summary>
    public sealed record AssetPlacement(int? CurrentFacilityId, int? PendingFromId, int? PendingToId, int? CreatorFacilityId, bool HasOpenJobForUser);

    /// <summary>A movement (or every movement of a batch) for the facility rules.</summary>
    public sealed record MovementFacts(int FromId, int ToId, bool IsApproved, bool IsReceived, bool IsRejected)
    {
        public bool IsPending => !IsReceived && !IsRejected;
    }

    /// <summary>
    /// The one place that decides whether a user may see or change another facility's record. Admins reach every
    /// facility; everyone else reaches records at, or travelling to/from, their own. Controllers apply it with
    /// [FacilityScoped]; list queries are scoped by their services. EMMS.Tests.FacilityScopeCoverageTests fails when
    /// an action declares neither, so a new action cannot quietly skip the check.
    /// </summary>
    public class FacilityAccess
    {
        private readonly ApplicationDbContext _context;

        public FacilityAccess(ApplicationDbContext context)
        {
            _context = context;
        }

        // ------------------------------------------------------------------------------------------
        // Rules (pure, unit-tested)
        // ------------------------------------------------------------------------------------------
        public static bool IsAdmin(User u) => u.UserRole?.UserType == UserType.Administrator;
        private static bool IsManager(User u) => u.UserRole?.UserType == UserType.FacilityManager;

        public static bool CanAccessFacility(User u, int? facilityId) => IsAdmin(u) || (facilityId != null && u.FacilityId == facilityId);

        /// <summary>
        /// View: the asset is at, or travelling to/from, the user's facility; not yet placed anywhere and registered by
        /// their facility; or it has a job assigned to them. Change: only where it is now (or, unplaced, its registering facility).
        /// </summary>
        public static bool CanAccessAsset(User u, AssetPlacement p, FacilityAccessLevel level)
        {
            if (IsAdmin(u)) return true;
            var mine = p.CurrentFacilityId != null
                ? p.CurrentFacilityId == u.FacilityId
                : p.CreatorFacilityId == u.FacilityId;
            if (level != FacilityAccessLevel.View) return mine;
            return mine || p.PendingFromId == u.FacilityId || p.PendingToId == u.FacilityId || p.HasOpenJobForUser;
        }

        /// <summary>
        /// View: either end. Change (edit/delete): the sending facility, until approved. Approve: an admin, or a facility
        /// manager at either end, while it awaits approval. Receive (receive/reject): the destination, once approved.
        /// </summary>
        public static bool CanAccessMovement(User u, MovementFacts m, FacilityAccessLevel level)
        {
            bool from = IsAdmin(u) || m.FromId == u.FacilityId;
            bool to = IsAdmin(u) || m.ToId == u.FacilityId;
            return level switch
            {
                FacilityAccessLevel.View => from || to,
                FacilityAccessLevel.Change => from && !m.IsApproved && m.IsPending,
                FacilityAccessLevel.Approve => (IsAdmin(u) || (IsManager(u) && (from || to))) && !m.IsApproved && m.IsPending,
                FacilityAccessLevel.Receive => to && m.IsApproved && m.IsPending,
                _ => false,
            };
        }

        /// <summary>A batch is reachable when any of its movements is (one batch has one source and one destination).</summary>
        public static bool CanAccessBatch(User u, IReadOnlyCollection<MovementFacts> movements, FacilityAccessLevel level) =>
            level == FacilityAccessLevel.Approve
                ? (IsAdmin(u) || IsManager(u)) && movements.Any(m => CanAccessMovement(u, m, FacilityAccessLevel.View))
                : movements.Any(m => CanAccessMovement(u, m, level));

        /// <summary>User accounts: admins manage everyone; anyone else only sees and edits their own profile.</summary>
        public static bool CanAccessUser(User u, Guid userId) => IsAdmin(u) || u.UserId == userId;

        // ------------------------------------------------------------------------------------------
        // Checks by id
        // ------------------------------------------------------------------------------------------
        public async Task<AccessResult> CheckAsync(User user, FacilityRecord record, object id, FacilityAccessLevel level)
        {
            switch (record)
            {
                case FacilityRecord.Asset:
                    var placement = await AssetPlacementAsync((Guid)id, user.UserId);
                    return placement == null ? AccessResult.Missing : Result(CanAccessAsset(user, placement, level));

                case FacilityRecord.Movement:
                    var movement = await MovementsAsync(m => m.MovementId == (Guid)id);
                    return movement.Count == 0 ? AccessResult.Missing : Result(CanAccessMovement(user, movement[0], level));

                case FacilityRecord.Batch:
                    var batch = await MovementsAsync(m => m.BatchId == (Guid)id);
                    return batch.Count == 0 ? AccessResult.Missing : Result(CanAccessBatch(user, batch, level));

                case FacilityRecord.WorkRequest:
                    var guid = (Guid)id;
                    var facility = await _context.WorkRequest.Where(w => w.WorkRequestId == guid).Select(w => (int?)w.FacilityId).FirstOrDefaultAsync()
                                ?? await _context.InfrustructureWorkRequest.Where(w => w.WorkRequestId == guid).Select(w => (int?)w.FacilityId).FirstOrDefaultAsync();
                    return facility == null ? AccessResult.Missing : Result(CanAccessFacility(user, facility));

                case FacilityRecord.Job:
                    var job = await _context.Job.AsNoTracking().FirstOrDefaultAsync(j => j.JobId == (Guid)id);
                    return job == null ? AccessResult.Missing : Result(WorkRequestService.CanViewJob(user, job));

                case FacilityRecord.User:
                    return Result(CanAccessUser(user, (Guid)id));

                default:
                    return AccessResult.Denied;
            }
        }

        private static AccessResult Result(bool allowed) => allowed ? AccessResult.Allowed : AccessResult.Denied;

        private Task<AssetPlacement?> AssetPlacementAsync(Guid assetId, Guid userId) =>
            _context.Assets.AsNoTracking()
                .Where(a => a.AssetId == assetId && a.RowState != RowStatus.Deleted)
                .Select(a => new AssetPlacement(
                    _context.AssetCurrentLocations.Where(l => l.AssetId == a.AssetId).Select(l => (int?)l.FacilityId).FirstOrDefault(),
                    _context.AssetsInTransit.Where(t => t.AssetId == a.AssetId).Select(t => (int?)t.FromId).FirstOrDefault(),
                    _context.AssetsInTransit.Where(t => t.AssetId == a.AssetId).Select(t => (int?)t.FacilityId).FirstOrDefault(),
                    _context.User.Where(u => u.UserId == a.CreatedBy).Select(u => (int?)u.FacilityId).FirstOrDefault(),
                    _context.Job.Any(j => j.AssetId == a.AssetId && j.AssignedTo == userId && j.EndDate == null)))
                .FirstOrDefaultAsync();

        private Task<List<MovementFacts>> MovementsAsync(System.Linq.Expressions.Expression<Func<MoveAsset, bool>> which) =>
            _context.AssetMovement.AsNoTracking()
                .Where(m => m.RowState == RowStatus.Active)
                .Where(which)
                .Select(m => new MovementFacts(m.FromId, m.FacilityId, m.IsApproved, m.DateReceived != null, m.DateRejected != null))
                .ToListAsync();

        // ------------------------------------------------------------------------------------------
        // Scoped sets
        // ------------------------------------------------------------------------------------------

        /// <summary>Assets the user may view, for actions that take a list (e.g. stickers by tag).</summary>
        public IQueryable<Asset> VisibleAssets(User user)
        {
            if (IsAdmin(user)) return _context.Assets;
            var fid = user.FacilityId;
            return _context.Assets.Where(a =>
                _context.AssetCurrentLocations.Any(l => l.AssetId == a.AssetId && l.FacilityId == fid)
                || _context.AssetsInTransit.Any(t => t.AssetId == a.AssetId && (t.FromId == fid || t.FacilityId == fid))
                || (!_context.AssetCurrentLocations.Any(l => l.AssetId == a.AssetId)
                    && _context.User.Any(u => u.UserId == a.CreatedBy && u.FacilityId == fid)));
        }
    }
}
