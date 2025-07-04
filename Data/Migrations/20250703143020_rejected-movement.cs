using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EMMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class rejectedmovement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DateRejected",
                table: "AssetMovement",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RejectedBy",
                table: "AssetMovement",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RejectedReasonId",
                table: "AssetMovement",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssetMovement_ApprovedBy",
                table: "AssetMovement",
                column: "ApprovedBy");

            migrationBuilder.CreateIndex(
                name: "IX_AssetMovement_ReceivedBy",
                table: "AssetMovement",
                column: "ReceivedBy");

            migrationBuilder.CreateIndex(
                name: "IX_AssetMovement_RejectedBy",
                table: "AssetMovement",
                column: "RejectedBy");

            migrationBuilder.CreateIndex(
                name: "IX_AssetMovement_RejectedReasonId",
                table: "AssetMovement",
                column: "RejectedReasonId");

            migrationBuilder.AddForeignKey(
                name: "FK_AssetMovement_LookupItems_RejectedReasonId",
                table: "AssetMovement",
                column: "RejectedReasonId",
                principalTable: "LookupItems",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AssetMovement_User_ApprovedBy",
                table: "AssetMovement",
                column: "ApprovedBy",
                principalTable: "User",
                principalColumn: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_AssetMovement_User_ReceivedBy",
                table: "AssetMovement",
                column: "ReceivedBy",
                principalTable: "User",
                principalColumn: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_AssetMovement_User_RejectedBy",
                table: "AssetMovement",
                column: "RejectedBy",
                principalTable: "User",
                principalColumn: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AssetMovement_LookupItems_RejectedReasonId",
                table: "AssetMovement");

            migrationBuilder.DropForeignKey(
                name: "FK_AssetMovement_User_ApprovedBy",
                table: "AssetMovement");

            migrationBuilder.DropForeignKey(
                name: "FK_AssetMovement_User_ReceivedBy",
                table: "AssetMovement");

            migrationBuilder.DropForeignKey(
                name: "FK_AssetMovement_User_RejectedBy",
                table: "AssetMovement");

            migrationBuilder.DropIndex(
                name: "IX_AssetMovement_ApprovedBy",
                table: "AssetMovement");

            migrationBuilder.DropIndex(
                name: "IX_AssetMovement_ReceivedBy",
                table: "AssetMovement");

            migrationBuilder.DropIndex(
                name: "IX_AssetMovement_RejectedBy",
                table: "AssetMovement");

            migrationBuilder.DropIndex(
                name: "IX_AssetMovement_RejectedReasonId",
                table: "AssetMovement");

            migrationBuilder.DropColumn(
                name: "DateRejected",
                table: "AssetMovement");

            migrationBuilder.DropColumn(
                name: "RejectedBy",
                table: "AssetMovement");

            migrationBuilder.DropColumn(
                name: "RejectedReasonId",
                table: "AssetMovement");
        }
    }
}
