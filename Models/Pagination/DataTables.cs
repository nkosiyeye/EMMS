namespace EMMS.Models.Pagination
{
    public class DataTablesRequest
    {
        public int Draw { get; set; }
        public int Start { get; set; }
        public int Length { get; set; }
        public DataTablesSearch Search { get; set; }
    }

    public class DataTablesSearch
    {
        public string Value { get; set; }
    }

}
