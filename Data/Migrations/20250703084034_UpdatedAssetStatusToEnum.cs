using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EMMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdatedAssetStatusToEnum : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Assets_LookupItems_StatusId",
                table: "Assets");

            migrationBuilder.DropIndex(
                name: "IX_Assets_StatusId",
                table: "Assets");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Assets_StatusId",
                table: "Assets",
                column: "StatusId");

            migrationBuilder.AddForeignKey(
                name: "FK_Assets_LookupItems_StatusId",
                table: "Assets",
                column: "StatusId",
                principalTable: "LookupItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
