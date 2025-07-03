using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EMMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class jobManagement142 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop the old column
            migrationBuilder.DropColumn(
                name: "JobId",
                table: "WorkDone");

            // Add the new column
            migrationBuilder.AddColumn<int>(
                name: "JobId",
                table: "WorkDone",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.DropColumn(
                name: "JobId",
                table: "ExternalWorkDone");

            migrationBuilder.AddColumn<int>(
                name: "JobId",
                table: "ExternalWorkDone",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

    }


}
