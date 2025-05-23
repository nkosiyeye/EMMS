using EMMS.Models.Domain;
using static EMMS.Models.Enumerators;

namespace EMMS.Models.Entities
{
    public class LookupList : BaseEntity
    {
        public int LookupListId { get; set; }
        public string? Name { get; set; }
        public Guid? CreatedBy { get; set; }
        public DateTime? DateCreated { get; set; }
        public Guid? ModifiedBy { get; set; }
        public DateTime? DateModified { get; set; }
        public RowStatus RowState { get; set; }

    }
}
