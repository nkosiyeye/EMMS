using EMMS.CustomAttributes;
using EMMS.Models;
using EMMS.Models.Admin;
using EMMS.Service;
using EMMS.ViewModels;
using static EMMS.Models.Enumerators;

namespace EMMS.Tests
{
    public class FacilityAccessRulesTests
    {
        private const int Mine = 1, Other = 2, Third = 3;

        private static User MakeUser(UserType type, int facilityId = Mine) => new()
        {
            UserId = Guid.NewGuid(),
            FacilityId = facilityId,
            UserRole = new UserRole { UserType = type },
        };

        private static readonly User Admin = MakeUser(UserType.Administrator, Third);
        private static readonly User Manager = MakeUser(UserType.FacilityManager);
        private static readonly User Biomed = MakeUser(UserType.Biomed);

        // ------------------------------------------------------------------ assets
        private static AssetPlacement At(int? current, int? from = null, int? to = null, int? creator = null, bool job = false) =>
            new(current, from, to, creator, job);

        [Theory]
        [InlineData(FacilityAccessLevel.View)]
        [InlineData(FacilityAccessLevel.Change)]
        public void Asset_AtMyFacility_Reachable_AtAnotherNot(FacilityAccessLevel level)
        {
            Assert.True(FacilityAccess.CanAccessAsset(Biomed, At(Mine), level));
            Assert.False(FacilityAccess.CanAccessAsset(Biomed, At(Other), level));
            Assert.True(FacilityAccess.CanAccessAsset(Admin, At(Other), level));
        }

        [Fact]
        public void Asset_InTransitToMe_CanBeSeenButNotChanged()
        {
            var incoming = At(Other, from: Other, to: Mine);
            Assert.True(FacilityAccess.CanAccessAsset(Biomed, incoming, FacilityAccessLevel.View));
            Assert.False(FacilityAccess.CanAccessAsset(Biomed, incoming, FacilityAccessLevel.Change));
        }

        [Fact]
        public void Asset_NotYetPlaced_BelongsToTheRegisteringFacility()
        {
            Assert.True(FacilityAccess.CanAccessAsset(Biomed, At(null, creator: Mine), FacilityAccessLevel.Change));
            Assert.False(FacilityAccess.CanAccessAsset(Biomed, At(null, creator: Other), FacilityAccessLevel.View));
            // Once placed, the location decides, not who registered it
            Assert.False(FacilityAccess.CanAccessAsset(Biomed, At(Other, creator: Mine), FacilityAccessLevel.View));
        }

        [Fact]
        public void Asset_WithMyOpenJob_CanBeSeenFromAnotherFacility()
        {
            Assert.True(FacilityAccess.CanAccessAsset(Biomed, At(Other, job: true), FacilityAccessLevel.View));
            Assert.False(FacilityAccess.CanAccessAsset(Biomed, At(Other, job: true), FacilityAccessLevel.Change));
        }

        // ------------------------------------------------------------------ movements
        private static MovementFacts Move(int from, int to, bool approved = false, bool received = false, bool rejected = false) =>
            new(from, to, approved, received, rejected);

        [Fact]
        public void Movement_View_EitherEnd()
        {
            Assert.True(FacilityAccess.CanAccessMovement(Biomed, Move(Mine, Other), FacilityAccessLevel.View));
            Assert.True(FacilityAccess.CanAccessMovement(Biomed, Move(Other, Mine), FacilityAccessLevel.View));
            Assert.False(FacilityAccess.CanAccessMovement(Biomed, Move(Other, Third), FacilityAccessLevel.View));
        }

        [Fact]
        public void Movement_Change_SenderOnly_UntilApproved()
        {
            Assert.True(FacilityAccess.CanAccessMovement(Biomed, Move(Mine, Other), FacilityAccessLevel.Change));
            Assert.False(FacilityAccess.CanAccessMovement(Biomed, Move(Other, Mine), FacilityAccessLevel.Change));
            Assert.False(FacilityAccess.CanAccessMovement(Biomed, Move(Mine, Other, approved: true), FacilityAccessLevel.Change));
            Assert.False(FacilityAccess.CanAccessMovement(Admin, Move(Mine, Other, approved: true, received: true), FacilityAccessLevel.Change));
        }

        [Fact]
        public void Movement_Approve_AdminOrManagerAtEitherEnd_WhilePending()
        {
            Assert.True(FacilityAccess.CanAccessMovement(Manager, Move(Mine, Other), FacilityAccessLevel.Approve));
            Assert.True(FacilityAccess.CanAccessMovement(Manager, Move(Other, Mine), FacilityAccessLevel.Approve));
            Assert.False(FacilityAccess.CanAccessMovement(Manager, Move(Other, Third), FacilityAccessLevel.Approve));
            Assert.False(FacilityAccess.CanAccessMovement(Biomed, Move(Mine, Other), FacilityAccessLevel.Approve));   // not a manager
            Assert.True(FacilityAccess.CanAccessMovement(Admin, Move(Other, Mine), FacilityAccessLevel.Approve));
            Assert.False(FacilityAccess.CanAccessMovement(Admin, Move(Other, Mine, approved: true), FacilityAccessLevel.Approve));
            Assert.False(FacilityAccess.CanAccessMovement(Admin, Move(Other, Mine, rejected: true), FacilityAccessLevel.Approve));
        }

        [Fact]
        public void Movement_Receive_DestinationOnly_OnceApproved()
        {
            Assert.True(FacilityAccess.CanAccessMovement(Biomed, Move(Other, Mine, approved: true), FacilityAccessLevel.Receive));
            Assert.False(FacilityAccess.CanAccessMovement(Biomed, Move(Mine, Other, approved: true), FacilityAccessLevel.Receive));
            Assert.False(FacilityAccess.CanAccessMovement(Biomed, Move(Other, Mine), FacilityAccessLevel.Receive));           // not approved
            Assert.False(FacilityAccess.CanAccessMovement(Biomed, Move(Other, Mine, approved: true, received: true), FacilityAccessLevel.Receive));
        }

        [Fact]
        public void Batch_ReachableThroughAnyOfItsMovements()
        {
            var batch = new[] { Move(Other, Mine), Move(Other, Mine, rejected: true) };
            Assert.True(FacilityAccess.CanAccessBatch(Biomed, batch, FacilityAccessLevel.View));
            Assert.False(FacilityAccess.CanAccessBatch(Biomed, batch, FacilityAccessLevel.Approve));
            Assert.True(FacilityAccess.CanAccessBatch(Manager, batch, FacilityAccessLevel.Approve));
            Assert.False(FacilityAccess.CanAccessBatch(MakeUser(UserType.FacilityManager, Third), batch, FacilityAccessLevel.Approve));
        }

        // ------------------------------------------------------------------ users
        [Fact]
        public void User_SelfOrAdmin()
        {
            Assert.True(FacilityAccess.CanAccessUser(Biomed, Biomed.UserId));
            Assert.False(FacilityAccess.CanAccessUser(Manager, Biomed.UserId));
            Assert.True(FacilityAccess.CanAccessUser(Admin, Biomed.UserId));
        }

        // ------------------------------------------------------------------ attribute
        [Fact]
        public void Resolve_FollowsArgumentPaths()
        {
            var movementId = Guid.NewGuid();
            var args = new Dictionary<string, object?>
            {
                ["id"] = movementId,
                ["model"] = new MoveRequestViewModel { MoveAsset = new MoveAsset { MovementId = movementId } },
                ["empty"] = new MoveRequestViewModel(),
            };
            Assert.Equal(movementId, FacilityScopedAttribute.Resolve(args, "id"));
            Assert.Equal(movementId, FacilityScopedAttribute.Resolve(args, "model.MoveAsset.MovementId"));
            Assert.Equal(movementId, FacilityScopedAttribute.Resolve(args, "model.moveAsset.movementId"));   // case-insensitive like binding
            Assert.Null(FacilityScopedAttribute.Resolve(args, "empty.MoveAsset.MovementId"));
            Assert.Null(FacilityScopedAttribute.Resolve(args, "missing"));
        }
    }
}
