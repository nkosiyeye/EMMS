using System.ComponentModel.DataAnnotations;

namespace EMMS.Models
{
    public class WorkRequest
    {
        [Key]
        public int WorkRequestId { get; set; }

        [Required]
        [Display(Name = "Asset ID")]
        public string AssetId { get; set; }

        [Required]
        [Display(Name = "Request Date")]
        public DateTime RequestDate { get; set; }

        [Required]
        [Display(Name = "Title")]
        public string Title { get; set; }

        [Required]
        [Display(Name = "Description")]
        public string Description { get; set; }

        [Display(Name = "Status")]
        public string Status { get; set; }

        [Display(Name = "Outcome")]
        public string Outcome { get; set; }

        [Display(Name = "Requested By")]
        public string RequestedBy { get; set; }
    }
}
