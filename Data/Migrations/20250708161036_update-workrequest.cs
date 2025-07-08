using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EMMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class updateworkrequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkRequest_LookupItems_FaultReportId",
                table: "WorkRequest");

            migrationBuilder.DropIndex(
                name: "IX_WorkRequest_FaultReportId",
                table: "WorkRequest");

            migrationBuilder.DropColumn(
                name: "FaultReportId",
                table: "WorkRequest");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FaultReportId",
                table: "WorkRequest",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_WorkRequest_FaultReportId",
                table: "WorkRequest",
                column: "FaultReportId");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkRequest_LookupItems_FaultReportId",
                table: "WorkRequest",
                column: "FaultReportId",
                principalTable: "LookupItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
