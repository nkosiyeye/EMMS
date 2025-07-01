using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EMMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class all : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AssetMovement_LookupItems_ConditionId",
                table: "AssetMovement");

            migrationBuilder.AddColumn<DateTime>(
                name: "CloseDate",
                table: "WorkRequest",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ConditionId",
                table: "AssetMovement",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddForeignKey(
                name: "FK_AssetMovement_LookupItems_ConditionId",
                table: "AssetMovement",
                column: "ConditionId",
                principalTable: "LookupItems",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AssetMovement_LookupItems_ConditionId",
                table: "AssetMovement");

            migrationBuilder.DropColumn(
                name: "CloseDate",
                table: "WorkRequest");

            migrationBuilder.AlterColumn<int>(
                name: "ConditionId",
                table: "AssetMovement",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AssetMovement_LookupItems_ConditionId",
                table: "AssetMovement",
                column: "ConditionId",
                principalTable: "LookupItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
