using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EMMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AssetLocationViews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_AssetMovement_AssetId_DateCreated_MovementDate",
                table: "AssetMovement",
                columns: new[] { "AssetId", "DateCreated", "MovementDate" });

            // The single definition of "current location": the latest RECEIVED movement of each asset.
            // Replaces the ad-hoc definitions that ordered by MovementDate or counted pending moves.
            migrationBuilder.Sql(@"
CREATE VIEW dbo.vw_AssetCurrentLocation AS
SELECT x.AssetId, x.MovementId, x.FacilityId, x.ServicePointId, x.PatientId,
       x.MovementType, x.Reason, x.FunctionalStatus, x.DateReceived
FROM (
    SELECT m.*, ROW_NUMBER() OVER (PARTITION BY m.AssetId ORDER BY m.DateCreated DESC, m.MovementDate DESC) AS rn
    FROM dbo.AssetMovement m
    WHERE m.RowState = 1 AND m.DateReceived IS NOT NULL
) x
WHERE x.rn = 1;");

            // In transit: the asset's LATEST movement is neither received nor rejected (awaiting approval or on its way).
            migrationBuilder.Sql(@"
CREATE VIEW dbo.vw_AssetInTransit AS
SELECT x.AssetId, x.MovementId, x.FromId, x.FacilityId, x.ServicePointId, x.IsApproved, x.BatchId, x.MovementDate
FROM (
    SELECT m.*, ROW_NUMBER() OVER (PARTITION BY m.AssetId ORDER BY m.DateCreated DESC, m.MovementDate DESC) AS rn
    FROM dbo.AssetMovement m
    WHERE m.RowState = 1
) x
WHERE x.rn = 1 AND x.DateReceived IS NULL AND x.DateRejected IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS dbo.vw_AssetInTransit;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS dbo.vw_AssetCurrentLocation;");

            migrationBuilder.DropIndex(
                name: "IX_AssetMovement_AssetId_DateCreated_MovementDate",
                table: "AssetMovement");
        }
    }
}
