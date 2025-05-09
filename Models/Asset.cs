using System.ComponentModel.DataAnnotations;

namespace EMMS.Models
{
    public class Asset
    {
        [Required]
        [Display(Name = "Asset Tag")]
        public string AssetId { get; set; }

        [Required]
        [Display(Name = "Category")]
        public string Category { get; set; }

        [Required]
        [Display(Name = "SubCategory")]
        public string SubCategory { get; set; }

        [Required]
        [Display(Name = "Item Name")]
        public string ItemName { get; set; }

        [Required]
        [Display(Name = "Department")]
        public string Department { get; set; }

        [Required]
        [Display(Name = "Manufacturer")]
        public string Manufacturer { get; set; }

        [Required]
        [Display(Name = "Serial Number")]
        public string SerialNumber { get; set; }

        [Required]
        [Display(Name = "Model")]
        public string Model { get; set; }

        [Display(Name = "Placement")]
        public bool IsPlacement { get; set; }

        [Display(Name = "Placement Start Date")]
        public DateTime? PlacementStartDate { get; set; }

        [Display(Name = "Placement End Date")]
        public DateTime? PlacementEndDate { get; set; }

        [Display(Name = "Donated")]
        public bool IsDonated { get; set; }

        [Display(Name = "Serviceable")]
        public bool IsServiceable { get; set; }

        [Display(Name = "Service Period")]
        public string ServicePeriod { get; set; }

        [Display(Name = "Service Interval")]
        public string ServiceInterval { get; set; }

        [Required]
        [Display(Name = "Vendor")]
        public string Vendor { get; set; }

        [Required]
        [Display(Name = "Service Provider")]
        public string ServiceProvider { get; set; }

        [Required]
        [Display(Name = "Status")]
        public string Status { get; set; }

        [Required]
        [Display(Name = "Procurement Date")]
        public DateTime ProcurementDate { get; set; }

        [Required]
        [Display(Name = "Cost")]
        public decimal Cost { get; set; }

        [Display(Name = "Unit of Measure")]
        public string UnitOfMeasure { get; set; }

        [Display(Name = "Quantity")]
        public int Quantity { get; set; }

        [Display(Name = "Lifespan Period")]
        public string LifespanPeriod { get; set; }

        [Display(Name = "Lifespan Quantity")]
        public int LifespanQuantity { get; set; }
    }
}
