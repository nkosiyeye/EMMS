using EMMS.Service;
using static EMMS.Models.Enumerators;

namespace EMMS.Tests
{
    public class AssetListReportTests
    {
        private static readonly DateTime Now = new(2026, 2, 1, 9, 30, 0);

        [Fact]
        public void ReportTypes_CoverEveryReport_AndDontClashWithSummaryReports()
        {
            Assert.Equal(Enum.GetValues<AssetListReport>().OrderBy(r => r), AssetListReportService.ReportTypes.Values.OrderBy(r => r));
            Assert.Empty(AssetListReportService.ReportTypes.Keys.Intersect(AssetSummaryReportService.ReportTypes.Keys));
        }

        [Fact]
        public void FileName_WithoutDates_IsNamePlusTimestamp()
        {
            Assert.Equal("All_Assets_Report_20260201_093000.xlsx", AssetListReportService.FileName(AssetListReport.AllAssets, null, null, Now));
            Assert.Equal("Assets_Due_Service_Report_20260201_093000.xlsx", AssetListReportService.FileName(AssetListReport.DueService, null, null, Now));
        }

        [Fact]
        public void FileName_WithDates_IncludesTheRange_BlankForAMissingEnd()
        {
            Assert.Equal("All_Assets_Report_20260101_20260131_20260201_093000.xlsx",
                AssetListReportService.FileName(AssetListReport.AllAssets, new DateTime(2026, 1, 1), new DateTime(2026, 1, 31), Now));
            Assert.Equal("Assets_Due_Service_Report_20260101__20260201_093000.xlsx",
                AssetListReportService.FileName(AssetListReport.DueService, new DateTime(2026, 1, 1), null, Now));
        }
    }

    public class AssetRegistrationRulesTests
    {
        [Theory]
        [InlineData(ProcurementStatus.Decommissioned, 5, MovementReason.Decommission)]
        [InlineData(ProcurementStatus.New, null, MovementReason.Deployment)]
        [InlineData(ProcurementStatus.New, 5, MovementReason.Installation)]
        public void DeploymentReason_DecommissionedWins_ElseServicePointMeansInstallation(ProcurementStatus status, int? servicePointId, MovementReason expected)
        {
            Assert.Equal(expected, AssetRegistrationService.DeploymentReason((int)status, servicePointId));
        }

        [Theory]
        [InlineData(ProcurementStatus.Decommissioned, true, FunctionalStatus.NonFunctional)]
        [InlineData(ProcurementStatus.New, true, FunctionalStatus.Idle)]
        [InlineData(ProcurementStatus.New, false, FunctionalStatus.Functional)]
        public void DeploymentStatus_DecommissionedIsNonFunctional_ElseIdleOrFunctional(ProcurementStatus status, bool isIdle, FunctionalStatus expected)
        {
            Assert.Equal(expected, AssetRegistrationService.DeploymentStatus((int)status, isIdle));
        }
    }
}
