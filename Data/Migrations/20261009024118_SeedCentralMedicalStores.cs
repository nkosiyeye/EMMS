using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EMMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeedCentralMedicalStores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Creates the Central Medical Stores facility (default initial location for cylinders and,
            // from Phase 3, the source of all dispatches). Idempotent: an existing CMS facility is reused
            // and only flagged; a new one is inserted only when none exists.
            migrationBuilder.Sql(@"
DECLARE @now datetime2 = SYSDATETIME();

IF NOT EXISTS (SELECT 1 FROM Facilities WHERE IsCentralStore = 1)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM Facilities
                   WHERE RowState = 1 AND (FacilityCode = N'CMS' OR FacilityName LIKE N'Central Medical Store%'))
        INSERT INTO Facilities (FacilityName, FacilityCode, isOffSite, IsCentralStore, DateCreated, RowState)
        VALUES (N'Central Medical Stores', N'CMS', 0, 1, @now, 1);
    ELSE
        UPDATE Facilities SET IsCentralStore = 1, DateModified = @now
        WHERE FacilityId = (SELECT TOP 1 FacilityId FROM Facilities
                            WHERE RowState = 1 AND (FacilityCode = N'CMS' OR FacilityName LIKE N'Central Medical Store%')
                            ORDER BY FacilityId);
END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The facility is left in place: assets, movements and users may reference it by then.
        }
    }
}
