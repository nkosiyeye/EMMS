using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EMMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class updatedasset : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AssetMovement_Facilities_FacilityId",
                table: "AssetMovement");

            migrationBuilder.DropForeignKey(
                name: "FK_Assets_LookupItems_ServicePeriodId",
                table: "Assets");

            migrationBuilder.DropIndex(
                name: "IX_Assets_ServicePeriodId",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "ServicePeriodId",
                table: "Assets");

            migrationBuilder.AddColumn<DateTime>(
                name: "NextServiceDate",
                table: "Assets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "FacilityId",
                table: "AssetMovement",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AssetMovement_Facilities_FacilityId",
                table: "AssetMovement",
                column: "FacilityId",
                principalTable: "Facilities",
                principalColumn: "FacilityId",
                onDelete: ReferentialAction.NoAction);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AssetMovement_Facilities_FacilityId",
                table: "AssetMovement");

            migrationBuilder.DropColumn(
                name: "NextServiceDate",
                table: "Assets");

            migrationBuilder.AddColumn<int>(
                name: "ServicePeriodId",
                table: "Assets",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "FacilityId",
                table: "AssetMovement",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.CreateIndex(
                name: "IX_Assets_ServicePeriodId",
                table: "Assets",
                column: "ServicePeriodId");

            migrationBuilder.AddForeignKey(
                name: "FK_AssetMovement_Facilities_FacilityId",
                table: "AssetMovement",
                column: "FacilityId",
                principalTable: "Facilities",
                principalColumn: "FacilityId");

            migrationBuilder.AddForeignKey(
                name: "FK_Assets_LookupItems_ServicePeriodId",
                table: "Assets",
                column: "ServicePeriodId",
                principalTable: "LookupItems",
                principalColumn: "Id");
        }
    }
}
