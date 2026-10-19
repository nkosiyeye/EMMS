using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using EMMS.Models.Admin;
using EMMS.Models.Domain;
using EMMS.Models.Entities;
using static EMMS.Models.Enumerators;

namespace EMMS.Models
{
    /// <summary>
    /// An asset scanned (or ticked) into a user's movement queue. An asset can be in one queue only
    /// (unique AssetId). Submitting the queue turns every item into an AssetMovement sharing one BatchId.
    /// </summary>
    public class MovementQueueItem : BaseEntity
    {
        [Key]
        public Guid QueueItemId { get; set; }

        public Guid UserId { get; set; }
        [ForeignKey(nameof(UserId))]
        public virtual User? User { get; set; }

        public Guid AssetId { get; set; }
        [ForeignKey(nameof(AssetId))]
        public virtual Asset? Asset { get; set; }

        // Where the asset was when it was scanned; every item in one queue shares the same source
        public int SourceFacilityId { get; set; }
        [ForeignKey(nameof(SourceFacilityId))]
        public virtual Facility? SourceFacility { get; set; }

        public DateTime AddedAt { get; set; }

        public Guid? CreatedBy { get; set; }
        public DateTime? DateCreated { get; set; }
        public Guid? ModifiedBy { get; set; }
        public DateTime? DateModified { get; set; }
        public RowStatus RowState { get; set; }
    }
}
