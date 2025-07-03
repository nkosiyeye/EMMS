using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using EMMS.Models.Admin;
using EMMS.Models.Domain;
using EMMS.Models.Entities;
using static EMMS.Models.Enumerators;

namespace EMMS.Models
{
    public class Asset : BaseEntity    {
        [Required]
        [Display(Name = "Asset Id")]
        public Guid AssetId { get; set; }

        [Display(Name = "Asset Tag Number")]
        public string AssetTagNumber { get; set; }

        // Category
        [Required(ErrorMessage = "Category is required")]
        [Display(Name = "Category")]
        public int CategoryId { get; set; }
        [ForeignKey(nameof(CategoryId))]
        public virtual LookupItem? Category { get; set; }

        // SubCategory
        [Required(ErrorMessage = "SubCategory is required")]
        [Display(Name = "SubCategory")]
        public int SubCategoryId { get; set; }
        [ForeignKey(nameof(SubCategoryId))]
        public virtual LookupItem? SubCategory { get; set; }

        [Required(ErrorMessage = "Item Name is required")]
        [Display(Name = "Item Name")]
        public string ItemName { get; set; }

        // Department
        [Required(ErrorMessage = "Department is required")]
        [Display(Name = "Department")]
        public int DepartmentId { get; set; }
        [ForeignKey(nameof(DepartmentId))]
        public virtual LookupItem? Department { get; set; }

        // Manufacturer
        [Required(ErrorMessage = "Manufacturer is required")]
        [Display(Name = "Manufacturer")]
        public int ManufacturerId { get; set; }
        [ForeignKey(nameof(ManufacturerId))]
        public virtual LookupItem? Manufacturer { get; set; }

        [Required(ErrorMessage = "Serial Number is required")]
        [Display(Name = "Serial Number")]
        public string SerialNumber { get; set; }

        [Required(ErrorMessage = "Model is required")]
        [Display(Name = "Model")]
        public string Model { get; set; }

        [Display(Name = "Placement")]
        public bool IsPlacement { get; set; }

        [Display(Name = "Placement Start Date")]
       // [RequiredIfAttribute("IsPlacement",true, ErrorMessage = "Placement Start Date is required when Placement is checked.")]
        public DateTime? PlacementStartDate { get; set; }

        [Display(Name = "Placement End Date")]
        //[RequiredIfAttribute("IsPlacement",true, ErrorMessage = "Placement End Date is required when Placement is checked.")]
        public DateTime? PlacementEndDate { get; set; }

        [Display(Name = "Donated")]
        public bool IsDonated { get; set; }

        [Display(Name = "Serviceable")]
        public bool IsServiceable { get; set; }

        [Display(Name = "Next Service Date")]
        public DateTime? NextServiceDate{ get; set; }
        //[ForeignKey(nameof(ServicePeriodId))]
        //public virtual LookupItem? ServicePeriodName { get; set; }

        [Display(Name = "Service Interval")]
        public int? ServiceInterval { get; set; }

        // Vendor
        [Required(ErrorMessage = "Vendor is required")]
        [Display(Name = "Vendor")]
        public int VendorId { get; set; }
        [ForeignKey(nameof(VendorId))]
        public virtual LookupItem? Vendor { get; set; }

        // Service Provider
        [Required(ErrorMessage = "Service Provider is required")]
        [Display(Name = "Service Provider")]
        public int ServiceProviderId { get; set; }
        [ForeignKey(nameof(ServiceProviderId))]
        public virtual LookupItem? ServiceProvider { get; set; }

        // Status
        [Required(ErrorMessage = "Status is required")]
        [Display(Name = "Status")]
        public int StatusId { get; set; }
        //[ForeignKey(nameof(StatusId))]
        //public virtual LookupItem? Status { get; set; }

        [RequiredIf("StatusId", (int)ProcurementStatus.New , ErrorMessage = "Procurement Date is required for new assets.")]
        [Display(Name = "Procurement Date")]
        public DateTime? ProcurementDate { get; set; }

        [RequiredIf("StatusId", (int)ProcurementStatus.New, ErrorMessage = "Cost is required for new assets  and cannot be a negative value.")]
        [Display(Name = "Cost")]
        public decimal? Cost { get; set; }

        [RequiredIf("StatusId", (int)ProcurementStatus.New, ErrorMessage = "Lifespan is required for new assets and cannot be a negative value.")]
        [Display(Name = "Lifespan (Years)")]
        public int? LifespanQuantity { get; set; }

        public Guid? CreatedBy { get; set; }
        [ForeignKey(nameof(CreatedBy))]
        public virtual User? User { get; set; }
        public DateTime? DateCreated { get; set; }
        public Guid? ModifiedBy { get; set; }
        public DateTime? DateModified { get; set; }
        public RowStatus RowState { get; set; }
    }
}

