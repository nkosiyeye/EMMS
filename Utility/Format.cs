namespace EMMS.Utility
{
    public static class Format
    {
        public static string FormatCategory(string category)
        {
            const string keyword = "Equipment";

            return category.Replace(keyword, "").Trim();

        }
    }
}
