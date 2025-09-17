using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConsolidationsApi.Migrations
{
    /// <inheritdoc />
    public partial class Migration_20250915_205854 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DailyConsolidations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MerchantId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    TotalDebits = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalCredits = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    NetBalance = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TransactionCount = table.Column<int>(type: "integer", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyConsolidations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DailyConsolidations_MerchantId_Date",
                table: "DailyConsolidations",
                columns: new[] { "MerchantId", "Date" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyConsolidations");
        }
    }
}
