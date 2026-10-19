using EMMS.Utility;

namespace EMMS.Tests
{
    // Expected values were cross-checked against JsBarcode 3.11.6 (CODE128B / CODE128 auto).
    public class Code128Tests
    {
        [Theory]
        [InlineData("S003AS-119", new[] { 104, 51, 16, 16, 19, 33, 51, 13, 17, 17, 25, 73, 106 })]                  // today's tag format: all code set B
        [InlineData("SWZ01AS-000421", new[] { 104, 51, 55, 58, 16, 17, 33, 51, 13, 99, 0, 4, 21, 65, 106 })]     // trailing digit run switches to C
        [InlineData("123456", new[] { 105, 12, 34, 56, 44, 106 })]                                                // all digits: start C
        [InlineData("X12345", new[] { 104, 56, 17, 99, 23, 45, 87, 106 })]                                        // odd run: first digit in B
        public void Encode_ProducesExpectedSymbols(string text, int[] expected)
        {
            Assert.Equal(expected, Code128.Encode(text));
        }

        [Fact]
        public void ToModules_MatchesReferenceBarcode()
        {
            const string expected = "1101001000011011101000100111011001001110110011001011100101000110001101110100010011011100100111001101001110011011100101100100001101001100011101011";
            Assert.Equal(expected, Code128.ToModules("S003AS-119"));
        }

        [Theory]
        [InlineData("S003AS-119")]
        [InlineData("SWZ01AS-000421")]
        public void ToModules_LengthIsElevenPerSymbolPlusStopTermination(string text)
        {
            Assert.Equal(Code128.Encode(text).Count * 11 + 2, Code128.ToModules(text).Length);
        }

        [Theory]
        [InlineData("")]
        [InlineData("TAB\tTAG")]
        [InlineData("Ü-001")]
        public void Encode_RejectsTextOutsidePrintableAscii(string text)
        {
            Assert.Throws<ArgumentException>(() => Code128.Encode(text));
        }

        [Fact]
        public void ToSvg_HasQuietZonesAndBars()
        {
            var svg = Code128.ToSvg("S003AS-119");
            int width = Code128.ToModules("S003AS-119").Length + 20;

            Assert.StartsWith("<svg", svg);
            Assert.Contains($"viewBox=\"0 0 {width} 40\"", svg);
            Assert.Contains("<rect x=\"10\"", svg); // first bar starts after the 10-module quiet zone
        }
    }
}
