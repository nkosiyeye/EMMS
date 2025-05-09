using System.ComponentModel.DataAnnotations;

namespace EMMS.Models
{
    public class RecieveAsset
    {
        [Required]
        [Display(Name = "Asset Tag")]
        public string AssetId { get; set; }

        [Required]
        [Display(Name = "Date Received")]
        public DateTime DateReceived { get; set; }

        [Required]
        [Display(Name = "Condition")]
        public string Condition { get; set; }

        [Required]
        [Display(Name = "Received By")]
        public string ReceivedBy { get; set; }

        [Display(Name = "Remarks")]
        public string Remarks { get; set; }
    }
}

