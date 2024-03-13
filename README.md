# TLScope
A network security project visualizing TLS connections.

## Overview
TLScope uses C++ and OpenSSL to visualize TLS connections within a local network. Each user is represented as a vertex, and each TLS connection as an edge in a graph. Using the basic Graph Theory concept:

``` G = { V1, V2, ..., Vi } ```

TLScope pings users to build a graph detailing network users. It also provides information about the graph, such as whether it's bipartite, complete, etc.


## Dependencies
This project depends on the following libraries:

- OpenSSL: Used for secure communication. Install with `sudo apt-get install libssl-dev` on Ubuntu.
- Boost: Used for various utility functions and data structures. Install with `sudo apt-get install libboost-all-dev` on Ubuntu.
- FTXUI: Used for easy UI building and visuals. Clone and build this library from Arthur Sonzogni's gitHub repository at https://github.com/ArthurSonzogni/FTXUI
