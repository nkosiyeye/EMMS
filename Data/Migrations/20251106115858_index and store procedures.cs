using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EMMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class indexandstoreprocedures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "SerialNumber",
                table: "Assets",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_WorkRequest_WorkRequestId",
                table: "WorkRequest",
                column: "WorkRequestId",
                unique: true);

            //migrationBuilder.CreateIndex(
            //    name: "IX_User_UserId",
            //    table: "User",
            //    column: "UserId",
            //    unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId",
                table: "Notifications",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Job_JobId",
                table: "Job",
                column: "JobId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InfrustructureWorkRequest_WorkRequestId",
                table: "InfrustructureWorkRequest",
                column: "WorkRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Facilities_FacilityId",
                table: "Facilities",
                column: "FacilityId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Assets_AssetId",
                table: "Assets",
                column: "AssetId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Assets_DateCreated",
                table: "Assets",
                column: "DateCreated");

            migrationBuilder.CreateIndex(
                name: "IX_Assets_RowState",
                table: "Assets",
                column: "RowState");

            migrationBuilder.CreateIndex(
                name: "IX_Assets_SerialNumber",
                table: "Assets",
                column: "SerialNumber");

            migrationBuilder.CreateIndex(
                name: "IX_AssetMovement_MovementDate",
                table: "AssetMovement",
                column: "MovementDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkRequest_WorkRequestId",
                table: "WorkRequest");

            migrationBuilder.DropIndex(
                name: "IX_User_UserId",
                table: "User");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_UserId",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Job_JobId",
                table: "Job");

            migrationBuilder.DropIndex(
                name: "IX_InfrustructureWorkRequest_WorkRequestId",
                table: "InfrustructureWorkRequest");

            migrationBuilder.DropIndex(
                name: "IX_Facilities_FacilityId",
                table: "Facilities");

            migrationBuilder.DropIndex(
                name: "IX_Assets_AssetId",
                table: "Assets");

            migrationBuilder.DropIndex(
                name: "IX_Assets_DateCreated",
                table: "Assets");

            migrationBuilder.DropIndex(
                name: "IX_Assets_RowState",
                table: "Assets");

            migrationBuilder.DropIndex(
                name: "IX_Assets_SerialNumber",
                table: "Assets");

            migrationBuilder.DropIndex(
                name: "IX_AssetMovement_MovementDate",
                table: "AssetMovement");

            migrationBuilder.AlterColumn<string>(
                name: "SerialNumber",
                table: "Assets",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");
        }
    }
}
