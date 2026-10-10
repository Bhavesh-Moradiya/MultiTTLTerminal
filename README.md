# MultiTTLTerminal

**MultiTTLTerminal** is a multi-protocol desktop terminal and network diagnostic tool built for Windows. Designed for embedded systems developers, firmware engineers, and hardware testing teams, it provides a unified interface for communicating over Serial UART, raw TCP, Telnet, SSL/TLS, and UDP sockets.

---

## Features

* **Multi-Transport Communication**:
  * **Serial UART / TTL**: Configurable COM ports, baud rates, data bits, stop bits, parity, flow control (RTS/CTS, XON/XOFF), and DTR/RTS line toggles.
  * **TCP / Network**: Raw TCP client sockets, Telnet sessions, and SSL/TLS encrypted streams.
  * **UDP**: Persistent IPv4 and dual-mode IPv6 UDP socket streams for fast datagram testing.
* **Dual Display Modes**: Instant switching between raw ASCII/Text view and hexadecimal (HEX) byte formatting.
* **Millisecond-Accurate Logging**: Automatic line-by-line logging to `.log` files with precise timestamps (`yyyy-MM-dd HH:mm:ss.fff`) for every RX and TX transmission.
* **Opacity Adjustment**: Dynamic window transparency control for overlaying the terminal directly on schematics, pinouts, or datasheets during debugging.

---

## Repository Structure

```text
MultiTTLTerminal/
├── src/
│   ├── MainForm.cs            # Primary configuration & session manager launcher
│   ├── PortTerminalForm.cs    # Active session window (TX/RX history, HEX/ASCII, logging)
│   └── Program.cs             # Application entry point
├── lib/
│   └── MultiTTLTerminal.Engine.dll  # Pre-compiled core networking & serial engine assembly
└── MultiTTLTerminal.csproj
```

---

## Architecture

The project uses a decoupled two-tier architecture:

* **User Interface Layer (`MultiTTLTerminal`)**: Open-source WinForms application handling control setup, rendering, user inputs, and disk logging.
* **Core Engine (`MultiTTLTerminal.Engine`)**: Pre-compiled class library handling stream buffers, socket loops, SSL authentication, and low-level hardware abstraction.

---

## Getting Started

### Prerequisites

* Windows 10 / 11 (64-bit)
* [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (for building from source)

### Building from Source

1. Clone the repository:
   ```bash
   git clone https://github.com/Bhavesh-Moradiya/MultiTTLTerminal.git
   cd MultiTTLTerminal
   ```

2. Restore packages and build:
   ```bash
   dotnet build -c Release
   ```

3. Run the application:
   ```bash
   dotnet run -c Release
   ```

---

## Download Standalone Executable

If you do not wish to build from source, pre-packaged standalone executables are available under the **[Releases](https://github.com/Bhavesh-Moradiya/MultiTTLTerminal/releases)** tab. Download the latest `MultiTTLTerminal_v1.0_Win-x64.zip`, extract, and run `MultiTTLTerminal.exe` directly without installing the .NET runtime.

---

## License

This project is open-source. Feel free to submit issues or pull requests to enhance UI functionality or feature sets.
