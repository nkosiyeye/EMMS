using System.Globalization;
using System.Text;

namespace EMMS.Utility
{
    /// <summary>
    /// Code 128 barcode encoder (code sets B and C) that renders an SVG.
    /// No external dependency so stickers print offline.
    /// </summary>
    public static class Code128
    {
        // Bar/space widths for symbol values 0..106 (106 = stop, which has a 7th element).
        private static readonly string[] Patterns =
        {
            "212222","222122","222221","121223","121322","131222","122213","122312","132212","221213",
            "221312","231212","112232","122132","122231","113222","123122","123221","223211","221132",
            "221231","213212","223112","312131","311222","321122","321221","312212","322112","322211",
            "212123","212321","232121","111323","131123","131321","112313","132113","132311","211313",
            "231113","231311","112133","112331","132131","113123","113321","133121","313121","211331",
            "231131","213113","213311","213131","311123","311321","331121","312113","312311","332111",
            "314111","221411","431111","111224","111422","121124","121421","141122","141221","112214",
            "112412","122114","122411","142112","142211","241211","221114","413111","241112","134111",
            "111242","121142","121241","114212","124112","124211","411212","421112","421211","212141",
            "214121","412121","111143","111341","131141","114113","114311","411113","411311","113141",
            "114131","311141","411131","211412","211214","211232","2331112"
        };

        private const int CodeC = 99;
        private const int CodeB = 100;
        private const int StartB = 104;
        private const int StartC = 105;
        private const int Stop = 106;

        /// <summary>Symbol values including start, check and stop.</summary>
        public static IReadOnlyList<int> Encode(string text)
        {
            if (string.IsNullOrEmpty(text))
                throw new ArgumentException("Barcode text is required.", nameof(text));
            foreach (var ch in text)
                if (ch < 32 || ch > 126)
                    throw new ArgumentException($"Character '{ch}' cannot be encoded in Code 128 B/C.", nameof(text));

            var codes = new List<int>();
            int i = 0;
            bool inC;

            int leadingDigits = DigitRun(text, 0);
            if (leadingDigits == text.Length ? leadingDigits >= 2 && leadingDigits % 2 == 0 : leadingDigits >= 4)
            {
                codes.Add(StartC);
                inC = true;
            }
            else
            {
                codes.Add(StartB);
                inC = false;
            }

            while (i < text.Length)
            {
                int run = DigitRun(text, i);
                if (inC)
                {
                    if (run >= 2)
                    {
                        codes.Add(int.Parse(text.AsSpan(i, 2), NumberStyles.None, CultureInfo.InvariantCulture));
                        i += 2;
                        continue;
                    }
                    codes.Add(CodeB);
                    inC = false;
                }

                // Switch to C for a run of digits worth compressing: 4+ at the end, 6+ in the middle.
                bool atEnd = i + run == text.Length;
                if (run >= (atEnd ? 4 : 6))
                {
                    if (run % 2 == 1)
                    {
                        codes.Add(text[i] - 32);
                        i++;
                    }
                    codes.Add(CodeC);
                    inC = true;
                    continue;
                }

                codes.Add(text[i] - 32);
                i++;
            }

            int checksum = codes[0];
            for (int p = 1; p < codes.Count; p++)
                checksum += codes[p] * p;
            codes.Add(checksum % 103);
            codes.Add(Stop);
            return codes;
        }

        /// <summary>Module string, '1' = bar and '0' = space, without quiet zones.</summary>
        public static string ToModules(string text)
        {
            var sb = new StringBuilder();
            foreach (var code in Encode(text))
            {
                var pattern = Patterns[code];
                for (int e = 0; e < pattern.Length; e++)
                    sb.Append(e % 2 == 0 ? '1' : '0', pattern[e] - '0');
            }
            return sb.ToString();
        }

        /// <summary>SVG barcode scaled to its container width, with a 10-module quiet zone each side.</summary>
        public static string ToSvg(string text, int height = 40)
        {
            const int quiet = 10;
            var modules = ToModules(text);
            int width = modules.Length + quiet * 2;

            var sb = new StringBuilder();
            sb.Append(CultureInfo.InvariantCulture,
                $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {width} {height}\" preserveAspectRatio=\"none\" shape-rendering=\"crispEdges\" role=\"img\" aria-label=\"Barcode {System.Net.WebUtility.HtmlEncode(text)}\">");
            int x = 0;
            while (x < modules.Length)
            {
                if (modules[x] == '1')
                {
                    int start = x;
                    while (x < modules.Length && modules[x] == '1') x++;
                    sb.Append(CultureInfo.InvariantCulture, $"<rect x=\"{start + quiet}\" y=\"0\" width=\"{x - start}\" height=\"{height}\"/>");
                }
                else x++;
            }
            sb.Append("</svg>");
            return sb.ToString();
        }

        private static int DigitRun(string text, int start)
        {
            int n = 0;
            while (start + n < text.Length && char.IsAsciiDigit(text[start + n])) n++;
            return n;
        }
    }
}
