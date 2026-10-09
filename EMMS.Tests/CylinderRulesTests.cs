using EMMS.Service;

namespace EMMS.Tests
{
    public class CylinderRulesTests
    {
        private static readonly int[] Sizes = { 10, 11 };
        private static readonly int[] Contents = { 20 };

        [Fact]
        public void ValidCylinder_HasNoErrors()
        {
            Assert.Empty(CylinderRules.Validate(10, 20, Sizes, Contents, "CYL-10041", alreadyDeployed: true, facilityId: 5));
        }

        [Fact]
        public void MissingEverything_ReportsEachRule()
        {
            var errors = CylinderRules.Validate(null, 0, Sizes, Contents, "  ", alreadyDeployed: false, facilityId: null);

            Assert.Equal(new[]
            {
                CylinderRules.SizeRequired,
                CylinderRules.ContentRequired,
                CylinderRules.SerialRequired,
                CylinderRules.LocationRequired
            }, errors);
        }

        [Fact]
        public void LookupFromAnotherList_IsRejected()
        {
            // e.g. a Department id posted as SizeId
            var errors = CylinderRules.Validate(99, 98, Sizes, Contents, "CYL-1", true, 5);

            Assert.Equal(new[] { CylinderRules.SizeInvalid, CylinderRules.ContentInvalid }, errors);
        }

        [Theory]
        [InlineData(false, 5)]
        [InlineData(true, null)]
        [InlineData(true, 0)]
        public void InitialLocation_IsRequired(bool deployed, int? facilityId)
        {
            Assert.Equal(new[] { CylinderRules.LocationRequired },
                CylinderRules.Validate(10, 20, Sizes, Contents, "CYL-1", deployed, facilityId));
        }

        [Fact]
        public void ValidateDetail_IgnoresSerialAndLocation()
        {
            Assert.Empty(CylinderRules.ValidateDetail(11, 20, Sizes, Contents));
            Assert.Equal(new[] { CylinderRules.SizeRequired }, CylinderRules.ValidateDetail(null, 20, Sizes, Contents));
        }
    }
}
