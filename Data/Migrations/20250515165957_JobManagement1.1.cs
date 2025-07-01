using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EMMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class JobManagement11 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkRequest_LookupItems_OutcomeId",
                table: "WorkRequest");

            migrationBuilder.AlterColumn<int>(
                name: "OutcomeId",
                table: "WorkRequest",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "FacilityId",
                table: "WorkRequest",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "Remarks",
                table: "Job",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<int>(
                name: "FacilityId",
                table: "Job",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_WorkRequest_FacilityId",
                table: "WorkRequest",
                column: "FacilityId");

            migrationBuilder.CreateIndex(
                name: "IX_Job_FacilityId",
                table: "Job",
                column: "FacilityId");

            migrationBuilder.AddForeignKey(
                name: "FK_Job_Facilities_FacilityId",
                table: "Job",
                column: "FacilityId",
                principalTable: "Facilities",
                principalColumn: "FacilityId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkRequest_Facilities_FacilityId",
                table: "WorkRequest",
                column: "FacilityId",
                principalTable: "Facilities",
                principalColumn: "FacilityId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkRequest_LookupItems_OutcomeId",
                table: "WorkRequest",
                column: "OutcomeId",
                principalTable: "LookupItems",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Job_Facilities_FacilityId",
                table: "Job");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkRequest_Facilities_FacilityId",
                table: "WorkRequest");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkRequest_LookupItems_OutcomeId",
                table: "WorkRequest");

            migrationBuilder.DropIndex(
                name: "IX_WorkRequest_FacilityId",
                table: "WorkRequest");

            migrationBuilder.DropIndex(
                name: "IX_Job_FacilityId",
                table: "Job");

            migrationBuilder.DropColumn(
                name: "FacilityId",
                table: "WorkRequest");

            migrationBuilder.DropColumn(
                name: "FacilityId",
                table: "Job");

            migrationBuilder.AlterColumn<int>(
                name: "OutcomeId",
                table: "WorkRequest",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Remarks",
                table: "Job",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkRequest_LookupItems_OutcomeId",
                table: "WorkRequest",
                column: "OutcomeId",
                principalTable: "LookupItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
