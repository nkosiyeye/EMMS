using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using EMMS.Models.Admin;
using static EMMS.Models.Enumerators;

namespace EMMS.Models
{
    /// <summary>One change of an asset's functional status. Written only by <see cref="EMMS.Service.AssetStatusTracker"/>.</summary>
    public class AssetStatusChange
    {
        [Key]
        public Guid AssetStatusChangeId { get; set; }

        public Guid AssetId { get; set; }
        [ForeignKey(nameof(AssetId))]
        public virtual Asset? Asset { get; set; }

        [Display(Name = "From")]
        public FunctionalStatus? FromStatus { get; set; }

        [Display(Name = "To")]
        public FunctionalStatus ToStatus { get; set; }

        [Display(Name = "Source")]
        public StatusChangeSource Source { get; set; }

        // What caused the change, when it was a job or a movement.
        public Guid? JobId { get; set; }
        [ForeignKey(nameof(JobId))]
        public virtual Job? Job { get; set; }

        public Guid? MovementId { get; set; }
        [ForeignKey(nameof(MovementId))]
        public virtual MoveAsset? Movement { get; set; }

        [Display(Name = "Changed By")]
        public Guid? ChangedBy { get; set; }
        [ForeignKey(nameof(ChangedBy))]
        public virtual User? ChangedByUser { get; set; }

        [Display(Name = "Date")]
        public DateTime DateChanged { get; set; }
    }
}
