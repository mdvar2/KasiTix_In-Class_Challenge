using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasiTix.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DeferTier2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_EventId_CreatedAt_Id",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "SalesRevision",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "Events");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "SalesRevision",
                table: "Events",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "Events",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_EventId_CreatedAt_Id",
                table: "Orders",
                columns: new[] { "EventId", "CreatedAt", "Id" });
        }
    }
}
