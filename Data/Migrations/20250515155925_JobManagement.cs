using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EMMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class JobManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkRequest",
                columns: table => new
                {
                    WorkRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FaultReportId = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WorkStatusId = table.Column<int>(type: "int", nullable: false),
                    OutcomeId = table.Column<int>(type: "int", nullable: false),
                    RequestedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DateCreated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DateModified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowState = table.Column<byte>(type: "tinyint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkRequest", x => x.WorkRequestId);
                    table.ForeignKey(
                        name: "FK_WorkRequest_Assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Assets",
                        principalColumn: "AssetId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WorkRequest_LookupItems_FaultReportId",
                        column: x => x.FaultReportId,
                        principalTable: "LookupItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                    table.ForeignKey(
                        name: "FK_WorkRequest_LookupItems_OutcomeId",
                        column: x => x.OutcomeId,
                        principalTable: "LookupItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                    table.ForeignKey(
                        name: "FK_WorkRequest_LookupItems_WorkStatusId",
                        column: x => x.WorkStatusId,
                        principalTable: "LookupItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateTable(
                name: "Job",
                columns: table => new
                {
                    JobId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignedTo = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FaultReportId = table.Column<int>(type: "int", nullable: false),
                    IsExternalProvider = table.Column<bool>(type: "bit", nullable: false),
                    ExternalProviderId = table.Column<int>(type: "int", nullable: false),
                    StatusId = table.Column<int>(type: "int", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DateCreated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DateModified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowState = table.Column<byte>(type: "tinyint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Job", x => x.JobId);
                    table.ForeignKey(
                        name: "FK_Job_Assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Assets",
                        principalColumn: "AssetId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Job_LookupItems_ExternalProviderId",
                        column: x => x.ExternalProviderId,
                        principalTable: "LookupItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                    table.ForeignKey(
                        name: "FK_Job_LookupItems_FaultReportId",
                        column: x => x.FaultReportId,
                        principalTable: "LookupItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                    table.ForeignKey(
                        name: "FK_Job_LookupItems_StatusId",
                        column: x => x.StatusId,
                        principalTable: "LookupItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                    table.ForeignKey(
                        name: "FK_Job_WorkRequest_WorkRequestId",
                        column: x => x.WorkRequestId,
                        principalTable: "WorkRequest",
                        principalColumn: "WorkRequestId",   
                        onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Job_AssetId",
                table: "Job",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_Job_ExternalProviderId",
                table: "Job",
                column: "ExternalProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_Job_FaultReportId",
                table: "Job",
                column: "FaultReportId");

            migrationBuilder.CreateIndex(
                name: "IX_Job_StatusId",
                table: "Job",
                column: "StatusId");

            migrationBuilder.CreateIndex(
                name: "IX_Job_WorkRequestId",
                table: "Job",
                column: "WorkRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkRequest_AssetId",
                table: "WorkRequest",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkRequest_FaultReportId",
                table: "WorkRequest",
                column: "FaultReportId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkRequest_OutcomeId",
                table: "WorkRequest",
                column: "OutcomeId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkRequest_WorkStatusId",
                table: "WorkRequest",
                column: "WorkStatusId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Job");

            migrationBuilder.DropTable(
                name: "WorkRequest");
        }
    }
}
