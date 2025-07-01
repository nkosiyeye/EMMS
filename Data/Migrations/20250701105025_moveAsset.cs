using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EMMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class moveAsset : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AssetMovement_LookupItems_FunctionalStatusId",
                table: "AssetMovement");

            migrationBuilder.DropForeignKey(
                name: "FK_AssetMovement_LookupItems_MovementTypeId",
                table: "AssetMovement");

            migrationBuilder.DropForeignKey(
                name: "FK_AssetMovement_LookupItems_ReasonId",
                table: "AssetMovement");

            migrationBuilder.DropIndex(
                name: "IX_AssetMovement_FunctionalStatusId",
                table: "AssetMovement");

            migrationBuilder.DropIndex(
                name: "IX_AssetMovement_MovementTypeId",
                table: "AssetMovement");

            migrationBuilder.DropIndex(
                name: "IX_AssetMovement_ReasonId",
                table: "AssetMovement");

            migrationBuilder.DropColumn(
                name: "FunctionalStatusId",
                table: "AssetMovement");

            migrationBuilder.DropColumn(
                name: "MovementTypeId",
                table: "AssetMovement");

            migrationBuilder.DropColumn(
                name: "ReasonId",
                table: "AssetMovement");

            migrationBuilder.AddColumn<byte>(
                name: "FunctionalStatus",
                table: "AssetMovement",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<byte>(
                name: "MovementType",
                table: "AssetMovement",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<string>(
                name: "OtherReason",
                table: "AssetMovement",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "Reason",
                table: "AssetMovement",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FunctionalStatus",
                table: "AssetMovement");

            migrationBuilder.DropColumn(
                name: "MovementType",
                table: "AssetMovement");

            migrationBuilder.DropColumn(
                name: "OtherReason",
                table: "AssetMovement");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "AssetMovement");

            migrationBuilder.AddColumn<int>(
                name: "FunctionalStatusId",
                table: "AssetMovement",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MovementTypeId",
                table: "AssetMovement",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ReasonId",
                table: "AssetMovement",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_AssetMovement_FunctionalStatusId",
                table: "AssetMovement",
                column: "FunctionalStatusId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetMovement_MovementTypeId",
                table: "AssetMovement",
                column: "MovementTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetMovement_ReasonId",
                table: "AssetMovement",
                column: "ReasonId");

            migrationBuilder.AddForeignKey(
                name: "FK_AssetMovement_LookupItems_FunctionalStatusId",
                table: "AssetMovement",
                column: "FunctionalStatusId",
                principalTable: "LookupItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AssetMovement_LookupItems_MovementTypeId",
                table: "AssetMovement",
                column: "MovementTypeId",
                principalTable: "LookupItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AssetMovement_LookupItems_ReasonId",
                table: "AssetMovement",
                column: "ReasonId",
                principalTable: "LookupItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
