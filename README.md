# TLScope

**Real-time P2P Network Security Visualization Tool**

TLScope is a terminal-based network monitoring and visualization tool that discovers devices, tracks connections, and maps network topology in real-time. Built with Spectre.Console, it provides an adaptive ASCII art interface with force-directed graph layouts, connection strength indicators, and comprehensive network analysis.

Devices are represented as vertices and connections as edges following graph theory principles: **G = { V1, V2, ..., Vi }**

---

## Features

### Real-time Network Visualization
- **Adaptive Dashboard**: Automatic single/two-column layout based on terminal width
- **Force-Directed Graph**: Physics-based network topology with angle-aware connections
- **Device Classification**: Gateways, TLS peers, local/remote devices with visual indicators
- **Connection Strength**: Visual weight indicators (→ weak, ⇒ medium, ⇛ strong)
- **Unicode/ASCII Modes**: Beautiful ⋯⋮⋱⋰ math dots or compatible fallback

### Network Analysis
- **Topology Metrics**: Vertex cut, graph connectivity, network resilience
- **Connection Matrix**: Intensity heat map with visual density indicators
- **Protocol Analysis**: HTTP, HTTPS, SSH, DNS, custom protocol tracking
- **Traffic Statistics**: Real-time packet counts, bandwidth, connection rates
- **Gateway Detection**: Automatic identification of network gateways and routes

### Device Management
- **Interactive Device List**: Browse and inspect all discovered devices
- **Detailed Views**: Comprehensive device information with connections, timeline, traffic stats
- **Device Actions**: Filter network view, highlight connections, quick navigation
- **TLS Peer Status**: Real-time tracking of connected TLScope instances

### Filtering & Configuration
- **IP Filtering**: Exclude loopback, broadcast, multicast, link-local, reserved addresses
- **Device Exclusions**: Filter specific IPs, hostnames, or MAC addresses
- **Display Settings**: Customize connection characters and visual preferences
- **Persistent Config**: JSON-based configuration with hot-reload support

### Export Capabilities
- **DOT Graph**: Graphviz-compatible network diagrams
- **LaTeX/PDF Reports**: Comprehensive analysis with auto-compilation
  - Executive summary with key metrics
  - Complete device inventory
  - Connection analysis and protocol distribution
  - Network topology visualization
  - Graph metrics and security configuration

### P2P Collaboration
- **TLS Peer Discovery**: UDP broadcast on port 8442
- **Mutual Authentication**: TLS connections using SSH keys on port 8443
- **Graph Synchronization**: Real-time topology data exchange
- **SSH Randomart Avatars**: Unique visual identifiers from key hashes

---

## Quick Start

### Requirements
- **.NET 9.0 SDK** (or .NET 8.0+)
- **libpcap** (Linux/macOS) or **Npcap** (Windows)
- **sudo/admin privileges** for packet capture
- **Optional**: `pdflatex` for PDF report generation

### Installation

```bash
git clone https://github.com/yourusername/tlscope.git
cd tlscope
dotnet restore
dotnet build
sudo dotnet run
```

### First Run
1. Create user account (username + password)
2. Select network interface to monitor
3. Start capture - discovery begins automatically
4. Navigate with keyboard shortcuts

### UI Testing (No Root Required)
```bash
dotnet run -- uitest
```
Uses mock services to explore the interface without packet capture.

---

## Commands

| Key | Command     | Description                          |
|-----|-------------|--------------------------------------|
| `d` | devices     | View all discovered devices          |
| `c` | connections | Show active network connections      |
| `p` | peers       | List TLScope peers on network        |
| `s` | statistics  | Network statistics and topology      |
| `g` | graph       | Fullscreen network visualization     |
| `e` | export      | Export DOT graph or LaTeX/PDF report |
| `f` | filters     | Configure IP filtering rules         |
| `x` | exclusions  | Manage device exclusions             |
| `a` | avatar      | Customize SSH randomart avatar       |
| `i` | interface   | Switch network interface             |
| `l` | logs        | View activity logs                   |
| `h` | help        | Show command reference               |
| `q` | quit        | Exit TLScope                         |

---

## Architecture

### Core Components

**Network Layer**
- `PacketCaptureService`: Real-time passive traffic capture (SharpPcap)
- `NetworkScanService`: Active network scanning and discovery
- `GatewayDetectionService`: Gateway identification and routing analysis

**Graph Layer**
- `GraphService`: Network topology management (QuikGraph)
- `ClusteringUtility`: Device grouping and layout algorithms
- `NetworkGraphUtility`: ASCII visualization rendering

**P2P Layer**
- `TlsPeerService`: Peer discovery, TLS authentication, data sync
- `CryptoUtility`: Certificate generation from SSH keys

**User Interface**
- `MainApplication`: Interactive Spectre.Console UI with adaptive layout
- Command pattern for all user actions

**Data Layer**
- SQLite + EF Core: Users, devices, connections, peers
- JSON configuration: Filters, display settings, exclusions

### Network Communication Model

**Passive Monitoring (All Devices)**
- Captures all network traffic without active scanning
- Builds connection graph from observed flows
- Monitors protocols, bandwidth, connection patterns

**TLS P2P (TLScope Instances)**
- UDP broadcast discovery (port 8442)
- Mutual TLS authentication (port 8443) with SSH keys
- Real-time topology synchronization

---

## Project Structure

```
TLScope/
├── src/
│   ├── Commands/              # CLI command implementations
│   ├── Data/                  # EF Core context and migrations
│   ├── Models/                # Domain models
│   │   ├── Device.cs
│   │   ├── Connection.cs
│   │   ├── TLSPeer.cs
│   │   ├── User.cs
│   │   └── ...
│   ├── Services/              # Business logic
│   │   ├── PacketCaptureService.cs
│   │   ├── GraphService.cs
│   │   ├── TlsPeerService.cs
│   │   ├── GatewayDetectionService.cs
│   │   └── Mock/              # Testing mocks
│   ├── Utilities/             # Helper functions
│   │   ├── NetworkGraphUtility.cs
│   │   ├── CryptoUtility.cs
│   │   ├── AvatarUtility.cs
│   │   └── ...
│   ├── Views/                 # UI layer
│   │   └── MainApplication.cs
│   ├── Testing/               # Test harness
│   └── Program.cs
├── appearances.json           # Avatar definitions
├── tlscope.db                 # SQLite database (generated)
├── tlscope_*.json             # Configuration files (generated)
└── README.md
```

---

## Configuration Files

All configuration files are created automatically and stored in `~/.config/tlscope/` (or equivalent).

### `tlscope_filters.json`
IP filtering rules:
- Loopback, broadcast, multicast, link-local, reserved addresses
- Duplicate IP detection

### `tlscope_display.json`
Visualization settings:
- `UseAsciiConnections`: Toggle Unicode/ASCII connection characters

### `tlscope_exclusions.json`
Device exclusions:
- Excluded IPs, hostnames, MAC addresses

---

## Technology Stack

| Component        | Technology       | Version                         |
|------------------|------------------|---------------------------------|
| Runtime          | .NET             | 9.0                             |
| UI               | Spectre.Console  | 0.49.1                          |
| Packet Capture   | SharpPcap        | 6.3.0                           |
| Packet Parsing   | PacketDotNet     | 1.4.7                           |
| Graph Algorithms | QuikGraph        | 2.5.0                           |
| Database         | EF Core + SQLite | 8.0                             |
| Logging          | Serilog          | Latest                          |
| Crypto           | Argon2           | Konscious.Security.Cryptography |

---

## Visualization

### Device Symbols
- `◆` Gateway (default gateway)
- `◉` TLS Peer (green)
- `●` Active device (cyan)
- `○` Inactive device (grey)
- `.` Connection edge (based on signal strength)

---

## Development

### Building
```bash
dotnet build
```

### Running Tests
```bash
# UI test mode (mock services, no root)
dotnet run -- uitest

# Scan command test
dotnet run -- scan --interface eth0
```

### Database Migrations
```bash
# Create migration
dotnet ef migrations add MigrationName

# Apply migrations
dotnet ef database update
```

---

## Roadmap Goals

- [-] Force-directed graph layout
- [-] Enhanced filtering (regex, protocol-specific)
- [-] TLS peer discovery
- [-] LaTeX/PDF reports
- [X] Adaptive UI (single/two-column)
- [X] Connection strength visualization
- [X] Vertex cut analysis
- [X] Gateway detection
- [X] Device action menus
- [X] SSH randomart avatars
- [-] Multi-interface monitoring
- [ ] Network anomaly detection
- [ ] Historical data analysis
- [ ] Plugin system for custom protocols
- [ ] GraphML/JSON export formats
- [ ] Network diff tool (snapshot comparison)
- [ ] Geolocation for public IPs
- [ ] SSH tunnel support

---

## Contributing

Contributions welcome! Areas of interest:
- Protocol analyzers
- Graph algorithms
- Performance optimizations
- Documentation
- Bug reports

---

## License

Copyright © 2025 Ethan Khai Dang

---

## Acknowledgments

Built with:
- [Spectre.Console](https://spectreconsole.net/) - Terminal UI framework
- [SharpPcap](https://github.com/dotpcap/sharppcap) - Packet capture
- [QuikGraph](https://github.com/KeRNeLith/QuikGraph) - Graph algorithms
- SSH Randomart inspired by OpenSSH
