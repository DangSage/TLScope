# TLScope TLS Peer Testing Guide

This guide explains how to test TLScope's TLS peer discovery and communication features on a single Linux machine using network namespaces.

## Overview

TLScope uses a two-layer protocol for peer-to-peer networking:

1. **UDP Discovery (Port 8442)**: Broadcast-based peer discovery with unencrypted announcements
2. **TLS Transport (Port 8443)**: Encrypted TCP/TLS connections for actual communication

To test this on a single machine, we use **Linux network namespaces** to create isolated network stacks. Each namespace can bind to the same ports (8442/8443) without conflicts.

## Prerequisites

- Linux operating system
- Root/sudo access
- .NET 9.0 SDK
- TLScope source code

## Quick Start

### 1. Build TLScope

First, build TLScope in your normal environment (network namespaces don't have internet access):

```bash
cd /home/khai/Documents/TLScope
dotnet build
```

### 2. Setup Test Environment

Run the setup script to create network namespaces and test configuration:

```bash
sudo ./test_tls_peers.sh
```

This will:
- Create 3 network namespaces (tlscope1, tlscope2, tlscope3)
- Set up a bridge network for communication
- Generate SSH keys for 3 test users (alice, bob, charlie)
- Create launch scripts for each instance

### 3. Launch Test Instances

Open **3 separate terminal windows** and run one launch script in each (scripts will use sudo internally):

**Terminal 1 (Alice):**
```bash
./test_peers/launch_alice.sh
```

**Terminal 2 (Bob):**
```bash
./test_peers/launch_bob.sh
```

**Terminal 3 (Charlie):**
```bash
./test_peers/launch_charlie.sh
```

**Important**: Run the scripts directly (NOT with sudo). The scripts handle sudo internally for:
- Entering the network namespace
- Packet capture permissions (requires root)
- While preserving your user environment and config directories

### 4. Configure Each Instance

On first run, you'll need to configure each user:

1. **Create User** → Choose "Create new user"
2. **Username**: Use the suggested username (alice, bob, or charlie)
3. **SSH Private Key**: Use the suggested path (displayed on screen)
4. **Start Network Scan**: Choose "Yes"
5. **Select Network Interface**: Choose `eth0` (the virtual interface)

### 5. Start Packet Capture

In each instance:
1. Navigate to `1) Dashboard`
2. Press `c` to start packet capture
3. Wait a few seconds for devices to appear

### 6. Observe Peer Discovery!

Within ~30 seconds, you should see:
- **UDP broadcast packets** on port 8442 (discovery announcements)
- **Peers appearing** in the "TLScope Peers" section
- **TLS connections** established on port 8443
- **Peer verification** completed (green checkmark)

## What You'll See

### Network Topology

```
┌─────────────────────────────────────────────┐
│  Bridge: tlscope-br0 (192.168.100.254/24)  │
└──────┬──────────────────┬──────────────────┬┘
       │                  │                  │
   ┌───┴───┐          ┌───┴───┐         ┌───┴───┐
   │ veth  │          │ veth  │         │ veth  │
   └───┬───┘          └───┬───┘         └───┬───┘
       │                  │                  │
┌──────▼────────┐  ┌──────▼────────┐  ┌─────▼─────────┐
│  tlscope1     │  │  tlscope2     │  │  tlscope3     │
│  (alice)      │  │  (bob)        │  │  (charlie)    │
│ 192.168.100.1 │  │ 192.168.100.2 │  │192.168.100.3  │
└───────────────┘  └───────────────┘  └───────────────┘
```

### Peer Discovery Process

1. **UDP Broadcast** (every 30 seconds):
   ```
   Alice → 255.255.255.255:8442 (DISCOVERY announcement)
   Bob   → 255.255.255.255:8442 (DISCOVERY announcement)
   Charlie → 255.255.255.255:8442 (DISCOVERY announcement)
   ```

2. **TLS Connection** (when peer discovered):
   ```
   Alice connects to Bob on 192.168.100.2:8443
   - TLS handshake
   - Challenge-response authentication
   - Signature verification with SSH keys
   - Connection established ✓
   ```

3. **Peer Status**:
   ```
   TLScope Peers (2 connected, 2 total)
   ◉ bob       192.168.100.2:8443  ✓ Verified  2m ago
   ◉ charlie   192.168.100.3:8443  ✓ Verified  1m ago
   ```

## Network Details

### IP Address Assignment

| Instance | Namespace | IP Address      | Username |
|----------|-----------|-----------------|----------|
| 1        | tlscope1  | 192.168.100.1   | alice    |
| 2        | tlscope2  | 192.168.100.2   | bob      |
| 3        | tlscope3  | 192.168.100.3   | charlie  |

### Ports Used

| Port | Protocol | Purpose                |
|------|----------|------------------------|
| 8442 | UDP      | Peer discovery (broadcast) |
| 8443 | TCP/TLS  | Secure peer connections   |

## Verification Commands

### Test Network Connectivity

From alice's namespace, ping bob:
```bash
sudo ip netns exec tlscope1 ping 192.168.100.2
```

### Check Network Interfaces

View alice's network configuration:
```bash
sudo ip netns exec tlscope1 ip addr
```

### Monitor UDP Broadcasts

Capture UDP discovery packets:
```bash
sudo ip netns exec tlscope1 tcpdump -i eth0 -n udp port 8442
```

### Monitor TLS Connections

Watch TCP connections on port 8443:
```bash
sudo ip netns exec tlscope1 tcpdump -i eth0 -n tcp port 8443
```

### List Active Network Namespaces

```bash
sudo ip netns list
```

## Troubleshooting

### Peers Not Discovered

**Problem**: Instances don't see each other in the peers list.

**Solutions**:
1. Check UDP port is not blocked:
   ```bash
   sudo ip netns exec tlscope1 ss -ulnp | grep 8442
   ```

2. Verify bridge network is up:
   ```bash
   ip link show tlscope-br0
   ```

3. Check broadcast is enabled:
   ```bash
   sudo ip netns exec tlscope1 ip addr show eth0
   ```

4. Restart packet capture in each instance (press `c`)

### TLS Connection Fails

**Problem**: Peers discovered but TLS connection fails.

**Solutions**:
1. Verify SSH keys are valid:
   ```bash
   ssh-keygen -l -f test_peers/alice/id_rsa
   ```

2. Check TLS port is listening:
   ```bash
   sudo ip netns exec tlscope1 ss -tlnp | grep 8443
   ```

3. Review TLScope logs for signature verification errors

### Permission Denied

**Problem**: Cannot create namespaces or "permission denied" for packet capture.

**Solutions**:

1. **For setup script**: Ensure you're using `sudo`:
   ```bash
   sudo ./test_tls_peers.sh
   ```

2. **For launch scripts**: Run WITHOUT sudo (they handle it internally):
   ```bash
   ./test_peers/launch_alice.sh  # Correct
   ```

3. **Packet capture requires root**: The launch scripts automatically run TLScope with sudo for packet capture permissions. You'll be prompted for your password when launching.

### Database Locked

**Problem**: "Database is locked" error when launching multiple instances.

**Solution**: Each instance uses a separate database in its own config directory. If you see this error, you may have launched multiple instances with the same username. Use the provided launch scripts which handle this automatically.

## Advanced Testing

### Manual Namespace Execution

Run commands directly in a namespace:

```bash
# Execute command in alice's namespace
sudo ip netns exec tlscope1 <command>

# Example: Check routing table
sudo ip netns exec tlscope1 ip route

# Example: Test DNS
sudo ip netns exec tlscope1 ping google.com
```

### Add More Instances

Edit `test_tls_peers.sh` and increase `NUM_INSTANCES`:

```bash
NUM_INSTANCES=5  # Create 5 instances instead of 3
```

Then add entries to the arrays for instances 4 and 5.

### Change Network Subnet

Edit the IP addresses in `test_tls_peers.sh`:

```bash
BRIDGE_IP="10.100.0.254/24"

declare -A IPS=(
    [1]="10.100.0.1/24"
    [2]="10.100.0.2/24"
    [3]="10.100.0.3/24"
)
```

## Testing Checklist

Use this checklist when testing peer functionality:

- [ ] Setup script completes without errors
- [ ] All 3 namespaces created (`sudo ip netns list`)
- [ ] Bridge network active (`ip link show tlscope-br0`)
- [ ] SSH keys generated (check `test_peers/*/id_rsa`)
- [ ] Alice instance launches successfully
- [ ] Bob instance launches successfully
- [ ] Charlie instance launches successfully
- [ ] Each instance configured with unique username
- [ ] Packet capture started on all instances
- [ ] UDP discovery packets visible (use tcpdump)
- [ ] Peers appear in "TLScope Peers" section
- [ ] TLS connections established
- [ ] Peer verification successful (green checkmark)
- [ ] Can see peer's device count updating
- [ ] Network topology shows peer nodes

## Cleanup

When finished testing, clean up the network namespaces:

```bash
sudo ./cleanup_peers.sh
```

This will:
- Remove all network namespaces
- Remove the bridge network
- Optionally remove test configurations and SSH keys

## How It Works

### Network Namespace Isolation

Linux network namespaces provide:
- **Separate network stacks**: Each namespace has its own interfaces, routing tables, firewall rules
- **Port isolation**: Multiple processes can bind to the same port in different namespaces
- **Real networking**: UDP broadcasts, TCP connections, TLS handshakes all work normally

### Virtual Ethernet (veth) Pairs

- Each namespace gets a virtual ethernet pair
- One end (`veth-tlsX`) attached to the bridge (host side)
- Other end (`eth0`) moved into the namespace
- Acts like a real network cable between namespace and bridge

### Bridge Network

- Connects all namespaces together
- Forwards broadcasts between all ports
- Allows direct communication between instances
- Acts like a virtual switch

### No Code Changes Required

This approach works with TLScope's hardcoded ports because:
- Each namespace has its own network stack
- Port 8442 (UDP) can be bound in tlscope1, tlscope2, and tlscope3 simultaneously
- Port 8443 (TCP) can be bound in all namespaces without conflict
- Broadcasts reach all namespaces via the bridge

## Protocol Details

### Discovery Protocol (UDP Port 8442)

Broadcast format:
```json
{
  "type": "DISCOVERY",
  "username": "alice",
  "ssh_public_key": "ssh-rsa AAAA...",
  "avatar": {
    "type": "Robot",
    "primary_color": "Blue",
    "randomart": "+--[RSA 4096]--+\n..."
  },
  "tls_port": 8443,
  "version": "1.0.0"
}
```

### TLS Protocol (TCP Port 8443)

Authentication flow:
```
Client                          Server
  |                               |
  |---- TCP SYN ------------------>|
  |<--- TCP SYN-ACK ---------------|
  |---- TCP ACK ------------------>|
  |                               |
  |<--- TLS Handshake ----------->| (X.509 cert validation)
  |                               |
  |<--- CHALLENGE -----------------|  (32 random bytes)
  |                               |
  |---- PEER_IDENTIFICATION ----->|  (username + signature)
  |                               |
  |<--- Verify signature ---------|  (using SSH public key)
  |                               |
  |<--- Connection OK ----------->|
```

## Expected Results

When testing is successful, you should observe:

1. **In Terminal Output**:
   - "Discovered peer: bob (192.168.100.2)"
   - "TLS connection established with bob"
   - "Peer verification successful: bob"

2. **In Dashboard**:
   - All peers listed with green checkmarks
   - Device counts updating in real-time
   - Network constellation showing peer nodes

3. **In Network Graph**:
   - Peer devices marked with green circles (◉)
   - Connections between peers visible
   - TLS peer connections color-coded green

## Additional Resources

- **TLScope Documentation**: See main README.md
- **Network Namespaces**: `man ip-netns`
- **Bridge Networking**: `man bridge`
- **TLS/SSL**: OpenSSL documentation

## Support

If you encounter issues not covered in this guide:

1. Check TLScope logs in `~/.config/tlscope/` (for each user)
2. Review the source code:
   - `src/Services/TlsPeerService.cs` - Peer discovery and communication
   - `src/Utilities/CryptoUtility.cs` - Certificate and signature handling
3. Open an issue on GitHub with:
   - Error messages
   - Output from `sudo ip netns list`
   - Output from `ip addr show tlscope-br0`
   - Relevant log files
