using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EMMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class asset : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ServicePeriodId",
                table: "Assets",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Assets_ServicePeriodId",
                table: "Assets",
                column: "ServicePeriodId");

            migrationBuilder.AddForeignKey(
                name: "FK_Assets_LookupItems_ServicePeriodId",
                table: "Assets",
                column: "ServicePeriodId",
                principalTable: "LookupItems",
                principalColumn: "Id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Assets_LookupItems_ServicePeriodId",
                table: "Assets");

            migrationBuilder.DropIndex(
                name: "IX_Assets_ServicePeriodId",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "ServicePeriodId",
                table: "Assets");
        }
    }
}
