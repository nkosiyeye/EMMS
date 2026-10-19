using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EMMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class CylinderRegistration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsCentralStore",
                table: "Facilities",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "AssetCylinderDetails",
                columns: table => new
                {
                    AssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SizeId = table.Column<int>(type: "int", nullable: false),
                    ContentId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetCylinderDetails", x => x.AssetId);
                    table.ForeignKey(
                        name: "FK_AssetCylinderDetails_Assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Assets",
                        principalColumn: "AssetId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AssetCylinderDetails_LookupItems_ContentId",
                        column: x => x.ContentId,
                        principalTable: "LookupItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssetCylinderDetails_LookupItems_SizeId",
                        column: x => x.SizeId,
                        principalTable: "LookupItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssetCylinderDetails_ContentId",
                table: "AssetCylinderDetails",
                column: "ContentId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetCylinderDetails_SizeId",
                table: "AssetCylinderDetails",
                column: "SizeId");

            // Lookup seeds. Idempotent: an environment that already created any of these keeps its rows,
            // and only a missing flag is filled in. Size / content values are starting points; admins maintain them.
            migrationBuilder.Sql(@"
DECLARE @now datetime2 = SYSDATETIME();

IF NOT EXISTS (SELECT 1 FROM LookupLists WHERE Name = N'Cylinder Size')
    INSERT INTO LookupLists (Name, DateCreated, RowState) VALUES (N'Cylinder Size', @now, 1);
IF NOT EXISTS (SELECT 1 FROM LookupLists WHERE Name = N'Cylinder Content')
    INSERT INTO LookupLists (Name, DateCreated, RowState) VALUES (N'Cylinder Content', @now, 1);

DECLARE @categoryList int     = (SELECT TOP 1 LookupListId FROM LookupLists WHERE Name = N'Category');
DECLARE @subCategoryList int  = (SELECT TOP 1 LookupListId FROM LookupLists WHERE Name = N'SubCategory');
DECLARE @servicePointList int = (SELECT TOP 1 LookupListId FROM LookupLists WHERE Name = N'Service Point');
DECLARE @sizeList int         = (SELECT TOP 1 LookupListId FROM LookupLists WHERE Name = N'Cylinder Size');
DECLARE @contentList int      = (SELECT TOP 1 LookupListId FROM LookupLists WHERE Name = N'Cylinder Content');

-- Category 'Cylinder' (top-level categories use ParentId = 0)
IF @categoryList IS NOT NULL AND NOT EXISTS (SELECT 1 FROM LookupItems WHERE LookupListId = @categoryList AND Name = N'Cylinder')
    INSERT INTO LookupItems (LookupListId, Name, SortIndex, RowState, ParentId, FlagsJson, DateCreated)
    VALUES (@categoryList, N'Cylinder', (SELECT ISNULL(MAX(SortIndex), 0) + 1 FROM LookupItems WHERE LookupListId = @categoryList), 1, 0, N'{""CylinderProfile"":true}', @now);
UPDATE LookupItems SET FlagsJson = N'{""CylinderProfile"":true}'
WHERE LookupListId = @categoryList AND Name = N'Cylinder' AND (FlagsJson IS NULL OR FlagsJson IN (N'', N'{}'));

DECLARE @cylinder int = (SELECT TOP 1 Id FROM LookupItems WHERE LookupListId = @categoryList AND Name = N'Cylinder' ORDER BY Id);

-- SubCategory 'Oxygen Cylinder' under Cylinder
IF @subCategoryList IS NOT NULL AND @cylinder IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM LookupItems WHERE LookupListId = @subCategoryList AND Name = N'Oxygen Cylinder' AND ParentId = @cylinder)
    INSERT INTO LookupItems (LookupListId, Name, SortIndex, RowState, ParentId, FlagsJson, DateCreated)
    VALUES (@subCategoryList, N'Oxygen Cylinder', 1, 1, @cylinder, N'{""RequiresSerial"":true}', @now);

-- Service Point 'Home Based Care' (used from Phase 2: movement captures the patient)
IF @servicePointList IS NOT NULL AND NOT EXISTS (SELECT 1 FROM LookupItems WHERE LookupListId = @servicePointList AND Name = N'Home Based Care')
    INSERT INTO LookupItems (LookupListId, Name, SortIndex, RowState, ParentId, FlagsJson, DateCreated)
    VALUES (@servicePointList, N'Home Based Care', (SELECT ISNULL(MAX(SortIndex), 0) + 1 FROM LookupItems WHERE LookupListId = @servicePointList), 1, NULL, N'{""RequiresPatient"":true}', @now);
UPDATE LookupItems SET FlagsJson = N'{""RequiresPatient"":true}'
WHERE LookupListId = @servicePointList AND Name = N'Home Based Care' AND (FlagsJson IS NULL OR FlagsJson IN (N'', N'{}'));

-- Sizes and contents (only when the list is still empty)
IF NOT EXISTS (SELECT 1 FROM LookupItems WHERE LookupListId = @sizeList)
    INSERT INTO LookupItems (LookupListId, Name, SortIndex, RowState, ParentId, DateCreated)
    VALUES (@sizeList, N'2 L', 1, 1, NULL, @now), (@sizeList, N'5 L', 2, 1, NULL, @now),
           (@sizeList, N'10 L', 3, 1, NULL, @now), (@sizeList, N'47 L', 4, 1, NULL, @now);
IF NOT EXISTS (SELECT 1 FROM LookupItems WHERE LookupListId = @contentList)
    INSERT INTO LookupItems (LookupListId, Name, SortIndex, RowState, ParentId, DateCreated)
    VALUES (@contentList, N'Oxygen', 1, 1, NULL, @now), (@contentList, N'Medical Air', 2, 1, NULL, @now);

-- Central Medical Stores: flag it only if a matching facility already exists (none is created here)
IF NOT EXISTS (SELECT 1 FROM Facilities WHERE IsCentralStore = 1)
    UPDATE Facilities SET IsCentralStore = 1
    WHERE FacilityId = (SELECT TOP 1 FacilityId FROM Facilities
                        WHERE RowState = 1 AND (FacilityCode = N'CMS' OR FacilityName LIKE N'Central Medical Store%')
                        ORDER BY FacilityId);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Seeded lookups are left in place: assets may reference them and LookupItems deletes can cascade.
            migrationBuilder.DropTable(
                name: "AssetCylinderDetails");

            migrationBuilder.DropColumn(
                name: "IsCentralStore",
                table: "Facilities");
        }
    }
}
