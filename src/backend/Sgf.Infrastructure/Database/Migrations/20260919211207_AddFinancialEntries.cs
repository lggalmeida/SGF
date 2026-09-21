using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgf.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddFinancialEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FinancialEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PaidAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinancialEntries", x => x.Id);
                    table.CheckConstraint("CK_FinancialEntries_Amount", "\"Amount\" > 0");
                    table.CheckConstraint("CK_FinancialEntries_Description", "length(btrim(\"Description\")) > 0");
                    table.CheckConstraint("CK_FinancialEntries_Payment", "(\"Status\" = 'Pending' AND \"PaidAt\" IS NULL) OR (\"Status\" = 'Paid' AND \"PaidAt\" IS NOT NULL)");
                    table.CheckConstraint("CK_FinancialEntries_Type", "\"Type\" IN ('Income', 'Expense')");
                    table.ForeignKey(
                        name: "FK_FinancialEntries_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FinancialEntries_CompanyId_DueDate",
                table: "FinancialEntries",
                columns: new[] { "CompanyId", "DueDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FinancialEntries");
        }
    }
}
