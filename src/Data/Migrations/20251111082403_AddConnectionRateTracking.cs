using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TLScope.src.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddConnectionRateTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastRateUpdate",
                table: "Connections",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<long>(
                name: "RecentPacketCount",
                table: "Connections",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastRateUpdate",
                table: "Connections");

            migrationBuilder.DropColumn(
                name: "RecentPacketCount",
                table: "Connections");
        }
    }
}
