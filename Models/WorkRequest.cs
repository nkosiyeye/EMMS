using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using EMMS.Models.Domain;
using EMMS.Models.Entities;
using static EMMS.Models.Enumerators;

namespace EMMS.Models
{
    public class WorkRequest : BaseEntity
    {
        [Key]
        public Guid WorkRequestId { get; set; }

        [Required]
        [Display(Name = "Asset Id")]
        public Guid? AssetId { get; set; }
        [ForeignKey(nameof(AssetId))]
        public virtual Asset? Asset { get; set; }

        [Required(ErrorMessage = "Request Date is required")]
        [Display(Name = "Request Date")]
        public DateTime RequestDate { get; set; }


        [Required(ErrorMessage = "Fault Report is required")]
        [Display(Name = "FaultReport")]
        public int FaultReportId { get; set; }
        [ForeignKey(nameof(FaultReportId))]
        public virtual LookupItem? FaultReport { get; set; }

        [Required(ErrorMessage = "Description is required")]
        [Display(Name = "Description")]
        public string? Description { get; set; }


        [Display(Name = "Work Status")]
        public int WorkStatusId { get; set; }
        [ForeignKey(nameof(WorkStatusId))]
        public virtual LookupItem? WorkStatus { get; set; }


        [Display(Name = "Outcome")]
        public int? OutcomeId { get; set; }
        [ForeignKey(nameof(OutcomeId))]
        public virtual LookupItem? Outcome { get; set; }

        [Display(Name = "Requested By")]
        public Guid? RequestedBy { get; set; }

        [Display(Name = "Facility")]
        public int FacilityId { get; set; }
        [ForeignKey(nameof(FacilityId))]
        public virtual Facility? Facility { get; set; }

        [Display(Name = "Close Date")]
        public DateTime? CloseDate { get; set; }


        public Guid? CreatedBy { get; set; }
        public DateTime? DateCreated { get; set; }
        public Guid? ModifiedBy { get; set; }
        public DateTime? DateModified { get; set; }
        public RowStatus RowState { get; set; }
    }
}
