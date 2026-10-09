using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using EMMS.Models.Admin;
using EMMS.Models.Domain;
using EMMS.Models.Entities;
using static EMMS.Models.Enumerators;

namespace EMMS.Models
{
    /// <summary>A facility's request for cylinders; approved by the facility pharmacist before CMS orders it.</summary>
    public class CylinderRequest : BaseEntity
    {
        [Key]
        public Guid RequestId { get; set; }

        [Display(Name = "Facility")]
        public int FacilityId { get; set; }
        [ForeignKey(nameof(FacilityId))]
        public virtual Facility? Facility { get; set; }

        // Item (a SubCategory under a CylinderProfile category, e.g. Oxygen Cylinder)
        [Required(ErrorMessage = "Item is required")]
        [Display(Name = "Item")]
        public int DescriptionId { get; set; }
        [ForeignKey(nameof(DescriptionId))]
        public virtual LookupItem? Description { get; set; }

        [Required(ErrorMessage = "Size is required")]
        [Display(Name = "Size")]
        public int SizeId { get; set; }
        [ForeignKey(nameof(SizeId))]
        public virtual LookupItem? Size { get; set; }

        [Required(ErrorMessage = "Content is required")]
        [Display(Name = "Content")]
        public int ContentId { get; set; }
        [ForeignKey(nameof(ContentId))]
        public virtual LookupItem? Content { get; set; }

        [Range(1, 10000, ErrorMessage = "Quantity must be at least 1")]
        [Display(Name = "Quantity")]
        public int Quantity { get; set; }

        [Required(ErrorMessage = "Requisition name is required")]
        [MaxLength(150)]
        [Display(Name = "Requisition Name")]
        public string? RequisitionName { get; set; }

        [Required(ErrorMessage = "Requisition number is required")]
        [MaxLength(50)]
        [Display(Name = "Requisition Number")]
        public string? RequisitionNumber { get; set; }

        public CylinderRequestStatus Status { get; set; }

        public Guid? ApprovedBy { get; set; }
        [ForeignKey(nameof(ApprovedBy))]
        public virtual User? Approver { get; set; }
        public DateTime? DateApproved { get; set; }

        // Pharmacist rejection reason, or CMS note when returned (plant down)
        [MaxLength(500)]
        public string? DecisionReason { get; set; }

        public Guid? CreatedBy { get; set; }
        [ForeignKey(nameof(CreatedBy))]
        public virtual User? Requester { get; set; }
        public DateTime? DateCreated { get; set; }
        public Guid? ModifiedBy { get; set; }
        public DateTime? DateModified { get; set; }
        public RowStatus RowState { get; set; }
    }
}
