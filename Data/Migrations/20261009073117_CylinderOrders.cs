using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EMMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class CylinderOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "CylinderOrderSeq");

            migrationBuilder.CreateTable(
                name: "CylinderRequests",
                columns: table => new
                {
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FacilityId = table.Column<int>(type: "int", nullable: false),
                    DescriptionId = table.Column<int>(type: "int", nullable: false),
                    SizeId = table.Column<int>(type: "int", nullable: false),
                    ContentId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    RequisitionName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    RequisitionNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    ApprovedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DateApproved = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecisionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DateCreated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DateModified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowState = table.Column<byte>(type: "tinyint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CylinderRequests", x => x.RequestId);
                    table.ForeignKey(
                        name: "FK_CylinderRequests_Facilities_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "Facilities",
                        principalColumn: "FacilityId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CylinderRequests_LookupItems_ContentId",
                        column: x => x.ContentId,
                        principalTable: "LookupItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CylinderRequests_LookupItems_DescriptionId",
                        column: x => x.DescriptionId,
                        principalTable: "LookupItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CylinderRequests_LookupItems_SizeId",
                        column: x => x.SizeId,
                        principalTable: "LookupItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CylinderRequests_User_ApprovedBy",
                        column: x => x.ApprovedBy,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CylinderRequests_User_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CylinderOrders",
                columns: table => new
                {
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DateCreated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DateModified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowState = table.Column<byte>(type: "tinyint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CylinderOrders", x => x.OrderId);
                    table.ForeignKey(
                        name: "FK_CylinderOrders_CylinderRequests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "CylinderRequests",
                        principalColumn: "RequestId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CylinderOrders_User_OrderedBy",
                        column: x => x.OrderedBy,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CylinderOrderItems",
                columns: table => new
                {
                    OrderItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MovementId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CylinderOrderItems", x => x.OrderItemId);
                    table.ForeignKey(
                        name: "FK_CylinderOrderItems_AssetMovement_MovementId",
                        column: x => x.MovementId,
                        principalTable: "AssetMovement",
                        principalColumn: "MovementId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CylinderOrderItems_Assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Assets",
                        principalColumn: "AssetId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CylinderOrderItems_CylinderOrders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "CylinderOrders",
                        principalColumn: "OrderId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CylinderOrderItems_AssetId",
                table: "CylinderOrderItems",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_CylinderOrderItems_MovementId",
                table: "CylinderOrderItems",
                column: "MovementId");

            migrationBuilder.CreateIndex(
                name: "IX_CylinderOrderItems_OrderId",
                table: "CylinderOrderItems",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_CylinderOrders_OrderedBy",
                table: "CylinderOrders",
                column: "OrderedBy");

            migrationBuilder.CreateIndex(
                name: "IX_CylinderOrders_OrderNumber",
                table: "CylinderOrders",
                column: "OrderNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CylinderOrders_RequestId",
                table: "CylinderOrders",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_CylinderRequests_ApprovedBy",
                table: "CylinderRequests",
                column: "ApprovedBy");

            migrationBuilder.CreateIndex(
                name: "IX_CylinderRequests_ContentId",
                table: "CylinderRequests",
                column: "ContentId");

            migrationBuilder.CreateIndex(
                name: "IX_CylinderRequests_CreatedBy",
                table: "CylinderRequests",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_CylinderRequests_DescriptionId",
                table: "CylinderRequests",
                column: "DescriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_CylinderRequests_FacilityId_Status",
                table: "CylinderRequests",
                columns: new[] { "FacilityId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CylinderRequests_SizeId",
                table: "CylinderRequests",
                column: "SizeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CylinderOrderItems");

            migrationBuilder.DropTable(
                name: "CylinderOrders");

            migrationBuilder.DropTable(
                name: "CylinderRequests");

            migrationBuilder.DropSequence(
                name: "CylinderOrderSeq");
        }
    }
}
