# TLScope Overview

TLScope is a network security project visualizing TLS connections, using OpenSSL.

TLScope Visualizes TLS connections within a local network. Each user is represented as a vertex, and each TLS connection as an edge in a graph. Using the basic Graph Theory concept:

**G = { V1, V2, ..., Vi }**

With this info, TLScope details information on the network.

# C# Technology Stack

## Core Technology

1. **Language & Framework**: 
   - C# 10.0 or later
   - .NET 6.0 or later (for cross-platform support)

2. **Development Environment**:
   - Visual Studio Code with C# extension (for cross-platform development)

## Networking & TLS

3. **System.Net.Security**:
   - Built-in .NET library for handling TLS connections
   - Use `SslStream` for secure communication

4. **System.Net.NetworkInformation**:
   - For network discovery and ping functionality
   - Use `Ping` class for ICMP requests

## Graph Representation & Algorithms

5. **QuikGraph (4.0.1 or later)**:
   - High-performance graph data structures and algorithms
   - Use for representing network topology

## GUI & Visualization

6. **Terminal.Gui (1.4.0 or later)**:
   - Text-based user interface for terminal applications
   - Provides cross-platform console GUI

7. **Microsoft.Msagl**:
   - Microsoft Automatic Graph Layout for graph visualization
   - Use for rendering network topology

## Database & User Management

8. **Microsoft.Data.Sqlite (6.0.0 or later)**:
   - SQLite provider for .NET
   - Use for storing user data and session information

9. **Microsoft.AspNetCore.Identity (6.0.0 or later)**:
   - For user authentication and management
   - Provides secure user registration and login functionality

## Security

10. **Konscious.Security.Cryptography.Argon2 (1.3.0 or later)**:
    - Implementation of Argon2 for password hashing
    - Use for secure storage of user passwords

## Testing

11. **xUnit.net (2.4.1 or later)**:
    - Unit testing framework for .NET
    - Use for writing and running automated tests

12. **Moq (4.16.1 or later)**:
    - Mocking framework for .NET
    - Use for creating mock objects in unit tests

## Additional Libraries

13. **Serilog (2.10.0 or later)**:
    - Structured logging library
    - Use for application logging and diagnostics

14. **CommandLineParser (2.9.1 or later)**:
    - Library for parsing command line arguments
    - Use for handling CLI options in TLScope

## Build & Dependency Management

15. **.NET CLI**:
    - Command-line interface for .NET development
    - Use for building, running, and publishing the application

16. **NuGet**:
    - Package manager for .NET
    - Use for managing project dependencies

