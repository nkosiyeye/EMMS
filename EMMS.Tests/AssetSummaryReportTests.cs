using EMMS.Service;
using static EMMS.Models.Enumerators;

namespace EMMS.Tests
{
    public class AssetSummaryReportTests
    {
        private static AssetReportRow Row(string tag, FunctionalStatus status = FunctionalStatus.Functional,
            ProcurementStatus procurement = ProcurementStatus.New, MovementReason? reason = MovementReason.Deployment,
            string? facility = "Mbabane", string? servicePoint = "OPD", bool located = true) => new()
        {
            Tag = tag, FunctionalStatus = status, ProcurementStatus = procurement, Reason = located ? reason : null,
            Facility = located ? facility : null, ServicePoint = located ? servicePoint : null, HasLocation = located,
        };

        private static Dictionary<string, int> Counts(List<ReportGroup> groups) => groups.ToDictionary(g => g.Name, g => g.Rows.Count);

        [Fact]
        public void ReportTypes_CoverEveryReport()
        {
            Assert.Equal(Enum.GetValues<AssetSummaryReport>().OrderBy(r => r), AssetSummaryReportService.ReportTypes.Values.OrderBy(r => r));
        }

        [Fact]
        public void FunctionalStatus_OnlyLocatedAssets_InStatusOrder_UnknownLast()
        {
            var rows = new[]
            {
                Row("A", FunctionalStatus.Unknown), Row("B", FunctionalStatus.NonFunctional), Row("C"), Row("D"),
                Row("E", located: false),   // never deployed: the old chart skipped these
            };

            var groups = AssetSummaryReportService.Group(AssetSummaryReport.FunctionalStatus, rows);

            Assert.Equal(new[] { "Functional", "Non-functional", "Unknown" }, groups.Select(g => g.Name));
            Assert.Equal(2, groups[0].Rows.Count);
            Assert.DoesNotContain(groups.SelectMany(g => g.Rows), r => r.Tag == "E");
        }

        [Fact]
        public void ProcurementStatus_IncludesUndeployedAssets_InEnumOrder()
        {
            var rows = new[] { Row("A", procurement: ProcurementStatus.Used), Row("B", located: false), Row("C", procurement: ProcurementStatus.Decommissioned) };

            var groups = AssetSummaryReportService.Group(AssetSummaryReport.ProcurementStatus, rows);

            Assert.Equal(new[] { "New", "Used", "Decommissioned" }, groups.Select(g => g.Name));
        }

        [Fact]
        public void MovementReason_LargestFirst_FriendlyLabels()
        {
            var rows = new[]
            {
                Row("A", reason: MovementReason.StolenorMissing),
                Row("B", reason: MovementReason.RoutineTransfer), Row("C", reason: MovementReason.RoutineTransfer),
            };

            var groups = AssetSummaryReportService.Group(AssetSummaryReport.MovementReason, rows);

            Assert.Equal(new[] { "Routine transfer", "Stolen or missing" }, groups.Select(g => g.Name));
        }

        [Fact]
        public void NonFunctionalByFacility_NonFunctionalOrDecommissioned_ByFacility()
        {
            var rows = new[]
            {
                Row("A", FunctionalStatus.NonFunctional, facility: "Hlathikhulu"),
                Row("B", reason: MovementReason.Decommission, facility: "Mbabane"),
                Row("C", FunctionalStatus.NonFunctional, facility: "Mbabane"),
                Row("D", facility: "Mbabane"),                                 // functional: excluded
                Row("E", FunctionalStatus.UnderMaintenance, facility: "Mbabane"), // the old chart did not count these
            };

            var groups = AssetSummaryReportService.Group(AssetSummaryReport.NonFunctionalByFacility, rows);

            Assert.Equal(new Dictionary<string, int> { ["Mbabane"] = 2, ["Hlathikhulu"] = 1 }, Counts(groups));
            Assert.Equal("Mbabane", groups[0].Name);
        }

        [Fact]
        public void AssetsByServicePoint_SkipsAssetsWithoutAServicePoint()
        {
            var rows = new[] { Row("A", servicePoint: "ICU"), Row("B", servicePoint: "OPD"), Row("C", servicePoint: "OPD"), Row("D", servicePoint: null) };

            var groups = AssetSummaryReportService.Group(AssetSummaryReport.AssetsByServicePoint, rows);

            Assert.Equal(new Dictionary<string, int> { ["OPD"] = 2, ["ICU"] = 1 }, Counts(groups));
        }

        [Fact]
        public void Group_NoRows_NoGroups()
        {
            foreach (var report in Enum.GetValues<AssetSummaryReport>())
                Assert.Empty(AssetSummaryReportService.Group(report, Array.Empty<AssetReportRow>()));
        }

        [Fact]
        public void Group_RowsSortedByFacilityThenTag()
        {
            var rows = new[] { Row("B2", facility: "Mbabane"), Row("A9", facility: "Mbabane"), Row("Z1", facility: "Hlathikhulu") };

            var group = Assert.Single(AssetSummaryReportService.Group(AssetSummaryReport.FunctionalStatus, rows));

            Assert.Equal(new[] { "Z1", "A9", "B2" }, group.Rows.Select(r => r.Tag));
        }

        [Theory]
        [InlineData("OPD", "OPD")]
        [InlineData("Lab: Bay [2] / A*?", "Lab  Bay  2    A")]
        [InlineData("   ", "Sheet")]
        [InlineData("'Quoted'", "Quoted")]
        [InlineData("Mbabane Government Hospital - Main Wing", "Mbabane Government Hospital - M")]
        public void SheetName_IsValidForExcel(string input, string expected)
        {
            Assert.Equal(expected, AssetSummaryReportService.SheetName(input, new HashSet<string>(StringComparer.OrdinalIgnoreCase)));
        }

        [Fact]
        public void SheetName_DeduplicatesCaseInsensitivelyWithinTheLimit()
        {
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var longName = new string('x', 40);

            Assert.Equal("Summary", AssetSummaryReportService.SheetName("Summary", used));
            Assert.Equal("summary (2)", AssetSummaryReportService.SheetName("summary", used));
            var first = AssetSummaryReportService.SheetName(longName, used);
            var second = AssetSummaryReportService.SheetName(longName, used);

            Assert.Equal(31, first.Length);
            Assert.Equal(31, second.Length);
            Assert.EndsWith(" (2)", second);
        }

        [Fact]
        public void FileName_NamesReportAndScope()
        {
            var now = new DateTime(2026, 10, 21, 14, 5, 9);
            Assert.Equal("Functional_Status_All_Facilities_20261021_140509.xlsx",
                AssetSummaryReportService.FileName(AssetSummaryReport.FunctionalStatus, null, now));
            Assert.Equal("Assets_by_Service_Point_Mbabane_Government_Hospital_20261021_140509.xlsx",
                AssetSummaryReportService.FileName(AssetSummaryReport.AssetsByServicePoint, "Mbabane Government Hospital", now));
        }
    }
}
