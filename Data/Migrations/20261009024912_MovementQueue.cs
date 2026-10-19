using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EMMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class MovementQueue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BatchId",
                table: "AssetMovement",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PatientId",
                table: "AssetMovement",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "HomeBasedCarePatients",
                columns: table => new
                {
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Surname = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DateCreated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DateModified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowState = table.Column<byte>(type: "tinyint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HomeBasedCarePatients", x => x.PatientId);
                });

            migrationBuilder.CreateTable(
                name: "MovementQueueItems",
                columns: table => new
                {
                    QueueItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceFacilityId = table.Column<int>(type: "int", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DateCreated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DateModified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowState = table.Column<byte>(type: "tinyint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovementQueueItems", x => x.QueueItemId);
                    table.ForeignKey(
                        name: "FK_MovementQueueItems_Assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Assets",
                        principalColumn: "AssetId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MovementQueueItems_Facilities_SourceFacilityId",
                        column: x => x.SourceFacilityId,
                        principalTable: "Facilities",
                        principalColumn: "FacilityId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MovementQueueItems_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssetMovement_BatchId",
                table: "AssetMovement",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetMovement_PatientId",
                table: "AssetMovement",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_HomeBasedCarePatients_Number",
                table: "HomeBasedCarePatients",
                column: "Number");

            migrationBuilder.CreateIndex(
                name: "IX_MovementQueueItems_AssetId",
                table: "MovementQueueItems",
                column: "AssetId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MovementQueueItems_SourceFacilityId",
                table: "MovementQueueItems",
                column: "SourceFacilityId");

            migrationBuilder.CreateIndex(
                name: "IX_MovementQueueItems_UserId",
                table: "MovementQueueItems",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_AssetMovement_HomeBasedCarePatients_PatientId",
                table: "AssetMovement",
                column: "PatientId",
                principalTable: "HomeBasedCarePatients",
                principalColumn: "PatientId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AssetMovement_HomeBasedCarePatients_PatientId",
                table: "AssetMovement");

            migrationBuilder.DropTable(
                name: "HomeBasedCarePatients");

            migrationBuilder.DropTable(
                name: "MovementQueueItems");

            migrationBuilder.DropIndex(
                name: "IX_AssetMovement_BatchId",
                table: "AssetMovement");

            migrationBuilder.DropIndex(
                name: "IX_AssetMovement_PatientId",
                table: "AssetMovement");

            migrationBuilder.DropColumn(
                name: "BatchId",
                table: "AssetMovement");

            migrationBuilder.DropColumn(
                name: "PatientId",
                table: "AssetMovement");
        }
    }
}
