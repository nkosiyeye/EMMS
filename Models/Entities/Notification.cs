using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using EMMS.Models.Domain;
using static EMMS.Models.Enumerators;

namespace EMMS.Models.Entities
{
    public class Notification: BaseEntity
    {
        [Key]
        public int Id { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public DateTime? DateCreated { get; set; } = DateTime.Now;
        public bool IsRead { get; set; } = false;
        public Guid? UserId { get; set; } // Optional: for user-specific notifications
        public int? FacilityId { get; set; }
        [ForeignKey(nameof(FacilityId))]
        public virtual Facility? Facility { get; set; }
        public Guid? CreatedBy { get; set; }
        public Guid? ModifiedBy { get; set; }
        public DateTime? DateModified { get; set; }
        public RowStatus RowState { get; set; }
    }
}
