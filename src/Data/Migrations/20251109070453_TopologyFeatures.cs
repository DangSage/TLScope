using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TLScope.src.Data.Migrations
{
    /// <inheritdoc />
    public partial class TopologyFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GatewayRole",
                table: "Devices",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HopCount",
                table: "Devices",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDefaultGateway",
                table: "Devices",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsGateway",
                table: "Devices",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "AverageTTL",
                table: "Connections",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxTTL",
                table: "Connections",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinTTL",
                table: "Connections",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PacketCountForTTL",
                table: "Connections",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "Connections",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GatewayRole",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "HopCount",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "IsDefaultGateway",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "IsGateway",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "AverageTTL",
                table: "Connections");

            migrationBuilder.DropColumn(
                name: "MaxTTL",
                table: "Connections");

            migrationBuilder.DropColumn(
                name: "MinTTL",
                table: "Connections");

            migrationBuilder.DropColumn(
                name: "PacketCountForTTL",
                table: "Connections");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Connections");
        }
    }
}
