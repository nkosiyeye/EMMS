using EMMS.Models.Admin;
using EMMS.Service;
using EMMS.ViewModels;
using static EMMS.Models.Enumerators;

namespace EMMS.Tests
{
    public class DashboardRulesTests
    {
        private static readonly DateTime Today = new(2026, 10, 21);

        private static User UserOf(UserType type, int facilityId = 7) =>
            new() { FacilityId = facilityId, UserRole = new UserRole { UserType = type } };

        [Fact]
        public void ScopeFacility_AdminMayPickAnyFacilityOrAll()
        {
            var admin = UserOf(UserType.Administrator);
            Assert.Null(DashboardService.ScopeFacility(admin, null));
            Assert.Equal(3, DashboardService.ScopeFacility(admin, 3));
        }

        [Theory]
        [InlineData(UserType.FacilityManager)]
        [InlineData(UserType.Biomed)]
        [InlineData(UserType.GeneralUser)]
        [InlineData(UserType.DataCollector)]
        [InlineData(UserType.Pharmacist)]
        [InlineData(UserType.CmsOfficer)]
        public void ScopeFacility_OthersArePinnedToTheirOwnFacility(UserType type)
        {
            var user = UserOf(type, facilityId: 7);
            Assert.Equal(7, DashboardService.ScopeFacility(user, null));
            Assert.Equal(7, DashboardService.ScopeFacility(user, 3));
        }

        [Fact]
        public void ScopeFacility_UserWithoutRoleIsPinned()
        {
            Assert.Equal(7, DashboardService.ScopeFacility(new User { FacilityId = 7 }, 3));
        }

        [Theory]
        [InlineData(0, 0, 0)]
        [InlineData(5, 0, 0)]
        [InlineData(1, 3, 33)]
        [InlineData(2, 3, 67)]
        [InlineData(10, 10, 100)]
        public void Percent_RoundsAndHandlesEmpty(int part, int whole, int expected)
        {
            Assert.Equal(expected, DashboardService.Percent(part, whole));
        }

        [Theory]
        [InlineData(100, 10, DashTone.Neutral)]   // healthy is not coloured
        [InlineData(90, 10, DashTone.Neutral)]
        [InlineData(89, 10, DashTone.Warning)]
        [InlineData(75, 10, DashTone.Warning)]
        [InlineData(74, 10, DashTone.Danger)]
        [InlineData(0, 0, DashTone.Neutral)]      // no equipment is not an alarm
        public void FunctionalTone(int percent, int total, DashTone expected)
        {
            Assert.Equal(expected, DashboardService.FunctionalTone(percent, total));
        }

        [Fact]
        public void CountTone_ColoursOnlyWhenThereIsSomething()
        {
            Assert.Equal(DashTone.Neutral, DashboardService.CountTone(0, DashTone.Danger));
            Assert.Equal(DashTone.Danger, DashboardService.CountTone(3, DashTone.Danger));
        }

        [Theory]
        [InlineData(-1, DashTone.Danger)]   // yesterday
        [InlineData(0, DashTone.Warning)]   // today
        [InlineData(7, DashTone.Warning)]
        [InlineData(8, DashTone.Neutral)]
        public void ServiceDueTone(int dueInDays, DashTone expected)
        {
            Assert.Equal(expected, DashboardService.ServiceDueTone(Today.AddDays(dueInDays), Today));
        }

        [Fact]
        public void AgeTone_WarnsOnlyAfterStaleDays()
        {
            Assert.Equal(DashTone.Neutral, DashboardService.AgeTone(Today.AddDays(-DashboardService.StaleDays), Today));
            Assert.Equal(DashTone.Warning, DashboardService.AgeTone(Today.AddDays(-DashboardService.StaleDays - 1), Today));
        }

        [Theory]
        [InlineData(0, "today")]
        [InlineData(1, "1 day ago")]
        [InlineData(12, "12 days ago")]
        [InlineData(-2, "today")]   // future dates never read as "-2 days ago"
        public void AgeText(int daysAgo, string expected)
        {
            var now = Today.AddHours(9.5);
            Assert.Equal(expected, DashboardService.AgeText(now.AddDays(-daysAgo), now));
        }

        [Fact]
        public void AgeText_IgnoresTimeOfDay()
        {
            var now = new DateTime(2026, 10, 21, 0, 5, 0);
            Assert.Equal("1 day ago", DashboardService.AgeText(new DateTime(2026, 10, 20, 23, 59, 0), now));
        }

        [Theory]
        [InlineData(-5, "in 5 days")]
        [InlineData(-1, "in 1 day")]
        [InlineData(0, "due today")]
        [InlineData(1, "1 day overdue")]
        [InlineData(12, "12 days overdue")]
        public void OverdueText(int daysPastDue, string expected)
        {
            Assert.Equal(expected, DashboardService.OverdueText(Today.AddDays(-daysPastDue), Today));
        }

        [Theory]
        [InlineData(FunctionalStatus.Functional, "Functional", DashTone.Success)]
        [InlineData(FunctionalStatus.NonFunctional, "Non-functional", DashTone.Danger)]
        [InlineData(FunctionalStatus.UnderMaintenance, "Under maintenance", DashTone.Warning)]
        [InlineData(FunctionalStatus.Idle, "Idle", DashTone.Neutral)]
        [InlineData(FunctionalStatus.Unknown, "Unknown", DashTone.Neutral)]
        public void FunctionalStatus_LabelAndTone(FunctionalStatus status, string label, DashTone tone)
        {
            Assert.Equal(label, DashboardService.FunctionalLabel(status));
            Assert.Equal(tone, DashboardService.FunctionalStatusTone(status));
        }

        [Theory]
        [InlineData(DashTone.Neutral, "secondary")]
        [InlineData(DashTone.Info, "primary")]
        [InlineData(DashTone.Success, "success")]
        [InlineData(DashTone.Warning, "warning")]
        [InlineData(DashTone.Danger, "danger")]
        public void Tone_MapsToBootstrapColour(DashTone tone, string expected)
        {
            Assert.Equal(expected, tone.Css());
        }

        [Fact]
        public void RowAction_IsLabelOnlyWithoutLinkOrServiceRequest()
        {
            Assert.True(new RowAction { Label = "Requested" }.IsLabel);
            Assert.False(new RowAction { Label = "Open", Link = new DashLink("Home") }.IsLabel);
            Assert.False(new RowAction { Label = "Request service", ServiceRequestAssetId = Guid.NewGuid() }.IsLabel);
        }
    }
}
