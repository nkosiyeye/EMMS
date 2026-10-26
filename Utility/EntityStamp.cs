using EMMS.Models.Domain;
using static EMMS.Models.Enumerators;

namespace EMMS.Utility
{
    /// <summary>Audit columns for new and changed rows. BaseController and the services both stamp through here.</summary>
    public static class EntityStamp
    {
        public static void Created(BaseEntity entity, Guid? userId)
        {
            entity.CreatedBy = userId;
            entity.DateCreated = DateTime.Now;
            entity.RowState = RowStatus.Active;
        }

        public static void Updated(BaseEntity entity, Guid? userId)
        {
            entity.ModifiedBy = userId;
            entity.DateModified = DateTime.Now;
            entity.RowState = RowStatus.Active;
        }
    }
}
