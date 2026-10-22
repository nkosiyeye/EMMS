using EMMS.Utility;
using static EMMS.Models.Enumerators;

namespace EMMS.Tests
{
    public class AppNavigationTests
    {
        private static (string? Section, string? Link) Where(string controller, string action)
        {
            var here = AppNavigation.Locate(controller, action);
            return (here.Section?.Label, here.Link?.Label);
        }

        [Theory]
        [InlineData("Home", "Index", null, "Dashboard")]
        [InlineData("AssetManagement", "registerAsset", null, "Asset Management")]
        [InlineData("AssetMovement", "Index", "Asset Movement", "Move Asset")]
        [InlineData("AssetMovement", "edit", "Asset Movement", "Move Asset")]
        [InlineData("AssetMovement", "Queue", "Asset Movement", "Movement Queue")]
        [InlineData("assetmovement", "RECIEVEASSET", "Asset Movement", "Receive Asset")]
        [InlineData("JobManagement", "editWorkRequest", "Job Management", "Work Requests")]
        [InlineData("JobManagement", "workload", "Job Management", "Technician Workload")]
        [InlineData("Cylinder", "Dashboard", null, "Cylinder Dashboard")]
        [InlineData("Admin", "EditLookupItem", null, "Lookup List")]
        [InlineData("Users", "Edit", "User Management", "User Accounts")]
        [InlineData("Admin", "createUserRole", "User Management", "User Roles")]
        public void Locate_PutsEachPageUnderItsMenuEntry(string controller, string action, string? section, string link)
        {
            Assert.Equal((section, link), Where(controller, action));
        }

        [Fact]
        public void Locate_PageOwnedOnlyByASection_HasTheSectionButNoLink()
        {
            Assert.Equal(("Job Management", (string?)null), Where("JobManagement", "jobCard"));
        }

        [Fact]
        public void Locate_UnknownPage_IsNowhere()
        {
            Assert.Equal(((string?)null, (string?)null), Where("Home", "Privacy"));
        }

        [Fact]
        public void Sections_HaveDistinctCollapseIds()
        {
            var ids = AppNavigation.Items.Where(i => i.IsSection).Select(i => i.CollapseId).ToList();
            Assert.Equal(ids.Count, ids.Distinct().Count());
        }

        [Fact]
        public void DataCollector_SeesOnlyDashboardAndAssets()
        {
            var visible = AppNavigation.Items.Where(i => i.VisibleTo(UserType.DataCollector)).Select(i => i.Label);
            Assert.Equal(["Dashboard", "Asset Management"], visible);
        }
    }
}
