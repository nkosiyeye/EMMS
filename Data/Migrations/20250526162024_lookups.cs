using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EMMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class lookups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ParentId",
                table: "LookupItems",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ParentId",
                table: "LookupItems");
        }
    }
}
