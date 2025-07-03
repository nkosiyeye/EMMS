using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using EMMS.Models.Domain;
using EMMS.Models.Entities;
using static EMMS.Models.Enumerators;

namespace EMMS.Models
{
    public class MoveAsset : BaseEntity
    {
        [Key]
        [Display(Name = "Movement ID")]
        public Guid MovementId { get; set; }

        [Required(ErrorMessage = "Movement Date is required")]
        [Display(Name = "Movement Date")]
        public DateTime MovementDate { get; set; }

        [Required]
        [Display(Name = "Asset Id")]
        public Guid AssetId { get; set; }
        [ForeignKey(nameof(AssetId))]
        public virtual Asset? Asset { get; set; }

        // Movement Type
        [Required(ErrorMessage = "Movement Type is required")]
        [Display(Name = "Movement Type")]
        public MovementType MovementType { get; set; }
        //[ForeignKey(nameof(MovementTypeId))]
        //public virtual LookupItem? MovementType { get; set; }

        // From
        [Required]
        [Display(Name = "From")]
        public int FromId { get; set; }
        [ForeignKey(nameof(FromId))]
        public virtual Facility? From { get; set; }

        [Required(ErrorMessage = "Facility is Required")]
        [Display(Name = "Facility")]
        public int FacilityId { get; set; }
        [ForeignKey(nameof(FacilityId))]
        public virtual Facility? Facility { get; set; }

        // Service Point
        [Display(Name = "Service Point")]
        public int? ServicePointId { get; set; }
        [ForeignKey(nameof(ServicePointId))]
        public virtual LookupItem? ServicePoint { get; set; }

        // Reason
        [Required(ErrorMessage = "Reason is required")]
        [Display(Name = "Reason")]
        public MovementReason Reason { get; set; }

        [Display(Name = "OtherReason")]
        public string? OtherReason { get; set; }
        //[ForeignKey(nameof(ReasonId))]
        //public virtual LookupItem? Reason { get; set; }

        // Functional Status
        [Required(ErrorMessage = "Functional Status is required")]
        [Display(Name = "Functional Status")]
        public FunctionalStatus FunctionalStatus { get; set; }
        //[ForeignKey(nameof(FunctionalStatusId))]
       // public virtual LookupItem? FunctionalStatus { get; set; }

        [Display(Name = "Approved")]
        public bool IsApproved { get; set; }

        [Display(Name = "Approved By")]
        public Guid? ApprovedBy { get; set; }
      

        
        [Display(Name = "Date Received")]
        public DateTime? DateReceived { get; set; }

        // Condition
        [Display(Name = "Condition")]
        public int? ConditionId { get; set; }
        [ForeignKey(nameof(ConditionId))]
        public virtual LookupItem? Condition { get; set; }

        
        [Display(Name = "Received By")]
        public Guid? ReceivedBy { get; set; }

        [Display(Name = "Remarks")]
        public string? Remarks { get; set; }

        public Guid? CreatedBy { get; set; }
        public DateTime? DateCreated { get; set; }
        public Guid? ModifiedBy { get; set; }
        public DateTime? DateModified { get; set; }
        public RowStatus RowState { get; set; }
    }
}

