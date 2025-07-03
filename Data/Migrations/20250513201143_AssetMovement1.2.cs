using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EMMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AssetMovement12 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                table: "AssetMovement",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateCreated",
                table: "AssetMovement",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateModified",
                table: "AssetMovement",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedBy",
                table: "AssetMovement",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "RowState",
                table: "AssetMovement",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "AssetMovement");

            migrationBuilder.DropColumn(
                name: "DateCreated",
                table: "AssetMovement");

            migrationBuilder.DropColumn(
                name: "DateModified",
                table: "AssetMovement");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "AssetMovement");

            migrationBuilder.DropColumn(
                name: "RowState",
                table: "AssetMovement");
        }
    }
}
