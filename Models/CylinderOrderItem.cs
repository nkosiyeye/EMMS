using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EMMS.Models
{
    /// <summary>One dispatched cylinder; MovementId points at the pre-approved movement that proves it.</summary>
    public class CylinderOrderItem
    {
        [Key]
        public Guid OrderItemId { get; set; }

        public Guid OrderId { get; set; }
        [ForeignKey(nameof(OrderId))]
        public virtual CylinderOrder? Order { get; set; }

        public Guid AssetId { get; set; }
        [ForeignKey(nameof(AssetId))]
        public virtual Asset? Asset { get; set; }

        public Guid? MovementId { get; set; }
        [ForeignKey(nameof(MovementId))]
        public virtual MoveAsset? Movement { get; set; }
    }
}
