using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EMMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AssetTagSequence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Guard: a unique index cannot be created over duplicate tags. Stop with a clear message instead of
            // silently re-tagging assets that may already carry a printed label. See Docs/CylinderTracking/fix-duplicate-tags.sql.
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM Assets GROUP BY AssetTagNumber HAVING COUNT(*) > 1)
    THROW 50001, 'Duplicate AssetTagNumber values exist. Run Docs/CylinderTracking/fix-duplicate-tags.sql, then re-run the migration.', 1;
IF EXISTS (SELECT 1 FROM Assets WHERE LEN(AssetTagNumber) > 50)
    THROW 50002, 'AssetTagNumber values longer than 50 characters exist; shorten them before re-running the migration.', 1;");

            migrationBuilder.AlterColumn<string>(
                name: "AssetTagNumber",
                table: "Assets",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_Assets_AssetTagNumber",
                table: "Assets",
                column: "AssetTagNumber",
                unique: true);

            // Start above the highest numeric suffix in use ({code}AS-{n}) so new tags never collide with existing ones.
            migrationBuilder.Sql(@"
DECLARE @start bigint = 1 + ISNULL((
    SELECT MAX(TRY_CAST(CASE WHEN CHARINDEX('-', AssetTagNumber) > 0
                             THEN RIGHT(AssetTagNumber, CHARINDEX('-', REVERSE(AssetTagNumber)) - 1) END AS bigint))
    FROM Assets), 0);
DECLARE @sql nvarchar(200) = N'CREATE SEQUENCE dbo.AssetTagSeq AS bigint START WITH ' + CAST(@start AS nvarchar(20)) + N' INCREMENT BY 1 NO CACHE;';
EXEC sys.sp_executesql @sql;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Assets_AssetTagNumber",
                table: "Assets");

            migrationBuilder.DropSequence(
                name: "AssetTagSeq");

            migrationBuilder.AlterColumn<string>(
                name: "AssetTagNumber",
                table: "Assets",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);
        }
    }
}
