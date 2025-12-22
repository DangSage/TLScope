using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TLScope.src.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Devices_IsGateway",
                table: "Devices",
                column: "IsGateway");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_IsTLScopePeer",
                table: "Devices",
                column: "IsTLScopePeer");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_LastSeen",
                table: "Devices",
                column: "LastSeen");

            migrationBuilder.CreateIndex(
                name: "IX_Connections_LastSeen",
                table: "Connections",
                column: "LastSeen");

            migrationBuilder.CreateIndex(
                name: "IX_Connections_Type",
                table: "Connections",
                column: "Type");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Devices_IsGateway",
                table: "Devices");

            migrationBuilder.DropIndex(
                name: "IX_Devices_IsTLScopePeer",
                table: "Devices");

            migrationBuilder.DropIndex(
                name: "IX_Devices_LastSeen",
                table: "Devices");

            migrationBuilder.DropIndex(
                name: "IX_Connections_LastSeen",
                table: "Connections");

            migrationBuilder.DropIndex(
                name: "IX_Connections_Type",
                table: "Connections");
        }
    }
}
