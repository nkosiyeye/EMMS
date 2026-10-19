using EMMS.Service;

namespace EMMS.Tests
{
    public class AssetTagServiceTests
    {
        // Behaves like a SQL SEQUENCE: each call reserves a block atomically.
        private class FakeSequence : IAssetTagSequence
        {
            private long _next;
            public List<int> Requests { get; } = new();

            public FakeSequence(long start) => _next = start;

            public Task<long> NextRangeAsync(int count)
            {
                lock (Requests) Requests.Add(count);
                return Task.FromResult(Interlocked.Add(ref _next, count) - count);
            }
        }

        [Theory]
        [InlineData("S003", 120, "S003AS-120")]
        [InlineData("S003", 7, "S003AS-007")]
        [InlineData("S003", 1234, "S003AS-1234")]
        [InlineData(" SWZ01 ", 421, "SWZ01AS-421")]
        [InlineData(null, 5, "AS-005")]
        public void Format_KeepsExistingTagFormat(string? facilityCode, long number, string expected)
        {
            Assert.Equal(expected, AssetTagService.Format(facilityCode, number));
        }

        [Fact]
        public async Task NextTagsAsync_ReservesOneRangeForTheBatchInOrder()
        {
            var sequence = new FakeSequence(120);
            var service = new AssetTagService(sequence);

            var tags = await service.NextTagsAsync(new[] { "S003", "S003", "M001" });

            Assert.Equal(new[] { "S003AS-120", "S003AS-121", "M001AS-122" }, tags);
            Assert.Equal(new[] { 3 }, sequence.Requests);
        }

        [Fact]
        public async Task NextTagsAsync_EmptyBatchDoesNotTouchTheSequence()
        {
            var sequence = new FakeSequence(1);
            var tags = await new AssetTagService(sequence).NextTagsAsync(Array.Empty<string?>());

            Assert.Empty(tags);
            Assert.Empty(sequence.Requests);
        }

        [Fact]
        public async Task ConcurrentRegistrations_NeverShareATag()
        {
            // The old count + 1 generator produced duplicates here; the sequence cannot.
            var service = new AssetTagService(new FakeSequence(1));

            var batches = await Task.WhenAll(Enumerable.Range(0, 50).Select(i =>
                service.NextTagsAsync(Enumerable.Repeat<string?>("S003", 1 + i % 4).ToList())));
            var all = batches.SelectMany(b => b).ToList();

            Assert.Equal(all.Count, all.Distinct().Count());
        }
    }
}
