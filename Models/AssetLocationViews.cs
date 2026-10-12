using static EMMS.Models.Enumerators;

namespace EMMS.Models
{
    /// <summary>
    /// vw_AssetCurrentLocation: the canonical current location of an asset = its latest RECEIVED movement
    /// (ordered by DateCreated, then MovementDate). One row per asset that has a location.
    /// </summary>
    public class AssetCurrentLocation
    {
        public Guid AssetId { get; set; }
        public Guid MovementId { get; set; }
        public int FacilityId { get; set; }
        public int? ServicePointId { get; set; }
        public Guid? PatientId { get; set; }
        public MovementType MovementType { get; set; }
        public MovementReason Reason { get; set; }
        public FunctionalStatus FunctionalStatus { get; set; }
        public DateTime? DateReceived { get; set; }
    }

    /// <summary>
    /// vw_AssetInTransit: assets whose latest movement is still pending (awaiting approval or approved but
    /// not yet received or rejected). The asset's location stays the source until it is received.
    /// </summary>
    public class AssetInTransit
    {
        public Guid AssetId { get; set; }
        public Guid MovementId { get; set; }
        public int FromId { get; set; }
        public int FacilityId { get; set; }
        public int? ServicePointId { get; set; }
        public bool IsApproved { get; set; }
        public Guid? BatchId { get; set; }
        public DateTime MovementDate { get; set; }
    }
}
