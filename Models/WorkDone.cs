using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using EMMS.Models.Admin;
using EMMS.Models.Domain;
using static EMMS.Models.Enumerators;

namespace EMMS.Models
{
    public class WorkDone : BaseEntity
    {
        [Key]
        public Guid WorkDoneId { get; set; }

        
        [Display(Name = "Job ID")]
        public Guid? JobId { get; set; }

        // The Biomed who did this; stays with the entry when the job is reassigned.
        [Display(Name = "Biomed")]
        public Guid? BiomedId { get; set; }
        [ForeignKey(nameof(BiomedId))]
        public virtual User? Biomed { get; set; }

        [Required]
        [Display(Name = "Date Completed")]
        public DateTime DateCompleted { get; set; }

        [Required]
        [Display(Name = "Details")]
        public string? Details { get; set; }


        [Display(Name = "Hours")]
        public int? Hours { get; set; }

        public Guid? CreatedBy { get; set; }
        public DateTime? DateCreated { get; set; }
        public Guid? ModifiedBy { get; set; }
        public DateTime? DateModified { get; set; }
        public RowStatus RowState { get; set; }

    }
}
