namespace EMMS.Models
{
    public class Pagination
    {
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public int TotalItemCount { get; set; }

        public Pagination(int totalItems, int currentPage, int pageSize)
        {
            CurrentPage = currentPage;
            TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            TotalItemCount = totalItems;
        }

        public bool HasPreviousPage => CurrentPage > 1;
        public bool HasNextPage => CurrentPage < TotalPages;
    }
}
