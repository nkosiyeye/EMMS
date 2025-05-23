using EMMS.Models.Domain;
using System.ComponentModel.DataAnnotations.Schema;
using static EMMS.Models.Enumerators;

namespace EMMS.Models.Entities
{
    public class LookupItem : BaseEntity
    {
        public int Id { get; set; }
        public int LookupListId { get; set; }
        [ForeignKey("LookupListId")]
        public virtual LookupList LookupList { get; set; }
        public string? Name { get; set; }
        public int SortIndex { get; set; }
        public Guid? CreatedBy { get; set; }
        public DateTime? DateCreated { get; set; }
        public Guid? ModifiedBy { get; set; }
        public DateTime? DateModified { get; set; }
        public RowStatus RowState { get; set; }
    }
}
