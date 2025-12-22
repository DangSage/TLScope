#!/bin/bash
# TLScope TLS Peer Testing - Network Namespace Setup
# This script creates isolated network namespaces to run multiple TLScope instances
# on the same device for testing TLS peer discovery and communication.

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

echo -e "${BLUE}========================================${NC}"
echo -e "${BLUE}TLScope TLS Peer Testing Setup${NC}"
echo -e "${BLUE}========================================${NC}"
echo ""

# Check if running as root
if [ "$EUID" -ne 0 ]; then
    echo -e "${RED}Error: This script must be run as root (use sudo)${NC}"
    exit 1
fi

# Configuration
BRIDGE_NAME="tlscope-br0"
BRIDGE_IP="192.168.100.254/24"
NUM_INSTANCES=3

# Network namespace configuration
declare -A NAMESPACES=(
    [1]="tlscope1"
    [2]="tlscope2"
    [3]="tlscope3"
)

declare -A IPS=(
    [1]="192.168.100.1/24"
    [2]="192.168.100.2/24"
    [3]="192.168.100.3/24"
)

declare -A USERNAMES=(
    [1]="alice"
    [2]="bob"
    [3]="charlie"
)

# Step 1: Clean up any existing setup
echo -e "${YELLOW}[1/6] Cleaning up existing namespaces...${NC}"
for i in $(seq 1 $NUM_INSTANCES); do
    ns="${NAMESPACES[$i]}"
    if ip netns list | grep -q "^$ns"; then
        echo "  - Removing namespace: $ns"
        ip netns del "$ns" 2>/dev/null || true
    fi
done

# Remove bridge if exists
if ip link show "$BRIDGE_NAME" &>/dev/null; then
    echo "  - Removing bridge: $BRIDGE_NAME"
    ip link set "$BRIDGE_NAME" down 2>/dev/null || true
    ip link del "$BRIDGE_NAME" 2>/dev/null || true
fi

echo -e "${GREEN}✓ Cleanup complete${NC}"
echo ""

# Step 2: Create bridge network
echo -e "${YELLOW}[2/6] Creating bridge network...${NC}"
ip link add "$BRIDGE_NAME" type bridge
ip addr add "$BRIDGE_IP" dev "$BRIDGE_NAME"
ip link set "$BRIDGE_NAME" up

# Enable forwarding
sysctl -q -w net.ipv4.ip_forward=1

echo -e "${GREEN}✓ Bridge created: $BRIDGE_NAME ($BRIDGE_IP)${NC}"
echo ""

# Step 3: Create network namespaces
echo -e "${YELLOW}[3/6] Creating network namespaces...${NC}"
for i in $(seq 1 $NUM_INSTANCES); do
    ns="${NAMESPACES[$i]}"
    echo "  - Creating namespace: $ns"
    ip netns add "$ns"
done
echo -e "${GREEN}✓ Namespaces created${NC}"
echo ""

# Step 4: Create veth pairs and connect to bridge
echo -e "${YELLOW}[4/6] Setting up virtual network interfaces...${NC}"
for i in $(seq 1 $NUM_INSTANCES); do
    ns="${NAMESPACES[$i]}"
    veth_host="veth-tls$i"
    veth_ns="eth0"
    ip_addr="${IPS[$i]}"

    echo "  - Setting up interface for $ns ($ip_addr)"

    # Create veth pair
    ip link add "$veth_host" type veth peer name "$veth_ns"

    # Attach host side to bridge
    ip link set "$veth_host" master "$BRIDGE_NAME"
    ip link set "$veth_host" up

    # Move namespace side into namespace
    ip link set "$veth_ns" netns "$ns"

    # Configure interface inside namespace
    ip netns exec "$ns" ip addr add "$ip_addr" dev "$veth_ns"
    ip netns exec "$ns" ip link set "$veth_ns" up
    ip netns exec "$ns" ip link set lo up

    # Set default route
    ip netns exec "$ns" ip route add default via 192.168.100.254
done
echo -e "${GREEN}✓ Network interfaces configured${NC}"
echo ""

# Step 5: Generate SSH keys for each instance
echo -e "${YELLOW}[5/6] Generating SSH keys for test instances...${NC}"
TEST_DIR="$(dirname "$(readlink -f "$0")")/test_peers"
mkdir -p "$TEST_DIR"

for i in $(seq 1 $NUM_INSTANCES); do
    username="${USERNAMES[$i]}"
    key_dir="$TEST_DIR/$username"
    key_file="$key_dir/id_rsa"

    mkdir -p "$key_dir"

    if [ ! -f "$key_file" ]; then
        echo "  - Generating key for $username"
        ssh-keygen -t rsa -b 4096 -f "$key_file" -N "" -C "tlscope-test-$username" >/dev/null
        chmod 600 "$key_file"
        chmod 644 "$key_file.pub"
    else
        echo "  - Key already exists for $username"
    fi
done

# Change ownership back to the user who ran sudo
if [ -n "$SUDO_USER" ]; then
    chown -R "$SUDO_USER:$SUDO_USER" "$TEST_DIR"
fi

echo -e "${GREEN}✓ SSH keys generated${NC}"
echo ""

# Step 6: Create launch helper scripts
echo -e "${YELLOW}[6/6] Creating launch scripts...${NC}"

for i in $(seq 1 $NUM_INSTANCES); do
    ns="${NAMESPACES[$i]}"
    username="${USERNAMES[$i]}"
    ip_addr="${IPS[$i]%%/*}"  # Remove /24 suffix

    launch_script="$TEST_DIR/launch_${username}.sh"

    cat > "$launch_script" << EOF
#!/bin/bash
# Launch TLScope instance for $username in namespace $ns

set -e

SCRIPT_DIR="\$(dirname "\$(readlink -f "\$0")")"
TLSCOPE_DIR="\$(dirname "\$SCRIPT_DIR")"
KEY_FILE="\$SCRIPT_DIR/$username/id_rsa"
CONFIG_DIR="\$SCRIPT_DIR/$username/.config/tlscope"

# Get the actual username (not root)
ACTUAL_USER="\${SUDO_USER:-\$USER}"
if [ "\$ACTUAL_USER" = "root" ]; then
    echo "Error: Cannot determine non-root user. Please run without sudo."
    exit 1
fi

# Create config directory
mkdir -p "\$CONFIG_DIR"

# Set ownership to actual user so config files are readable
chown -R "\$ACTUAL_USER:\$ACTUAL_USER" "\$SCRIPT_DIR/$username" 2>/dev/null || true

echo "========================================="
echo "Launching TLScope as: $username"
echo "Namespace: $ns"
echo "IP Address: $ip_addr"
echo "SSH Key: \$KEY_FILE"
echo "Config Dir: \$CONFIG_DIR"
echo "User: \$ACTUAL_USER"
echo "========================================="
echo ""
echo "Note: You'll need to configure the user on first run."
echo "When prompted for SSH private key, use: \$KEY_FILE"
echo ""
echo "Running with sudo for packet capture permissions..."
echo ""

# Launch in namespace with sudo (needed for packet capture)
cd "\$TLSCOPE_DIR"

# Run with sudo for packet capture, using env to set environment variables
sudo ip netns exec $ns env \
    HOME="\$SCRIPT_DIR/$username" \
    XDG_CONFIG_HOME="\$CONFIG_DIR" \
    USER="\$ACTUAL_USER" \
    LOGNAME="\$ACTUAL_USER" \
    dotnet run --no-build --no-restore
EOF

    chmod +x "$launch_script"

    if [ -n "$SUDO_USER" ]; then
        chown "$SUDO_USER:$SUDO_USER" "$launch_script"
    fi

    echo "  - Created: launch_${username}.sh"
done

echo -e "${GREEN}✓ Launch scripts created${NC}"
echo ""

# Print summary
echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}Setup Complete!${NC}"
echo -e "${GREEN}========================================${NC}"
echo ""
echo -e "${BLUE}Network Configuration:${NC}"
echo "  Bridge: $BRIDGE_NAME ($BRIDGE_IP)"
echo ""
for i in $(seq 1 $NUM_INSTANCES); do
    ns="${NAMESPACES[$i]}"
    username="${USERNAMES[$i]}"
    ip_addr="${IPS[$i]%%/*}"
    echo "  Instance $i ($username):"
    echo "    - Namespace: $ns"
    echo "    - IP: $ip_addr"
    echo "    - Launch: ./test_peers/launch_${username}.sh"
done
echo ""
echo -e "${BLUE}Testing Instructions:${NC}"
echo "  1. Build TLScope first (in normal environment, not in namespace):"
echo "     cd /home/khai/Documents/TLScope && dotnet build"
echo ""
echo "  2. Open 3 separate terminal windows"
echo "  3. In each terminal, run one of the launch scripts:"
echo "     Terminal 1: ./test_peers/launch_alice.sh"
echo "     Terminal 2: ./test_peers/launch_bob.sh"
echo "     Terminal 3: ./test_peers/launch_charlie.sh"
echo ""
echo "  4. Configure each user when prompted (use the generated SSH keys)"
echo "  5. Start packet capture on each instance"
echo "  6. Watch the 'Peers' section for discovered peers!"
echo ""
echo -e "${BLUE}Verify Network:${NC}"
echo "  # Ping from alice to bob:"
echo "  sudo ip netns exec tlscope1 ping 192.168.100.2"
echo ""
echo "  # Check network interfaces:"
echo "  sudo ip netns exec tlscope1 ip addr"
echo ""
echo -e "${BLUE}Cleanup:${NC}"
echo "  # When done testing, run:"
echo "  sudo ./cleanup_peers.sh"
echo ""
