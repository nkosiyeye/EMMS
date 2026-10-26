using EMMS.Constants;
using EMMS.Models;
using EMMS.Service;
using static EMMS.Models.Enumerators;

namespace EMMS.Tests
{
    public class AssetStatusRulesTests
    {
        [Fact]
        public void JobOpened_PutsAssetUnderMaintenance()
        {
            Assert.Equal(FunctionalStatus.UnderMaintenance, AssetStatusTracker.OnJobOpened);
        }

        [Theory]
        [InlineData(WorkOutcomeNames.Successful, FunctionalStatus.Functional)]
        [InlineData(WorkOutcomeNames.Unsuccessful, FunctionalStatus.NonFunctional)]
        [InlineData(WorkOutcomeNames.Pending, null)]
        [InlineData(null, null)]
        public void StatusAfterClose(string? outcome, FunctionalStatus? expected)
        {
            Assert.Equal(expected, AssetStatusTracker.StatusAfterClose(outcome));
        }

        private static AssetStatusChange Opened(FunctionalStatus? from) =>
            new() { FromStatus = from, ToStatus = FunctionalStatus.UnderMaintenance, Source = StatusChangeSource.JobOpened };

        [Fact]
        public void StatusAfterCancel_RestoresStatusFromBeforeTheJob()
        {
            Assert.Equal(FunctionalStatus.NonFunctional,
                AssetStatusTracker.StatusAfterCancel(FunctionalStatus.UnderMaintenance, Opened(FunctionalStatus.NonFunctional)));
            Assert.Equal(FunctionalStatus.Unknown,
                AssetStatusTracker.StatusAfterCancel(FunctionalStatus.UnderMaintenance, Opened(null)));
        }

        [Fact]
        public void StatusAfterCancel_LeavesStatusAloneWhenChangedSinceOrNeverSet()
        {
            // e.g. a movement was received with a new status after the job opened
            Assert.Null(AssetStatusTracker.StatusAfterCancel(FunctionalStatus.Functional, Opened(FunctionalStatus.NonFunctional)));
            // the job never changed the status (it was already Under Maintenance)
            Assert.Null(AssetStatusTracker.StatusAfterCancel(FunctionalStatus.UnderMaintenance, null));
        }
    }
}
