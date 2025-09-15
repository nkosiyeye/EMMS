using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using EMMS.Models.Admin;
using EMMS.Models.Domain;
using EMMS.Models.Entities;
using static EMMS.Models.Enumerators;

namespace EMMS.Models
{
    public class InfrustructureWorkRequest : BaseEntity
    {
        [Key]
        public Guid WorkRequestId { get; set; }

        [Required(ErrorMessage = "Type Of Request is required")]
        [Display(Name = "Type Of Request")]
        public int? TypeOfRequestId { get; set; }
        [ForeignKey(nameof(TypeOfRequestId))]
        public virtual LookupItem? TypeOfRequest { get; set; }

        [Required(ErrorMessage = "Request Date is required")]
        [Display(Name = "Request Date")]
        public DateTime RequestDate { get; set; }

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

        [ForeignKey(nameof(RequestedBy))]
        public virtual User? RequestedByUser { get; set; }

        [Display(Name = "Facility")]
        public int FacilityId { get; set; }
        [ForeignKey(nameof(FacilityId))]
        public virtual Facility? Facility { get; set; }

        [Display(Name = "Cancel Reason")]
        public int? CancelReasonId { get; set; }
        [ForeignKey(nameof(CancelReasonId))]
        public virtual LookupItem? CancelReason { get; set; }

        [Display(Name = "Close Date")]
        public DateTime? CloseDate { get; set; }

        public Guid? JobId { get; set; }
        [ForeignKey(nameof(JobId))]
        public virtual Job? Job { get; set; }


        public Guid? CreatedBy { get; set; }
        public DateTime? DateCreated { get; set; }
        public Guid? ModifiedBy { get; set; }
        public DateTime? DateModified { get; set; }
        public RowStatus RowState { get; set; }
    }
}
