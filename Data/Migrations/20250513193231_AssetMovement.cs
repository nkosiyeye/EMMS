using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EMMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AssetMovement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Facilities",
                columns: table => new
                {
                    FacilityId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FacilityName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FacilityCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DateCreated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DateModified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowState = table.Column<byte>(type: "tinyint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Facilities", x => x.FacilityId);
                });

            migrationBuilder.CreateTable(
                name: "AssetMovement",
                columns: table => new
                {
                    MovementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MovementDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MovementTypeId = table.Column<int>(type: "int", nullable: false),
                    FromId = table.Column<int>(type: "int", nullable: false),
                    FacilityId = table.Column<int>(type: "int", nullable: true),
                    ServicePointId = table.Column<int>(type: "int", nullable: true),
                    ReasonId = table.Column<int>(type: "int", nullable: false),
                    FunctionalStatusId = table.Column<int>(type: "int", nullable: false),
                    IsApproved = table.Column<bool>(type: "bit", nullable: false),
                    DateReceived = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConditionId = table.Column<int>(type: "int", nullable: false),
                    ReceivedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetMovement", x => x.MovementId);
                    table.ForeignKey(
                        name: "FK_AssetMovement_Assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Assets",
                        principalColumn: "AssetId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AssetMovement_Facilities_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "Facilities",
                        principalColumn: "FacilityId");
                    table.ForeignKey(
                        name: "FK_AssetMovement_Facilities_FromId",
                        column: x => x.FromId,
                        principalTable: "Facilities",
                        principalColumn: "FacilityId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AssetMovement_LookupItems_ConditionId",
                        column: x => x.ConditionId,
                        principalTable: "LookupItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                    table.ForeignKey(
                        name: "FK_AssetMovement_LookupItems_FunctionalStatusId",
                        column: x => x.FunctionalStatusId,
                        principalTable: "LookupItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                    table.ForeignKey(
                        name: "FK_AssetMovement_LookupItems_MovementTypeId",
                        column: x => x.MovementTypeId,
                        principalTable: "LookupItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                    table.ForeignKey(
                        name: "FK_AssetMovement_LookupItems_ReasonId",
                        column: x => x.ReasonId,
                        principalTable: "LookupItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                    table.ForeignKey(
                        name: "FK_AssetMovement_LookupItems_ServicePointId",
                        column: x => x.ServicePointId,
                        principalTable: "LookupItems",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssetMovement_AssetId",
                table: "AssetMovement",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetMovement_ConditionId",
                table: "AssetMovement",
                column: "ConditionId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetMovement_FacilityId",
                table: "AssetMovement",
                column: "FacilityId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetMovement_FromId",
                table: "AssetMovement",
                column: "FromId");

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

            migrationBuilder.CreateIndex(
                name: "IX_AssetMovement_ServicePointId",
                table: "AssetMovement",
                column: "ServicePointId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssetMovement");

            migrationBuilder.DropTable(
                name: "Facilities");
        }
    }
}
