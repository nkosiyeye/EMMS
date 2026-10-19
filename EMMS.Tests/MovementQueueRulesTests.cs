using EMMS.Models;
using EMMS.Service;
using static EMMS.Models.Enumerators;

namespace EMMS.Tests
{
    public class MovementQueueRulesTests
    {
        // A movable asset: active, received at facility 1, user at facility 1, empty queue
        private static QueueScanFacts Movable() => new()
        {
            Exists = true,
            RowState = RowStatus.Active,
            HasLocation = true,
            CurrentFacilityId = 1,
            UserFacilityId = 1,
        };

        [Fact]
        public void MovableAsset_IsAdded()
        {
            Assert.Equal(QueueScanResult.Added, MovementQueueService.Evaluate(Movable()));
        }

        // Each case breaks one rule on top of every LATER rule, so only correct ordering returns the expected code.
        public static IEnumerable<object[]> OrderingCases()
        {
            QueueScanFacts AllLaterBroken()
            {
                var f = Movable();
                f.InTransit = true;
                f.StolenMissing = true;
                f.Decommissioned = true;
                f.OpenWorkRequest = true;
                f.UserFacilityId = 2;              // not at your facility
                f.QueueSourceFacilityId = 3;       // mixed source
                f.AlreadyQueued = true;
                return f;
            }

            var notFound = AllLaterBroken(); notFound.Exists = false; notFound.RowState = RowStatus.Inactive; notFound.HasLocation = false;
            yield return new object[] { "NotFound", notFound, QueueScanResult.NotFound };

            var inactive = AllLaterBroken(); inactive.RowState = RowStatus.Inactive; inactive.HasLocation = false;
            yield return new object[] { "Inactive", inactive, QueueScanResult.Inactive };

            var noLocation = AllLaterBroken(); noLocation.HasLocation = false;
            yield return new object[] { "NoLocation", noLocation, QueueScanResult.NoLocation };

            yield return new object[] { "InTransit", AllLaterBroken(), QueueScanResult.InTransit };

            var stolen = AllLaterBroken(); stolen.InTransit = false;
            yield return new object[] { "StolenMissing", stolen, QueueScanResult.StolenMissing };

            var decommissioned = AllLaterBroken(); decommissioned.InTransit = false; decommissioned.StolenMissing = false;
            yield return new object[] { "Decommissioned", decommissioned, QueueScanResult.Decommissioned };

            var workRequest = AllLaterBroken(); workRequest.InTransit = false; workRequest.StolenMissing = false; workRequest.Decommissioned = false;
            yield return new object[] { "OpenWorkRequest", workRequest, QueueScanResult.OpenWorkRequest };

            var elsewhere = AllLaterBroken(); elsewhere.InTransit = false; elsewhere.StolenMissing = false; elsewhere.Decommissioned = false; elsewhere.OpenWorkRequest = false;
            yield return new object[] { "NotAtYourFacility", elsewhere, QueueScanResult.NotAtYourFacility };

            var mixed = AllLaterBroken(); mixed.InTransit = false; mixed.StolenMissing = false; mixed.Decommissioned = false; mixed.OpenWorkRequest = false; mixed.UserFacilityId = 1;
            yield return new object[] { "MixedSource", mixed, QueueScanResult.MixedSource };

            var queued = Movable(); queued.AlreadyQueued = true;
            yield return new object[] { "AlreadyQueued", queued, QueueScanResult.AlreadyQueued };
        }

        [Theory]
        [MemberData(nameof(OrderingCases))]
        public void FirstFailingCheckWins(string name, QueueScanFacts facts, QueueScanResult expected)
        {
            Assert.True(expected == MovementQueueService.Evaluate(facts), name);
        }

        [Fact]
        public void Admin_SkipsFacilityCheck_ButNotMixedSource()
        {
            var f = Movable();
            f.IsAdmin = true;
            f.UserFacilityId = 99;
            Assert.Equal(QueueScanResult.Added, MovementQueueService.Evaluate(f));

            f.QueueSourceFacilityId = 7;
            Assert.Equal(QueueScanResult.MixedSource, MovementQueueService.Evaluate(f));
        }

        [Fact]
        public void SameSourceAsQueue_IsAdded()
        {
            var f = Movable();
            f.QueueSourceFacilityId = 1;
            Assert.Equal(QueueScanResult.Added, MovementQueueService.Evaluate(f));
        }

        [Fact]
        public void Describe_DistinguishesOwnQueueFromAnotherUsers()
        {
            var f = Movable();
            f.AlreadyQueued = true;
            f.QueuedByCurrentUser = true;
            Assert.Contains("your queue", MovementQueueService.Describe(QueueScanResult.AlreadyQueued, "T-1", f));
            f.QueuedByCurrentUser = false;
            Assert.Contains("another user", MovementQueueService.Describe(QueueScanResult.AlreadyQueued, "T-1", f));
        }

        [Fact]
        public void ValidatePatient_RequiresAllFields()
        {
            Assert.Equal(4, MovementQueueService.ValidatePatient(null).Count);
            Assert.Equal(4, MovementQueueService.ValidatePatient(new HomeBasedCarePatient { Number = " " }).Count);
            Assert.Empty(MovementQueueService.ValidatePatient(new HomeBasedCarePatient { Number = "P1", Name = "A", Surname = "B", Address = "C" }));
        }
    }
}
