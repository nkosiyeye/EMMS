using System.ComponentModel.DataAnnotations;
using static EMMS.Models.Enumerators;

namespace EMMS.Models
{
    public class ExternalWorkDone
    {
        [Key]
        public Guid ExternalWorkDoneId { get; set; }
        [Required]
        [Display(Name = "Job ID")]
        public int? JobId { get; set; }

        [Required]
        [Display(Name = "Date Completed")]
        public DateTime DateCompleted { get; set; }

        [Required]
        [Display(Name = "Details")]
        public string? Details { get; set; }


        [Display(Name = "Amount")]
        public decimal? Amount { get; set; }

        [Display(Name = "Invoice No")]
        public string? InvoiceNo { get; set; }

        public Guid? CreatedBy { get; set; }
        public DateTime? DateCreated { get; set; }
        public Guid? ModifiedBy { get; set; }
        public DateTime? DateModified { get; set; }
        public RowStatus RowState { get; set; }
    }
}
