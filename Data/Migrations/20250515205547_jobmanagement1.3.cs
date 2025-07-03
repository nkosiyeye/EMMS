using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EMMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class jobmanagement13 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Job_LookupItems_ExternalProviderId",
                table: "Job");

            migrationBuilder.AlterColumn<int>(
                name: "ExternalProviderId",
                table: "Job",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddForeignKey(
                name: "FK_Job_LookupItems_ExternalProviderId",
                table: "Job",
                column: "ExternalProviderId",
                principalTable: "LookupItems",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Job_LookupItems_ExternalProviderId",
                table: "Job");

            migrationBuilder.AlterColumn<int>(
                name: "ExternalProviderId",
                table: "Job",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Job_LookupItems_ExternalProviderId",
                table: "Job",
                column: "ExternalProviderId",
                principalTable: "LookupItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
