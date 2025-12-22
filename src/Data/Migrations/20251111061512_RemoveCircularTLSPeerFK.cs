using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TLScope.src.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveCircularTLSPeerFK : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Devices_TLSPeerId",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "DeviceId",
                table: "TLSPeers");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_TLSPeerId",
                table: "Devices",
                column: "TLSPeerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Devices_TLSPeerId",
                table: "Devices");

            migrationBuilder.AddColumn<int>(
                name: "DeviceId",
                table: "TLSPeers",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Devices_TLSPeerId",
                table: "Devices",
                column: "TLSPeerId",
                unique: true);
        }
    }
}
