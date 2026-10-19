using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EMMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class InfrustructureWorkRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InfrustructureWorkRequest",
                columns: table => new
                {
                    WorkRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TypeOfRequestId = table.Column<int>(type: "int", nullable: false),
                    RequestDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WorkStatusId = table.Column<int>(type: "int", nullable: false),
                    OutcomeId = table.Column<int>(type: "int", nullable: true),
                    RequestedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FacilityId = table.Column<int>(type: "int", nullable: false),
                    CancelReasonId = table.Column<int>(type: "int", nullable: true),
                    CloseDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    JobId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DateCreated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DateModified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowState = table.Column<byte>(type: "tinyint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InfrustructureWorkRequest", x => x.WorkRequestId);
                    table.ForeignKey(
                        name: "FK_InfrustructureWorkRequest_Facilities_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "Facilities",
                        principalColumn: "FacilityId",
                        onDelete: ReferentialAction.NoAction);
                    table.ForeignKey(
                        name: "FK_InfrustructureWorkRequest_Job_JobId",
                        column: x => x.JobId,
                        principalTable: "Job",
                        principalColumn: "JobId");
                    table.ForeignKey(
                        name: "FK_InfrustructureWorkRequest_LookupItems_CancelReasonId",
                        column: x => x.CancelReasonId,
                        principalTable: "LookupItems",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_InfrustructureWorkRequest_LookupItems_OutcomeId",
                        column: x => x.OutcomeId,
                        principalTable: "LookupItems",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_InfrustructureWorkRequest_LookupItems_TypeOfRequestId",
                        column: x => x.TypeOfRequestId,
                        principalTable: "LookupItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                    table.ForeignKey(
                        name: "FK_InfrustructureWorkRequest_LookupItems_WorkStatusId",
                        column: x => x.WorkStatusId,
                        principalTable: "LookupItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                    table.ForeignKey(
                        name: "FK_InfrustructureWorkRequest_User_RequestedBy",
                        column: x => x.RequestedBy,
                        principalTable: "User",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_InfrustructureWorkRequest_CancelReasonId",
                table: "InfrustructureWorkRequest",
                column: "CancelReasonId");

            migrationBuilder.CreateIndex(
                name: "IX_InfrustructureWorkRequest_FacilityId",
                table: "InfrustructureWorkRequest",
                column: "FacilityId");

            migrationBuilder.CreateIndex(
                name: "IX_InfrustructureWorkRequest_JobId",
                table: "InfrustructureWorkRequest",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_InfrustructureWorkRequest_OutcomeId",
                table: "InfrustructureWorkRequest",
                column: "OutcomeId");

            migrationBuilder.CreateIndex(
                name: "IX_InfrustructureWorkRequest_RequestedBy",
                table: "InfrustructureWorkRequest",
                column: "RequestedBy");

            migrationBuilder.CreateIndex(
                name: "IX_InfrustructureWorkRequest_TypeOfRequestId",
                table: "InfrustructureWorkRequest",
                column: "TypeOfRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_InfrustructureWorkRequest_WorkStatusId",
                table: "InfrustructureWorkRequest",
                column: "WorkStatusId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InfrustructureWorkRequest");
        }
    }
}
