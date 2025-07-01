using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EMMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class jobManagement141 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                table: "WorkDone",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateCreated",
                table: "WorkDone",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateModified",
                table: "WorkDone",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedBy",
                table: "WorkDone",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "RowState",
                table: "WorkDone",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                table: "ExternalWorkDone",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateCreated",
                table: "ExternalWorkDone",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateModified",
                table: "ExternalWorkDone",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedBy",
                table: "ExternalWorkDone",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "RowState",
                table: "ExternalWorkDone",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "WorkDone");

            migrationBuilder.DropColumn(
                name: "DateCreated",
                table: "WorkDone");

            migrationBuilder.DropColumn(
                name: "DateModified",
                table: "WorkDone");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "WorkDone");

            migrationBuilder.DropColumn(
                name: "RowState",
                table: "WorkDone");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "ExternalWorkDone");

            migrationBuilder.DropColumn(
                name: "DateCreated",
                table: "ExternalWorkDone");

            migrationBuilder.DropColumn(
                name: "DateModified",
                table: "ExternalWorkDone");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "ExternalWorkDone");

            migrationBuilder.DropColumn(
                name: "RowState",
                table: "ExternalWorkDone");
        }
    }
}
