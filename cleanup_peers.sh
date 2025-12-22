#!/bin/bash
# TLScope TLS Peer Testing - Cleanup Script
# Removes network namespaces and test configurations

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

echo -e "${BLUE}========================================${NC}"
echo -e "${BLUE}TLScope TLS Peer Testing Cleanup${NC}"
echo -e "${BLUE}========================================${NC}"
echo ""

# Check if running as root
if [ "$EUID" -ne 0 ]; then
    echo -e "${RED}Error: This script must be run as root (use sudo)${NC}"
    exit 1
fi

# Configuration
BRIDGE_NAME="tlscope-br0"
NUM_INSTANCES=3

declare -A NAMESPACES=(
    [1]="tlscope1"
    [2]="tlscope2"
    [3]="tlscope3"
)

# Step 1: Remove network namespaces
echo -e "${YELLOW}[1/3] Removing network namespaces...${NC}"
for i in $(seq 1 $NUM_INSTANCES); do
    ns="${NAMESPACES[$i]}"
    if ip netns list | grep -q "^$ns"; then
        echo "  - Removing namespace: $ns"
        ip netns del "$ns" 2>/dev/null || true
    else
        echo "  - Namespace not found: $ns (already removed)"
    fi
done
echo -e "${GREEN}✓ Namespaces removed${NC}"
echo ""

# Step 2: Remove bridge
echo -e "${YELLOW}[2/3] Removing bridge network...${NC}"
if ip link show "$BRIDGE_NAME" &>/dev/null; then
    echo "  - Removing bridge: $BRIDGE_NAME"
    ip link set "$BRIDGE_NAME" down 2>/dev/null || true
    ip link del "$BRIDGE_NAME" 2>/dev/null || true
    echo -e "${GREEN}✓ Bridge removed${NC}"
else
    echo "  - Bridge not found (already removed)"
    echo -e "${GREEN}✓ Nothing to clean${NC}"
fi
echo ""

# Step 3: Ask about test data
echo -e "${YELLOW}[3/3] Test data cleanup...${NC}"
TEST_DIR="$(dirname "$(readlink -f "$0")")/test_peers"

if [ -d "$TEST_DIR" ]; then
    echo ""
    echo -e "${BLUE}Test directory found: $TEST_DIR${NC}"
    echo "This contains:"
    echo "  - SSH keys for test users"
    echo "  - Configuration files"
    echo "  - Launch scripts"
    echo ""
    read -p "Do you want to remove the test_peers directory? [y/N] " -n 1 -r
    echo ""

    if [[ $REPLY =~ ^[Yy]$ ]]; then
        echo "  - Removing test directory..."
        rm -rf "$TEST_DIR"
        echo -e "${GREEN}✓ Test directory removed${NC}"
    else
        echo -e "${YELLOW}✓ Test directory preserved${NC}"
        echo ""
        echo "  To manually remove later:"
        echo "  rm -rf $TEST_DIR"
    fi
else
    echo "  - No test directory found"
    echo -e "${GREEN}✓ Nothing to clean${NC}"
fi
echo ""

# Summary
echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}Cleanup Complete!${NC}"
echo -e "${GREEN}========================================${NC}"
echo ""
echo "All network namespaces have been removed."
echo "You can now run test_tls_peers.sh again to recreate the test environment."
echo ""
