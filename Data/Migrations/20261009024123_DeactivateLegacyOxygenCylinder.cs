using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EMMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class DeactivateLegacyOxygenCylinder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Deactivates the older 'Oxygen Cylinder' subcategory under 'Medical Equipment' (Id 214 in the
            // reference data), now superseded by the one under 'Cylinder'. Matched by name and parent rather
            // than Id so it is safe in every environment, and skipped if any asset still uses it.
            migrationBuilder.Sql(@"
UPDATE sc SET sc.RowState = 2, sc.DateModified = SYSDATETIME()
FROM LookupItems sc
JOIN LookupLists scl ON scl.LookupListId = sc.LookupListId AND scl.Name = N'SubCategory'
JOIN LookupItems cat ON cat.Id = sc.ParentId AND cat.Name = N'Medical Equipment'
WHERE sc.Name = N'Oxygen Cylinder'
  AND sc.RowState = 1
  AND NOT EXISTS (SELECT 1 FROM Assets a WHERE a.SubCategoryId = sc.Id);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE sc SET sc.RowState = 1, sc.DateModified = SYSDATETIME()
FROM LookupItems sc
JOIN LookupLists scl ON scl.LookupListId = sc.LookupListId AND scl.Name = N'SubCategory'
JOIN LookupItems cat ON cat.Id = sc.ParentId AND cat.Name = N'Medical Equipment'
WHERE sc.Name = N'Oxygen Cylinder'
  AND sc.RowState = 2;");
        }
    }
}
