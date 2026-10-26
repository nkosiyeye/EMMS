using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EMMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class JobParts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "JobPart",
                columns: table => new
                {
                    JobPartId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JobId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartId = table.Column<int>(type: "int", nullable: true),
                    PartName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DateCreated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DateModified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowState = table.Column<byte>(type: "tinyint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobPart", x => x.JobPartId);
                    table.ForeignKey(
                        name: "FK_JobPart_Job_JobId",
                        column: x => x.JobId,
                        principalTable: "Job",
                        principalColumn: "JobId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JobPart_LookupItems_PartId",
                        column: x => x.PartId,
                        principalTable: "LookupItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JobPart_JobId",
                table: "JobPart",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_JobPart_PartId",
                table: "JobPart",
                column: "PartId");

            // Admins fill the spare parts catalogue from the Lookups page.
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM LookupLists WHERE Name = 'SparePart')
    INSERT INTO LookupLists (Name, DateCreated, RowState) VALUES ('SparePart', GETDATE(), 1);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JobPart");
        }
    }
}
