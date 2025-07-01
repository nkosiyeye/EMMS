using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EMMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AssignedTo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Job_AssignedTo",
                table: "Job",
                column: "AssignedTo");

            migrationBuilder.AddForeignKey(
                name: "FK_Job_User_AssignedTo",
                table: "Job",
                column: "AssignedTo",
                principalTable: "User",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Job_User_AssignedTo",
                table: "Job");

            migrationBuilder.DropIndex(
                name: "IX_Job_AssignedTo",
                table: "Job");
        }
    }
}
