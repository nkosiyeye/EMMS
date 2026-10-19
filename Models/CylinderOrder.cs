using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using EMMS.Models.Admin;
using EMMS.Models.Domain;
using static EMMS.Models.Enumerators;

namespace EMMS.Models
{
    /// <summary>CMS order raised from an approved request; dispatched through the Movement Queue.</summary>
    public class CylinderOrder : BaseEntity
    {
        [Key]
        public Guid OrderId { get; set; }

        public Guid RequestId { get; set; }
        [ForeignKey(nameof(RequestId))]
        public virtual CylinderRequest? Request { get; set; }

        public Guid OrderedBy { get; set; }
        [ForeignKey(nameof(OrderedBy))]
        public virtual User? OrderedByUser { get; set; }

        [MaxLength(50)]
        [Display(Name = "Order Number")]
        public string OrderNumber { get; set; } = "";

        [Range(1, 10000)]
        public int Quantity { get; set; }

        public CylinderOrderStatus Status { get; set; }

        public virtual ICollection<CylinderOrderItem> Items { get; set; } = new List<CylinderOrderItem>();

        public Guid? CreatedBy { get; set; }
        public DateTime? DateCreated { get; set; }
        public Guid? ModifiedBy { get; set; }
        public DateTime? DateModified { get; set; }
        public RowStatus RowState { get; set; }
    }
}
