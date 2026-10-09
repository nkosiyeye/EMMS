namespace EMMS.Service
{
    public interface IAssetTagSequence
    {
        /// <summary>Reserves <paramref name="count"/> consecutive numbers and returns the first one.</summary>
        Task<long> NextRangeAsync(int count);
    }
}
