using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using EMMS.Models.Domain;
using EMMS.Models.Entities;
using static EMMS.Models.Enumerators;

namespace EMMS.Models
{
    public class Job : BaseEntity
    {
        [Key]
        [Display(Name = "Job Card Number")]
        public int JobId { get; set; }

        [Required]
        [Display(Name = "Work Request ID")]
        public Guid WorkRequestId { get; set; }
        [ForeignKey(nameof(WorkRequestId))]
        public virtual WorkRequest? WorkRequest { get; set; }

        [Required]
        [Display(Name = "Asset ID")]
        public Guid? AssetId { get; set; }
        [ForeignKey(nameof(AssetId))]
        public virtual Asset? Asset { get; set; }

        [Required]
        [Display(Name = "Assigned To")]
        public Guid? AssignedTo { get; set; }

        [Required]
        [Display(Name = "Start Date")]
        public DateTime StartDate { get; set; }

        [Display(Name = "End Date")]
        public DateTime? EndDate { get; set; }

        [Display(Name = "FaultReport")]
        public int FaultReportId { get; set; }
        [ForeignKey(nameof(FaultReportId))]
        public virtual LookupItem? FaultReport { get; set; }

        [Required]
        [Display(Name = "IsExternalProvider")]
        public bool IsExternalProvider { get; set; }

        [Display(Name = "External Provider")]
        public int? ExternalProviderId { get; set; }
        [ForeignKey(nameof(ExternalProviderId))]
        public virtual LookupItem? ExternalProvider { get; set; }

        [Required]
        [Display(Name = "Status")]
        public int StatusId { get; set; }
        [ForeignKey(nameof(StatusId))]
        public virtual LookupItem? Status { get; set; }


        [Display(Name = "Remarks")]
        public string? Remarks { get; set; }

        public int FacilityId { get; set; }
        [ForeignKey(nameof(FacilityId))]
        public virtual Facility? Facility { get; set; }
        public Guid? CreatedBy { get; set; }
        public DateTime? DateCreated { get; set; }
        public Guid? ModifiedBy { get; set; }
        public DateTime? DateModified { get; set; }
        public RowStatus RowState { get; set; }
    }
}
