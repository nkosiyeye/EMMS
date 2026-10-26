using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EMMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AssetFunctionalStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte>(
                name: "FunctionalStatus",
                table: "Assets",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)4); // Unknown

            migrationBuilder.CreateTable(
                name: "AssetStatusChanges",
                columns: table => new
                {
                    AssetStatusChangeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromStatus = table.Column<byte>(type: "tinyint", nullable: true),
                    ToStatus = table.Column<byte>(type: "tinyint", nullable: false),
                    Source = table.Column<byte>(type: "tinyint", nullable: false),
                    JobId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MovementId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ChangedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DateChanged = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetStatusChanges", x => x.AssetStatusChangeId);
                    table.ForeignKey(
                        name: "FK_AssetStatusChanges_AssetMovement_MovementId",
                        column: x => x.MovementId,
                        principalTable: "AssetMovement",
                        principalColumn: "MovementId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssetStatusChanges_Assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Assets",
                        principalColumn: "AssetId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssetStatusChanges_Job_JobId",
                        column: x => x.JobId,
                        principalTable: "Job",
                        principalColumn: "JobId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssetStatusChanges_User_ChangedBy",
                        column: x => x.ChangedBy,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssetStatusChanges_AssetId_DateChanged",
                table: "AssetStatusChanges",
                columns: new[] { "AssetId", "DateChanged" });

            migrationBuilder.CreateIndex(
                name: "IX_AssetStatusChanges_ChangedBy",
                table: "AssetStatusChanges",
                column: "ChangedBy");

            migrationBuilder.CreateIndex(
                name: "IX_AssetStatusChanges_JobId",
                table: "AssetStatusChanges",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetStatusChanges_MovementId",
                table: "AssetStatusChanges",
                column: "MovementId");

            // Start from the status on each asset's current location (latest received movement) and log it.
            migrationBuilder.Sql(@"
UPDATE a SET FunctionalStatus = l.FunctionalStatus
FROM Assets a
JOIN dbo.vw_AssetCurrentLocation l ON l.AssetId = a.AssetId;

INSERT INTO AssetStatusChanges (AssetStatusChangeId, AssetId, FromStatus, ToStatus, Source, MovementId, ChangedBy, DateChanged)
SELECT NEWID(), l.AssetId, NULL, l.FunctionalStatus, 5, l.MovementId, NULL, GETDATE()
FROM dbo.vw_AssetCurrentLocation l
JOIN Assets a ON a.AssetId = l.AssetId;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssetStatusChanges");

            migrationBuilder.DropColumn(
                name: "FunctionalStatus",
                table: "Assets");
        }
    }
}
