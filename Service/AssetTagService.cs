using System.Globalization;

namespace EMMS.Service
{
    /// <summary>
    /// Generates asset tags from the AssetTagSeq database sequence (Finding D1).
    /// Tags keep the existing format {FacilityCode}AS-{number:D3}; the number is unique system-wide,
    /// so uniqueness no longer depends on counting or parsing existing tags.
    /// </summary>
    public class AssetTagService
    {
        public const string TagInfix = "AS-";
        private readonly IAssetTagSequence _sequence;

        public AssetTagService(IAssetTagSequence sequence)
        {
            _sequence = sequence;
        }

        public static string Format(string? facilityCode, long number) =>
            $"{facilityCode?.Trim()}{TagInfix}{number.ToString("D3", CultureInfo.InvariantCulture)}";

        public async Task<string> NextTagAsync(string? facilityCode) =>
            (await NextTagsAsync(new[] { facilityCode }))[0];

        /// <summary>One tag per entry, in order; the whole batch is reserved in a single round trip.</summary>
        public async Task<IReadOnlyList<string>> NextTagsAsync(IReadOnlyList<string?> facilityCodes)
        {
            if (facilityCodes.Count == 0) return Array.Empty<string>();

            long first = await _sequence.NextRangeAsync(facilityCodes.Count);
            var tags = new string[facilityCodes.Count];
            for (int i = 0; i < tags.Length; i++)
                tags[i] = Format(facilityCodes[i], first + i);
            return tags;
        }
    }
}
