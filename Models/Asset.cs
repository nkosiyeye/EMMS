namespace EMMS.Models
{
    public class Asset
    {
        public string Id { get; set; }
        public string Category { get; set; }
        public string SubCategory { get; set; }
        public string ItemName { get; set; }
        public string Department { get; set; }
        public string Manufacturer { get; set; }
        public string SerialNumber { get; set; }
        public string Model { get; set; }
        public bool Placement { get; set; }
        public string Vendor { get; set; }
        public string ServiceProvider { get; set; }
        public DateTime Date { get; set; }
        public string Status { get; set; }
        public string Location { get; set; }
        public string FunctionalStatus { get; set; }
    }
}
