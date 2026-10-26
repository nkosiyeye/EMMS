using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EMMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class JobEntryBiomed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BiomedId",
                table: "WorkDone",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BiomedId",
                table: "JobPart",
                type: "uniqueidentifier",
                nullable: true);

            // Existing entries belong to whoever recorded them (only the assignee could), else the job's assignee.
            foreach (var table in new[] { "WorkDone", "JobPart" })
                migrationBuilder.Sql($@"
UPDATE e SET BiomedId = COALESCE(c.UserId, a.UserId)
FROM [{table}] e
JOIN Job j ON j.JobId = e.JobId
LEFT JOIN [User] c ON c.UserId = e.CreatedBy
LEFT JOIN [User] a ON a.UserId = j.AssignedTo
WHERE e.BiomedId IS NULL;");

            migrationBuilder.CreateIndex(
                name: "IX_WorkDone_BiomedId",
                table: "WorkDone",
                column: "BiomedId");

            migrationBuilder.CreateIndex(
                name: "IX_JobPart_BiomedId",
                table: "JobPart",
                column: "BiomedId");

            migrationBuilder.AddForeignKey(
                name: "FK_JobPart_User_BiomedId",
                table: "JobPart",
                column: "BiomedId",
                principalTable: "User",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkDone_User_BiomedId",
                table: "WorkDone",
                column: "BiomedId",
                principalTable: "User",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_JobPart_User_BiomedId",
                table: "JobPart");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkDone_User_BiomedId",
                table: "WorkDone");

            migrationBuilder.DropIndex(
                name: "IX_WorkDone_BiomedId",
                table: "WorkDone");

            migrationBuilder.DropIndex(
                name: "IX_JobPart_BiomedId",
                table: "JobPart");

            migrationBuilder.DropColumn(
                name: "BiomedId",
                table: "WorkDone");

            migrationBuilder.DropColumn(
                name: "BiomedId",
                table: "JobPart");
        }
    }
}
