using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EMMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class WorkRequestWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Guards: the unique indexes below cannot be created over existing duplicates. Stop with a clear message
            // instead of renumbering job cards that may already be printed or merging jobs.
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM Job WHERE JobNumber IS NOT NULL GROUP BY JobNumber HAVING COUNT(*) > 1)
    THROW 50011, 'Duplicate Job.JobNumber values exist; renumber them before re-running the migration.', 1;
IF EXISTS (SELECT 1 FROM Job WHERE LEN(JobNumber) > 50)
    THROW 50012, 'Job.JobNumber values longer than 50 characters exist; shorten them before re-running the migration.', 1;
IF EXISTS (SELECT 1 FROM Job WHERE WorkRequestId IS NOT NULL GROUP BY WorkRequestId HAVING COUNT(*) > 1)
    THROW 50013, 'A work request has more than one job; close or re-link the extra jobs before re-running the migration.', 1;");

            migrationBuilder.DropIndex(
                name: "IX_Job_WorkRequestId",
                table: "Job");

            // Start above the highest numeric suffix in use ({code}Jb-{n}) so new job numbers never collide with existing ones.
            migrationBuilder.Sql(@"
DECLARE @start bigint = 1 + ISNULL((
    SELECT MAX(TRY_CAST(CASE WHEN CHARINDEX('-', JobNumber) > 0
                             THEN RIGHT(JobNumber, CHARINDEX('-', REVERSE(JobNumber)) - 1) END AS bigint))
    FROM Job), 0);
DECLARE @sql nvarchar(200) = N'CREATE SEQUENCE dbo.JobNumberSeq AS bigint START WITH ' + CAST(@start AS nvarchar(20)) + N' INCREMENT BY 1 NO CACHE;';
EXEC sys.sp_executesql @sql;");

            migrationBuilder.AddColumn<bool>(
                name: "IsServiceRequest",
                table: "WorkRequest",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "JobNumber",
                table: "Job",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Job_JobNumber",
                table: "Job",
                column: "JobNumber",
                unique: true,
                filter: "[JobNumber] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Job_WorkRequestId",
                table: "Job",
                column: "WorkRequestId",
                unique: true,
                filter: "[WorkRequestId] IS NOT NULL");

            // Service requests used to be recognised by their description text.
            migrationBuilder.Sql("UPDATE WorkRequest SET IsServiceRequest = 1 WHERE Description = 'Service request for asset';");

            // Completed jobs now carry an end date (the dashboard counts completed jobs by it).
            migrationBuilder.Sql(@"
UPDATE j SET EndDate = ISNULL(j.DateModified, j.StartDate)
FROM Job j
JOIN LookupItems s ON s.Id = j.StatusId
JOIN LookupLists l ON l.LookupListId = s.LookupListId
WHERE j.EndDate IS NULL AND s.Name = 'Completed' AND l.Name = 'WorkRequestStatus';");

            // Cancelling needs a reason; add the list where it was never seeded.
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM LookupLists WHERE Name = 'CancelReason')
BEGIN
    INSERT INTO LookupLists (Name, DateCreated, RowState) VALUES ('CancelReason', GETDATE(), 1);
    DECLARE @list int = SCOPE_IDENTITY();
    INSERT INTO LookupItems (LookupListId, Name, SortIndex, DateCreated, RowState) VALUES
        (@list, 'Duplicate request', 1, GETDATE(), 1),
        (@list, 'Raised in error', 2, GETDATE(), 1),
        (@list, 'Resolved without a job', 3, GETDATE(), 1),
        (@list, 'Asset decommissioned or replaced', 4, GETDATE(), 1),
        (@list, 'Other', 5, GETDATE(), 1);
END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Job_JobNumber",
                table: "Job");

            migrationBuilder.DropIndex(
                name: "IX_Job_WorkRequestId",
                table: "Job");

            migrationBuilder.DropColumn(
                name: "IsServiceRequest",
                table: "WorkRequest");

            migrationBuilder.DropSequence(
                name: "JobNumberSeq");

            migrationBuilder.AlterColumn<string>(
                name: "JobNumber",
                table: "Job",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Job_WorkRequestId",
                table: "Job",
                column: "WorkRequestId");
        }
    }
}
