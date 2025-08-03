using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EMMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class cancelJobReason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CancelReasonId",
                table: "WorkRequest",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkRequest_CancelReasonId",
                table: "WorkRequest",
                column: "CancelReasonId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkRequest_RequestedBy",
                table: "WorkRequest",
                column: "RequestedBy");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkRequest_LookupItems_CancelReasonId",
                table: "WorkRequest",
                column: "CancelReasonId",
                principalTable: "LookupItems",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkRequest_User_RequestedBy",
                table: "WorkRequest",
                column: "RequestedBy",
                principalTable: "User",
                principalColumn: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkRequest_LookupItems_CancelReasonId",
                table: "WorkRequest");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkRequest_User_RequestedBy",
                table: "WorkRequest");

            migrationBuilder.DropIndex(
                name: "IX_WorkRequest_CancelReasonId",
                table: "WorkRequest");

            migrationBuilder.DropIndex(
                name: "IX_WorkRequest_RequestedBy",
                table: "WorkRequest");

            migrationBuilder.DropColumn(
                name: "CancelReasonId",
                table: "WorkRequest");
        }
    }
}
