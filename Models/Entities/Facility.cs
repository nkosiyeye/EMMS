using static EMMS.Models.Enumerators;

namespace EMMS.Models.Entities
{
    public class Facility
    {
        public int FacilityId { get; set; }
        public string? FacilityName { get; set; }
        public string? FacilityCode { get; set; }
        public bool? isOffSite { get; set; }
        public Guid? CreatedBy { get; set; }
        public DateTime? DateCreated { get; set; }
        public Guid? ModifiedBy { get; set; }
        public DateTime? DateModified { get; set; }
        public RowStatus RowState { get; set; }
    }
}
