using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EMMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class parent_facility_lookupitem_column : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ParentFacilityId",
                table: "LookupItems",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LookupItems_ParentFacilityId",
                table: "LookupItems",
                column: "ParentFacilityId");

            migrationBuilder.AddForeignKey(
                name: "FK_LookupItems_Facilities_ParentFacilityId",
                table: "LookupItems",
                column: "ParentFacilityId",
                principalTable: "Facilities",
                principalColumn: "FacilityId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LookupItems_Facilities_ParentFacilityId",
                table: "LookupItems");

            migrationBuilder.DropIndex(
                name: "IX_LookupItems_ParentFacilityId",
                table: "LookupItems");

            migrationBuilder.DropColumn(
                name: "ParentFacilityId",
                table: "LookupItems");
        }
    }
}
