using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TLScope.src.Data.Migrations
{
    /// <inheritdoc />
    public partial class TlsProtocolRefactoring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TLSPeers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Username = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    IPAddress = table.Column<string>(type: "TEXT", maxLength: 45, nullable: false),
                    Port = table.Column<int>(type: "INTEGER", nullable: false),
                    SSHPublicKey = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    AvatarType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    AvatarColor = table.Column<string>(type: "TEXT", maxLength: 7, nullable: false),
                    FirstSeen = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastConnected = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsVerified = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastVerified = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CombinedRandomartAvatar = table.Column<string>(type: "TEXT", maxLength: 600, nullable: true),
                    Version = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    DeviceCount = table.Column<int>(type: "INTEGER", nullable: false),
                    DeviceId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TLSPeers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Username = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", nullable: false),
                    SSHPrivateKeyPath = table.Column<string>(type: "TEXT", nullable: true),
                    SSHPublicKey = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    AvatarType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    AvatarColor = table.Column<string>(type: "TEXT", maxLength: 7, nullable: false),
                    CustomAvatarLinesStorage = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastLogin = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Devices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MACAddress = table.Column<string>(type: "TEXT", maxLength: 17, nullable: false),
                    IPAddress = table.Column<string>(type: "TEXT", maxLength: 45, nullable: false),
                    Hostname = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    DeviceName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    OperatingSystem = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Vendor = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    FirstSeen = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastSeen = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PacketCount = table.Column<long>(type: "INTEGER", nullable: false),
                    BytesTransferred = table.Column<long>(type: "INTEGER", nullable: false),
                    OpenPorts = table.Column<string>(type: "TEXT", nullable: false),
                    IsTLScopePeer = table.Column<bool>(type: "INTEGER", nullable: false),
                    TLSPeerId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Devices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Devices_TLSPeers_TLSPeerId",
                        column: x => x.TLSPeerId,
                        principalTable: "TLSPeers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Connections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SourceDeviceId = table.Column<int>(type: "INTEGER", nullable: false),
                    DestinationDeviceId = table.Column<int>(type: "INTEGER", nullable: false),
                    Protocol = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    SourcePort = table.Column<int>(type: "INTEGER", nullable: true),
                    DestinationPort = table.Column<int>(type: "INTEGER", nullable: true),
                    FirstSeen = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastSeen = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PacketCount = table.Column<long>(type: "INTEGER", nullable: false),
                    BytesTransferred = table.Column<long>(type: "INTEGER", nullable: false),
                    State = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    IsTLSPeerConnection = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Connections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Connections_Devices_DestinationDeviceId",
                        column: x => x.DestinationDeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Connections_Devices_SourceDeviceId",
                        column: x => x.SourceDeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Connections_DestinationDeviceId",
                table: "Connections",
                column: "DestinationDeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_Connections_SourceDeviceId_DestinationDeviceId_Protocol",
                table: "Connections",
                columns: new[] { "SourceDeviceId", "DestinationDeviceId", "Protocol" });

            migrationBuilder.CreateIndex(
                name: "IX_Devices_IPAddress",
                table: "Devices",
                column: "IPAddress");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_MACAddress",
                table: "Devices",
                column: "MACAddress",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Devices_TLSPeerId",
                table: "Devices",
                column: "TLSPeerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TLSPeers_IPAddress",
                table: "TLSPeers",
                column: "IPAddress");

            migrationBuilder.CreateIndex(
                name: "IX_TLSPeers_Username",
                table: "TLSPeers",
                column: "Username");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Connections");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Devices");

            migrationBuilder.DropTable(
                name: "TLSPeers");
        }
    }
}
