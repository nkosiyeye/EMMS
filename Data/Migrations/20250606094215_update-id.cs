using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EMMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class updateid : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
    name: "IX_WorkRequest_JobId",
    table: "WorkRequest");
            // Step 1: Drop foreign key constraints
            migrationBuilder.DropForeignKey(
                name: "FK_WorkRequest_Job_JobId",
                table: "WorkRequest");

            

            // Step 2: Drop primary key on Job table
            migrationBuilder.DropPrimaryKey(
                name: "PK_Job",
                table: "Job");

            // Step 3: Drop old JobId columns
            migrationBuilder.DropColumn(
                name: "JobId",
                table: "WorkRequest");
            migrationBuilder.DropColumn(
               name: "JobId",
               table: "WorkDone");

            migrationBuilder.DropColumn(
                name: "JobId",
                table: "Job");

            // Step 4: Add new JobId as Guid
            migrationBuilder.AddColumn<Guid>(
                name: "JobId",
                table: "Job",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWID()");

            migrationBuilder.AddColumn<Guid>(
                name: "JobId",
                table: "WorkRequest",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "JobId",
                table: "WorkDone",
                type: "uniqueidentifier",
                nullable: true);

            // Step 5: Add new primary key
            migrationBuilder.AddPrimaryKey(
                name: "PK_Job",
                table: "Job",
                column: "JobId");

            // Step 6: Add new foreign key constraints
            migrationBuilder.AddForeignKey(
                name: "FK_WorkRequest_Job_JobId",
                table: "WorkRequest",
                column: "JobId",
                principalTable: "Job",
                principalColumn: "JobId",
                onDelete: ReferentialAction.NoAction);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkDone_Job_JobId",
                table: "WorkDone",
                column: "JobId",
                principalTable: "Job",
                principalColumn: "JobId",
                onDelete: ReferentialAction.NoAction);
        }



        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE WorkDone DROP COLUMN JobId");
            migrationBuilder.Sql(@"
        ALTER TABLE WorkDone
        ADD JobId INT NULL;
    ");

            migrationBuilder.Sql("ALTER TABLE WorkRequest DROP COLUMN JobId");
            migrationBuilder.Sql(@"
        ALTER TABLE WorkRequest
        ADD JobId INT NULL;
    ");

            migrationBuilder.Sql("ALTER TABLE Job DROP COLUMN JobId");
            migrationBuilder.Sql(@"
        ALTER TABLE Job
        ADD JobId INT NOT NULL IDENTITY(1,1);
    ");
        }

    }
}
