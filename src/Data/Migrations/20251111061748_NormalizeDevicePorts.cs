using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TLScope.src.Data.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeDevicePorts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DevicePorts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DeviceId = table.Column<int>(type: "INTEGER", nullable: false),
                    Port = table.Column<int>(type: "INTEGER", nullable: false),
                    Protocol = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    FirstSeen = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastSeen = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DevicePorts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DevicePorts_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DevicePorts_DeviceId_Port_Protocol",
                table: "DevicePorts",
                columns: new[] { "DeviceId", "Port", "Protocol" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DevicePorts_Port",
                table: "DevicePorts",
                column: "Port");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DevicePorts");
        }
    }
}
