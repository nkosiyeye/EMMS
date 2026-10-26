using EMMS.Data;
using EMMS.Models;
using static EMMS.Models.Enumerators;

namespace EMMS.Service
{
    /// <summary>
    /// Asset sets shared by the dashboard and the service schedule, so their counts always agree.
    /// </summary>
    public static class AssetScopes
    {
        // Active assets that are not decommissioned or lost; at a facility = its current (received) location is there.
        public static IQueryable<Asset> InService(ApplicationDbContext context, int? facilityId)
        {
            var lost = context.AssetCurrentLocations
                .Where(l => l.Reason == MovementReason.Decommission || l.Reason == MovementReason.StolenorMissing)
                .Select(l => l.AssetId);
            var assets = context.Assets
                .Where(a => a.RowState == RowStatus.Active
                         && a.StatusId != (int)ProcurementStatus.Decommissioned
                         && !lost.Contains(a.AssetId));
            if (facilityId == null) return assets;

            var here = context.AssetCurrentLocations.Where(l => l.FacilityId == facilityId).Select(l => l.AssetId);
            return assets.Where(a => here.Contains(a.AssetId));
        }

        /// <summary>In-service assets flagged serviceable, with or without a next service date.</summary>
        public static IQueryable<Asset> Serviceable(ApplicationDbContext context, int? facilityId) =>
            InService(context, facilityId).Where(a => a.IsServiceable);

        /// <summary>Serviceable assets that have a next service date.</summary>
        public static IQueryable<Asset> Scheduled(ApplicationDbContext context, int? facilityId) =>
            Serviceable(context, facilityId).Where(a => a.NextServiceDate != null);
    }
}
