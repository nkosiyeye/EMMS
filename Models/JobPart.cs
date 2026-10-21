using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using EMMS.Models.Admin;
using EMMS.Models.Domain;
using EMMS.Models.Entities;
using static EMMS.Models.Enumerators;

namespace EMMS.Models
{
    /// <summary>A spare part used on a job. The name is copied from the SparePart lookup (or typed) so history survives lookup edits.</summary>
    public class JobPart : BaseEntity
    {
        [Key]
        public Guid JobPartId { get; set; }

        [Display(Name = "Job ID")]
        public Guid JobId { get; set; }

        // The Biomed who did this; stays with the entry when the job is reassigned.
        [Display(Name = "Biomed")]
        public Guid? BiomedId { get; set; }
        [ForeignKey(nameof(BiomedId))]
        public virtual User? Biomed { get; set; }

        [Display(Name = "Part")]
        public int? PartId { get; set; }
        [ForeignKey(nameof(PartId))]
        public virtual LookupItem? Part { get; set; }

        [Display(Name = "Part Name")]
        [StringLength(200)]
        public string? PartName { get; set; }

        [Required]
        [Display(Name = "Quantity")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Quantity { get; set; }

        [Required]
        [Display(Name = "Unit Cost")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitCost { get; set; }

        [NotMapped]
        public decimal LineTotal => Quantity * UnitCost;

        public Guid? CreatedBy { get; set; }
        public DateTime? DateCreated { get; set; }
        public Guid? ModifiedBy { get; set; }
        public DateTime? DateModified { get; set; }
        public RowStatus RowState { get; set; }
    }
}
