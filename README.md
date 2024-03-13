# TLScope
A network security project visualizing TLS connections.

## Overview
TLScope uses C++ and OpenSSL to visualize TLS connections within a local network. Each user is represented as a vertex, and each TLS connection as an edge in a graph. Using the basic Graph Theory concept:

``` G = { V1, V2, ..., Vi } ```

TLScope pings users to build a graph detailing network users. It also provides information about the graph, such as whether it's bipartite, complete, etc.

# Requirements
- OpenSSL
- Boost Serialization Library
- FTXUI