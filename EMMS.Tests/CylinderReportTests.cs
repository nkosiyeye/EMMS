using EMMS.Service;
using EMMS.ViewModels;
using static EMMS.Models.Enumerators;

namespace EMMS.Tests
{
    public class CylinderReportTests
    {
        private static readonly DateTime Today = new(2026, 10, 10);

        // 2 sizes x 2 contents = 4 combinations in fixed order
        private static readonly List<StockColumn> Combos = new()
        {
            new() { Key = "1-10", Label = "10 L Oxygen", Slot = 0 },
            new() { Key = "1-11", Label = "10 L Medical Air", Slot = 1 },
            new() { Key = "2-10", Label = "47 L Oxygen", Slot = 2 },
            new() { Key = "2-11", Label = "47 L Medical Air", Slot = 3 },
        };

        private static CylinderStockRow At(int facility, string name, int size = 1, int content = 10, bool cms = false) => new()
        {
            SizeId = size, ContentId = content, FacilityId = facility, FacilityName = name, AtCentralStore = cms,
            Size = size == 1 ? "10 L" : "47 L", Content = content == 10 ? "Oxygen" : "Medical Air", Since = Today.AddDays(-1)
        };

        private static List<CylinderStockRow> Sample() => new()
        {
            At(238, "CMS", cms: true), At(238, "CMS", cms: true), At(238, "CMS", size: 2, cms: true),
            At(2, "Mbabane"), At(2, "Mbabane", content: 11),
            // with a patient for 45 days and for 3 days
            new() { SizeId = 1, ContentId = 10, Size = "10 L", Content = "Oxygen", FacilityId = 2, FacilityName = "Mbabane", ServicePoint = "Home Based Care", WithPatient = true, PatientNumber = "P-1", Since = Today.AddDays(-45), Tag = "T-OLD" },
            new() { SizeId = 1, ContentId = 10, FacilityId = 2, FacilityName = "Mbabane", WithPatient = true, Since = Today.AddDays(-3), Tag = "T-NEW" },
            // in transit CMS -> Nhlangano: location is still CMS, shown as incoming at Nhlangano only
            new() { SizeId = 1, ContentId = 10, FacilityId = 238, FacilityName = "CMS", AtCentralStore = true, InTransitToFacilityId = 1, InTransitTo = "Nhlangano" },
            // stolen, and never deployed
            new() { SizeId = 1, ContentId = 10, FacilityId = 2, FacilityName = "Mbabane", Lost = true },
            new() { SizeId = 1, ContentId = 10 },
        };

        [Fact]
        public void Kpis_CountEveryCylinderExactlyOnce()
        {
            var k = CylinderReportService.BuildStock(Sample(), Combos, 30, Today).Kpis;

            Assert.Equal(10, k.Total);
            Assert.Equal(3, k.AtCentralStore);
            Assert.Equal(2, k.AtFacilities);
            Assert.Equal(2, k.WithPatients);
            Assert.Equal(1, k.InTransit);
            Assert.Equal(2, k.NotAvailable);
            Assert.Equal(k.Total, k.AtCentralStore + k.AtFacilities + k.WithPatients + k.InTransit + k.NotAvailable);
        }

        [Fact]
        public void Rows_CentralStoreFirst_InTransitOnlyAsIncoming()
        {
            var s = CylinderReportService.BuildStock(Sample(), Combos, 30, Today);

            Assert.Equal(new[] { "CMS", "Mbabane", "Nhlangano" }, s.Rows.Select(r => r.Facility));
            var cms = s.Rows[0];
            Assert.True(cms.IsCentralStore);
            Assert.Equal(3, cms.Total);                       // the in-transit cylinder is not counted at the source
            Assert.Equal(2, cms.Counts["1-10"]);
            Assert.Equal(1, cms.Counts["2-10"]);
            var nhlangano = s.Rows[2];
            Assert.Equal(0, nhlangano.Total);
            Assert.Equal(1, nhlangano.Incoming);
            Assert.Equal(4, s.Rows[1].Total);                 // Mbabane: 2 in store + 2 with patients, stolen excluded
        }

        [Fact]
        public void Columns_OnlyCombinationsWithStock_InFixedOrder()
        {
            var s = CylinderReportService.BuildStock(Sample(), Combos, 30, Today);
            Assert.Equal(new[] { "1-10", "1-11", "2-10" }, s.Columns.Select(c => c.Key));
            Assert.Equal(new[] { 0, 1, 2 }, s.Columns.Select(c => c.Slot)); // slot = position in the full list, not in this page
        }

        [Fact]
        public void CombinationsPastEightFoldIntoOther()
        {
            var many = Enumerable.Range(0, 10).Select(i => new StockColumn { Key = $"{i}-1", Label = $"C{i}", Slot = i }).ToList();
            var rows = Enumerable.Range(0, 10).Select(i => new CylinderStockRow { SizeId = i, ContentId = 1, FacilityId = 5, FacilityName = "F" }).ToList();

            var s = CylinderReportService.BuildStock(rows, many, 30, Today);

            Assert.Equal(9, s.Columns.Count);                 // 8 slots + Other
            Assert.Equal(CylinderReportService.OtherKey, s.Columns.Last().Key);
            Assert.Equal(2, s.Rows[0].Counts[CylinderReportService.OtherKey]);
        }

        [Fact]
        public void Overdue_OnlyPatientsAtOrPastTheThreshold()
        {
            var s = CylinderReportService.BuildStock(Sample(), Combos, 30, Today);
            var o = Assert.Single(s.Overdue);
            Assert.Equal("T-OLD", o.Tag);
            Assert.Equal(45, o.Days);
            Assert.Equal(1, s.Kpis.Overdue);

            Assert.Equal(2, CylinderReportService.BuildStock(Sample(), Combos, 3, Today).Overdue.Count); // 3 days counts
        }

        [Fact]
        public void ChartSeries_TopThreeByNetworkStock_TiesInLookupOrder()
        {
            var rows = new List<CylinderStockRow>
            {
                At(1, "A", 2, 10), At(1, "A", 2, 10), At(1, "A", 2, 10),   // 47 L Oxygen x3
                At(1, "A", 1, 11), At(2, "B", 1, 11),                       // 10 L Medical Air x2
                At(2, "B", 1, 10),                                          // 10 L Oxygen x1
                At(2, "B", 2, 11),                                          // 47 L Medical Air x1 (tie, later in lookup order)
                new() { SizeId = 1, ContentId = 10, FacilityId = 2, InTransitToFacilityId = 1 },   // in transit: not counted
                new() { SizeId = 1, ContentId = 10, FacilityId = 2, Lost = true },                 // lost: not counted
            };

            var series = CylinderReportService.PickChartSeries(rows, Combos);

            Assert.Equal(new[] { "2-10", "1-11", "1-10" }, series.Select(c => c.Key));
            Assert.Equal(new[] { 0, 1, 2 }, series.Select(c => c.Slot));
        }

        [Fact]
        public void ScopeToFacility_KeepsWhatIsHereAndWhatIsComing()
        {
            var rows = Sample();
            var mbabane = CylinderReportService.ScopeToFacility(rows, 2);
            var nhlangano = CylinderReportService.ScopeToFacility(rows, 1);

            Assert.Equal(5, mbabane.Count);                                         // 2 store + 2 patients + 1 stolen
            Assert.Single(nhlangano);                                               // only the incoming cylinder
            Assert.Equal(1, nhlangano[0].InTransitToFacilityId);
            Assert.DoesNotContain(CylinderReportService.ScopeToFacility(rows, 238), r => r.InTransitToFacilityId != null); // outgoing leaves the source view
        }

        [Fact]
        public void RequestSummary_PerFacility()
        {
            var facts = new[]
            {
                new RequestFact { Facility = "Mbabane", Status = CylinderRequestStatus.Approved, Quantity = 20, Ordered = true, OrderQuantity = 20, Dispatched = 20, Received = 18 },
                new RequestFact { Facility = "Mbabane", Status = CylinderRequestStatus.Requested, Quantity = 5 },
                new RequestFact { Facility = "Mbabane", Status = CylinderRequestStatus.Rejected, Quantity = 50 },
                new RequestFact { Facility = "Hlatikulu", Status = CylinderRequestStatus.Approved, Quantity = 8 },
            };

            var rows = CylinderReportService.BuildRequestSummary(facts);

            Assert.Equal(new[] { "Hlatikulu", "Mbabane" }, rows.Select(r => r.Facility));
            var m = rows[1];
            Assert.Equal(3, m.Requests);
            Assert.Equal(1, m.PendingApproval);
            Assert.Equal(1, m.Rejected);
            Assert.Equal(20, m.Requested);                    // approved requests only
            Assert.Equal(20, m.Ordered);
            Assert.Equal(18, m.Received);
            Assert.Equal(2, m.Outstanding);
            Assert.Equal(0, rows[0].Ordered);                 // approved but not yet ordered
        }
    }
}
