using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AWms.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class HardenInboundReceiptChain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ReceiptLines_ReceiptId_OrderLineId",
                table: "ReceiptLines",
                columns: new[] { "ReceiptId", "OrderLineId" },
                unique: true,
                filter: "\"OrderLineId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ReceiptLines_ReceiptId_OrderLineId",
                table: "ReceiptLines");
        }
    }
}
