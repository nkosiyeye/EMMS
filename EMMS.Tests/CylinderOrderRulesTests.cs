using EMMS.Service;
using static EMMS.Models.Enumerators;

namespace EMMS.Tests
{
    public class CylinderOrderRulesTests
    {
        [Theory]
        [InlineData(CylinderOrderStatus.Ordered, 20, 0, 0, CylinderOrderStatus.Ordered)]
        [InlineData(CylinderOrderStatus.Ordered, 20, 12, 0, CylinderOrderStatus.Dispatched)]     // partial dispatch
        [InlineData(CylinderOrderStatus.Dispatched, 20, 20, 18, CylinderOrderStatus.Dispatched)] // 2 not yet received
        [InlineData(CylinderOrderStatus.Dispatched, 20, 20, 20, CylinderOrderStatus.Received)]   // every item received
        [InlineData(CylinderOrderStatus.Dispatched, 20, 19, 19, CylinderOrderStatus.Dispatched)] // one rejected: stays Dispatched
        [InlineData(CylinderOrderStatus.Modified, 20, 5, 5, CylinderOrderStatus.Modified)]       // waits for re-submit
        [InlineData(CylinderOrderStatus.PlantDown, 20, 0, 0, CylinderOrderStatus.PlantDown)]     // closed
        public void DeriveStatus(CylinderOrderStatus current, int qty, int dispatched, int received, CylinderOrderStatus expected)
        {
            Assert.Equal(expected, CylinderOrderService.DeriveStatus(current, qty, dispatched, received));
        }

        [Theory]
        [InlineData(CylinderOrderStatus.Ordered, 20, 0, true)]
        [InlineData(CylinderOrderStatus.Dispatched, 20, 19, true)]   // a rejected cylinder can be replaced
        [InlineData(CylinderOrderStatus.Dispatched, 20, 20, false)]
        [InlineData(CylinderOrderStatus.Modified, 20, 0, false)]     // re-submit first
        [InlineData(CylinderOrderStatus.PlantDown, 20, 0, false)]
        [InlineData(CylinderOrderStatus.Received, 20, 20, false)]
        public void CanDispatch(CylinderOrderStatus status, int qty, int dispatched, bool expected)
        {
            Assert.Equal(expected, CylinderOrderService.CanDispatch(status, qty, dispatched));
        }

        [Fact]
        public void OrderNumber_Format()
        {
            Assert.Equal("ORD-2026-0057", CylinderOrderService.FormatOrderNumber(2026, 57));
            Assert.Equal("ORD-2026-12345", CylinderOrderService.FormatOrderNumber(2026, 12345));
        }

        private static QueueScanFacts DispatchableAtCms() => new()
        {
            Exists = true,
            RowState = RowStatus.Active,
            HasLocation = true,
            CurrentFacilityId = 238,
            UserFacilityId = 238,
            DispatchMode = true,
            AtCentralStore = true,
            MatchesOrder = true,
            OrderHasRoom = true,
            OrderNumber = "ORD-2026-0001"
        };

        [Fact]
        public void Dispatch_MatchingCylinderAtCms_IsAdded()
        {
            Assert.Equal(QueueScanResult.Added, MovementQueueService.Evaluate(DispatchableAtCms()));
        }

        [Fact]
        public void Dispatch_CheckOrder_NotAtCentralStoreBeforeWrongItemBeforeMixedSource()
        {
            var f = DispatchableAtCms();
            f.IsAdmin = true;            // admins skip the facility check...
            f.UserFacilityId = 2;
            f.AtCentralStore = false;    // ...but not the central store check
            f.MatchesOrder = false;
            f.QueueSourceFacilityId = 1;
            Assert.Equal(QueueScanResult.NotAtCentralStore, MovementQueueService.Evaluate(f));

            f.AtCentralStore = true;
            Assert.Equal(QueueScanResult.WrongItem, MovementQueueService.Evaluate(f));

            f.MatchesOrder = true;
            Assert.Equal(QueueScanResult.MixedSource, MovementQueueService.Evaluate(f));
        }

        [Fact]
        public void Dispatch_AlreadyQueuedBeforeOrderFull()
        {
            var f = DispatchableAtCms();
            f.OrderHasRoom = false;
            f.AlreadyQueued = true;
            Assert.Equal(QueueScanResult.AlreadyQueued, MovementQueueService.Evaluate(f));

            f.AlreadyQueued = false;
            Assert.Equal(QueueScanResult.OrderFull, MovementQueueService.Evaluate(f));
        }

        [Fact]
        public void NormalMode_IgnoresDispatchFacts()
        {
            var f = DispatchableAtCms();
            f.DispatchMode = false;
            f.AtCentralStore = false;
            f.MatchesOrder = false;
            f.OrderHasRoom = false;
            Assert.Equal(QueueScanResult.Added, MovementQueueService.Evaluate(f));
        }

        [Theory]
        [InlineData(MovementReason.Dispatch, false, false, 1)]   // manual dispatch is not allowed
        [InlineData(MovementReason.Dispatch, false, true, 0)]    // from an order it is
        [InlineData(MovementReason.Return, false, false, 1)]     // return must go to the central store
        [InlineData(MovementReason.Return, true, false, 0)]
        [InlineData(MovementReason.RoutineTransfer, false, false, 0)]
        public void ValidateReason(MovementReason reason, bool toCentralStore, bool dispatch, int expectedErrors)
        {
            Assert.Equal(expectedErrors, MovementQueueService.ValidateReason(reason, toCentralStore, dispatch).Count);
        }
    }
}
