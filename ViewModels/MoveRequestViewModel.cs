using EMMS.Models;
using EMMS.Models.Entities;

namespace EMMS.ViewModels
{
    public class MoveRequestViewModel
    {
        public string? AssetTag { get; set; }
        public MoveAsset? MoveAsset { get; set; }
        public Facility? CurrentFacility { get; set; }

        public DateTime? WarrantyEndDate { get; set; }

        public IEnumerable<Enumerators.MovementType>? MovementTypes { get; set; } = Enum.GetValues<Enumerators.MovementType>();
        public IEnumerable<Facility>? Facilities { get; set; }
        public IEnumerable<LookupItem>? ServicePoints { get; set; }
        public IEnumerable<LookupItem>? Reasons { get; set; }
        public IEnumerable<LookupItem>? FunctionalStatuses { get; set; }
        public IEnumerable<Facility>? FromFacilities { get; set; }
    }
}
