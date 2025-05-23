using static EMMS.Models.Enumerators;

namespace EMMS.Models.Domain
{
    
    public interface BaseEntity
    {
        Guid? CreatedBy { get; set; }
        DateTime? DateCreated { get; set; }
        Guid? ModifiedBy { get; set; }
        DateTime? DateModified { get; set; }
        RowStatus RowState { get; set; }
    }
}
