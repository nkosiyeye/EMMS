namespace EMMS.Models.Pagination
{
    public class PaginatedResult<T>
    {
        public int TotalCount { get; set; }
        public int FilteredCount { get; set; }
        public List<T> Items { get; set; }
    }
}
