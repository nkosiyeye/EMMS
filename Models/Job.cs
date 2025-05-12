using System.ComponentModel.DataAnnotations;

namespace EMMS.Models
{
    public class Job
    {
        [Key]
        public int JobId { get; set; }
        [Required]
        [Display(Name = "Job Card Number")]
        public int JobCardNumber { get; set; }

        [Required]
        [Display(Name = "Work Request ID")]
        public int WorkRequestId { get; set; }

        [Required]
        [Display(Name = "Asset ID")]
        public string AssetId { get; set; }

        [Required]
        [Display(Name = "Assigned To")]
        public string AssignedTo { get; set; }

        [Required]
        [Display(Name = "Start Date")]
        public DateTime StartDate { get; set; }

        [Display(Name = "End Date")]
        public DateTime? EndDate { get; set; }

        [Display(Name = "FaultReport")]
        public string FaultReport { get; set; }


        [Display(Name = "Type Of Job")]
        public string JobType { get; set; }

        [Required]
        [Display(Name = "IsExternalProvider")]
        public bool IsExternalProvider { get; set; }

        [Display(Name = "External Provider")]
        public string ExternalProvider { get; set; }

        [Required]
        [Display(Name = "Status")]
        public string Status { get; set; }


        [Display(Name = "Remarks")]
        public string Remarks { get; set; }

        // Navigation property to link to the WorkRequest
        public WorkRequest WorkRequest { get; set; }
    }
}
