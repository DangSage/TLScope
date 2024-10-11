# TLScope Overview

TLScope is a network security project visualizing TLS connections, using OpenSSL.

TLScope Visualizes TLS connections within a local network. Each user is represented as a vertex, and each TLS connection as an edge in a graph. Using the basic Graph Theory concept:

**G = { V1, V2, ..., Vi }**

With this info, TLScope details information on the network.

# C# Stack

## Core Technology
- Language & Framework: C# 10.0+, .NET 6.0+
- Development Environment: Visual Studio Code with C# extension

## Networking & TLS
- System.Net.Security: `SslStream` for TLS connections
- System.Net.NetworkInformation: `Ping` class for network discovery

## Graph Representation & Algorithms
- QuikGraph (4.0.1+): Graph data structures and algorithms

## GUI & Visualization
- Terminal.Gui (1.4.0+): Text-based UI for terminal applications
- Microsoft.Msagl: Graph visualization

## Database & User Management
- Microsoft.Data.Sqlite (6.0.0+): SQLite provider for .NET
- Microsoft.AspNetCore.Identity (6.0.0+): User authentication and management

## Security
- Konscious.Security.Cryptography.Argon2 (1.3.0+): Argon2 for password hashing

## Testing
- xUnit.net (2.4.1+): Unit testing framework
- Moq (4.16.1+): Mocking framework

## Additional Libraries
- Serilog (2.10.0+): Structured logging
- CommandLineParser (2.9.1+): Command line argument parsing

## Build & Dependency Management
- .NET CLI: Command-line interface for .NET
- NuGet: Package manager for .NET