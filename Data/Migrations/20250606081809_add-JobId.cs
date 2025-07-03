using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EMMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class addJobId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "JobId",
                table: "WorkRequest",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkRequest_JobId",
                table: "WorkRequest",
                column: "JobId");

            //migrationBuilder.CreateIndex(
            //    name: "IX_Assets_CreatedBy",
            //    table: "Assets",
            //    column: "CreatedBy");

            //migrationBuilder.AddForeignKey(
            //    name: "FK_Assets_User_CreatedBy",
            //    table: "Assets",
            //    column: "CreatedBy",
            //    principalTable: "User",
            //    principalColumn: "UserId",
            //    onDelete: ReferentialAction.NoAction
            //    );

            migrationBuilder.AddForeignKey(
                name: "FK_WorkRequest_Job_JobId",
                table: "WorkRequest",
                column: "JobId",
                principalTable: "Job",
                principalColumn: "JobId",
                onDelete: ReferentialAction.NoAction);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            //migrationBuilder.DropForeignKey(
            //    name: "FK_Assets_User_CreatedBy",
            //    table: "Assets");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkRequest_Job_JobId",
                table: "WorkRequest");

            migrationBuilder.DropIndex(
                name: "IX_WorkRequest_JobId",
                table: "WorkRequest");

            //migrationBuilder.DropIndex(
            //    name: "IX_Assets_CreatedBy",
            //    table: "Assets");

            migrationBuilder.DropColumn(
                name: "JobId",
                table: "WorkRequest");
        }
    }
}
