using EMMS.Models.Entities;
using EMMS.Utility;

namespace EMMS.Tests
{
    public class SerialNumberTests
    {
        [Theory]
        [InlineData("CYL-10041", "CYL-10041")]
        [InlineData("  CYL-10041  ", "CYL-10041")]
        [InlineData("CYL-10041\r\n", "CYL-10041")]   // keyboard-wedge scanner suffix
        [InlineData("\tCYL-10041\t", "CYL-10041")]
        [InlineData("SN  12\t 34", "SN 12 34")]
        [InlineData("CYL\u0002-10041", "CYL-10041")]   // non-whitespace control char dropped
        [InlineData("", null)]
        [InlineData("   ", null)]
        [InlineData(null, null)]
        public void Normalize(string? input, string? expected)
        {
            Assert.Equal(expected, SerialNumber.Normalize(input));
        }

        [Fact]
        public void FindDuplicates_IsCaseAndWhitespaceInsensitive()
        {
            var dups = SerialNumber.FindDuplicates(new[] { "CYL-1", " cyl-1 ", "CYL-2", null, "", "CYL-3", "CYL-3\r\n" });

            Assert.Equal(2, dups.Count);
            Assert.Contains("CYL-1", dups, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("CYL-3", dups);
        }

        [Fact]
        public void FindDuplicates_NoneWhenUnique()
        {
            Assert.Empty(SerialNumber.FindDuplicates(new[] { "A", "B", null, null }));
        }

        [Theory]
        [InlineData(null, true, true)]
        [InlineData("", true, true)]
        [InlineData("not json", true, true)]
        [InlineData("{\"Other\":true}", true, true)]
        [InlineData("{\"RequiresSerial\":false}", true, false)]
        [InlineData("{\"RequiresSerial\":true}", false, true)]
        public void GetFlag_WithDefault_MatchesQuickRegistrationRule(string? flagsJson, bool defaultValue, bool expected)
        {
            var item = new LookupItem { FlagsJson = flagsJson };
            Assert.Equal(expected, item.GetFlag("RequiresSerial", defaultValue));
        }
    }
}
