using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EMMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class WorkRequestIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkRequest_FacilityId",
                table: "WorkRequest");

            migrationBuilder.DropIndex(
                name: "IX_WorkRequest_WorkRequestId",
                table: "WorkRequest");

            migrationBuilder.DropIndex(
                name: "IX_Job_JobId",
                table: "Job");

            migrationBuilder.DropIndex(
                name: "IX_InfrustructureWorkRequest_FacilityId",
                table: "InfrustructureWorkRequest");

            migrationBuilder.DropIndex(
                name: "IX_InfrustructureWorkRequest_WorkRequestId",
                table: "InfrustructureWorkRequest");

            migrationBuilder.CreateIndex(
                name: "IX_WorkRequest_FacilityId_RequestDate",
                table: "WorkRequest",
                columns: new[] { "FacilityId", "RequestDate" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkDone_JobId",
                table: "WorkDone",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_InfrustructureWorkRequest_FacilityId_RequestDate",
                table: "InfrustructureWorkRequest",
                columns: new[] { "FacilityId", "RequestDate" });

            // The 20250606 update-id migration already created this key without recording it in the model;
            // create it only where it is missing, and leave it in place on Down.
            migrationBuilder.Sql(@"
IF OBJECT_ID('FK_WorkDone_Job_JobId', 'F') IS NULL
    ALTER TABLE WorkDone ADD CONSTRAINT FK_WorkDone_Job_JobId FOREIGN KEY (JobId) REFERENCES Job (JobId) ON DELETE NO ACTION;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkRequest_FacilityId_RequestDate",
                table: "WorkRequest");

            migrationBuilder.DropIndex(
                name: "IX_WorkDone_JobId",
                table: "WorkDone");

            migrationBuilder.DropIndex(
                name: "IX_InfrustructureWorkRequest_FacilityId_RequestDate",
                table: "InfrustructureWorkRequest");

            migrationBuilder.CreateIndex(
                name: "IX_WorkRequest_FacilityId",
                table: "WorkRequest",
                column: "FacilityId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkRequest_WorkRequestId",
                table: "WorkRequest",
                column: "WorkRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Job_JobId",
                table: "Job",
                column: "JobId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InfrustructureWorkRequest_FacilityId",
                table: "InfrustructureWorkRequest",
                column: "FacilityId");

            migrationBuilder.CreateIndex(
                name: "IX_InfrustructureWorkRequest_WorkRequestId",
                table: "InfrustructureWorkRequest",
                column: "WorkRequestId",
                unique: true);
        }
    }
}
