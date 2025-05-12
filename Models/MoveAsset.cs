using System.ComponentModel.DataAnnotations;

namespace EMMS.Models
{
    public class MoveAsset
    {
        [Key]
        [Display(Name = "Movement ID")]
        public string MovementId { get; set; }

        [Required]
        [Display(Name = "Movement Date")]
        public DateTime MovementDate { get; set; }

        [Required]
        [Display(Name = "Asset Tag")]
        public string AssetId { get; set; }

        [Required]
        [Display(Name = "Movement Type")]
        public string MovementType { get; set; }

        [Required]
        [Display(Name = "From")]
        public string From { get; set; }

        [Display(Name = "Facility")]
        public string Facility { get; set; }

        [Display(Name = "Service Point")]
        public string ServicePoint { get; set; }

        [Required]
        [Display(Name = "Reason")]
        public string Reason { get; set; }

        [Required]
        [Display(Name = "Functional Status")]
        public string FunctionalStatus { get; set; }

        [Display(Name = "Approved")]
        public bool IsApproved { get; set; }

        [Required]
        [Display(Name = "Date Received")]
        public DateTime? DateReceived { get; set; }

        [Required]
        [Display(Name = "Condition")]
        public string? Condition { get; set; }

        [Required]
        [Display(Name = "Received By")]
        public string? ReceivedBy { get; set; }

        [Display(Name = "Remarks")]
        public string? Remarks { get; set; }

    }
}

