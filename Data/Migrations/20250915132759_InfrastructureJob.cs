using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EMMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class InfrastructureJob : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Job_WorkRequest_WorkRequestId",
                table: "Job");

            migrationBuilder.AlterColumn<Guid>(
                name: "WorkRequestId",
                table: "Job",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(
                name: "InfrastructureWorkRequestId",
                table: "Job",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Job_InfrastructureWorkRequestId",
                table: "Job",
                column: "InfrastructureWorkRequestId",
                unique: true,
                filter: "[InfrastructureWorkRequestId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Job_InfrustructureWorkRequest_InfrastructureWorkRequestId",
                table: "Job",
                column: "InfrastructureWorkRequestId",
                principalTable: "InfrustructureWorkRequest",
                principalColumn: "WorkRequestId");

            migrationBuilder.AddForeignKey(
                name: "FK_Job_WorkRequest_WorkRequestId",
                table: "Job",
                column: "WorkRequestId",
                principalTable: "WorkRequest",
                principalColumn: "WorkRequestId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Job_InfrustructureWorkRequest_InfrastructureWorkRequestId",
                table: "Job");

            migrationBuilder.DropForeignKey(
                name: "FK_Job_WorkRequest_WorkRequestId",
                table: "Job");

            migrationBuilder.DropIndex(
                name: "IX_Job_InfrastructureWorkRequestId",
                table: "Job");

            migrationBuilder.DropColumn(
                name: "InfrastructureWorkRequestId",
                table: "Job");

            migrationBuilder.AlterColumn<Guid>(
                name: "WorkRequestId",
                table: "Job",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Job_WorkRequest_WorkRequestId",
                table: "Job",
                column: "WorkRequestId",
                principalTable: "WorkRequest",
                principalColumn: "WorkRequestId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
