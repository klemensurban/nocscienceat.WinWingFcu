# nocscienceat.WinWingFcu

C#/.NET driver for the **WinWing FCU** (Flight Control Unit) and optional
**EFIS-L / EFIS-R** panels, bridging the hardware to X-Plane via the
[nocscienceat.XPlanePanel](https://github.com/klemensurban/nocscienceat.XPlanePanel)
framework.

## Installation

```bash
dotnet add package nocscienceat.WinWingFcu
```

## Features

- USB HID communication with WinWing FCU (+EFIS) hardware
- 7-segment LCD display driving (speed, heading, altitude, V/S, baro)
- LCD flag overlays (SPD/MACH, HDG/TRK, managed dots, V/S/FPA labels)
- LED annunciator control (AP1/2, A/THR, LOC, APPR, EXPED, EFIS buttons)
- Button and rotary encoder input with dispatch table
- Channel-based single-consumer HID write architecture
- Automatic device detection for all known WinWing FCU/EFIS combinations
- Configurable dataref/command mappings (default: ToLiSS Airbus)

## Usage

This library provides a panel handler for WinWing FCU/EFIS hardware,
integrating with X-Plane through the nocscienceat.XPlanePanel framework.

### Basic Setup

Create a hosted service application with the following `Program.cs`:

```csharp
using nocscienceat.WinWingFcu.Panels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using nocscienceat.XPlanePanel;

var builder = Host.CreateApplicationBuilder(args);

// Configuration
builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile("datarefs.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables("XP_");

// Core services
builder.Services.AddXPlaneWebConnector(builder.Configuration);
builder.Services.AddXPlanePanel();

// Panel handlers (register all, filter by config at runtime)
builder.Services.AddSingleton<IPanelHandler, FcuPanelHandler>();

// Logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

var host = builder.Build();
await host.RunAsync();
```

### Configuration

Create an `appsettings.json` file with your X-Plane Web Connector settings
and panel configuration:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "System.Net.Http.HttpClient": "Warning"
    }
  },
  "XPlane": {
    "IpAddress": "127.0.0.1",
    "WebPort": 8086,
    "ReadinessProbeDataRef": "AirbusFBW/BatVolts",
    "ReadinessProbeMaxRetries": 0,
    "Transport": "Http",
    "FireForgetOnHttpTransport": true,
    "ApiVersion": "v2"
  },
  "Panels": {
    "FCU": {
      "Enabled": true,
      "VendorId": "0x4098",
      "ProductId": "0x0000",
      "DefaultBrightness": 180
    }
  }
}
```

| Setting | Description |
|---------|------------|
| `Enabled` | Enable/disable the FCU panel handler |
| `VendorId` | USB Vendor ID for WinWing devices (default `0x4098`) |
| `ProductId` | USB Product ID. `0` = auto-detect from known device list |
| `DefaultBrightness` | Initial LED brightness 0-255 (overridden by X-Plane dataref at runtime) |

### DataRefs Configuration (Optional)

Optionally create a `datarefs.json` file to override the default
ToLiSS Airbus dataref and command mappings (the panel includes all
required definitions):

```json
{
  "XplaneDataRefsCommands": {
    "FCU": {
      "DataRefs": {
      },
      "Commands": {
      }
    }
  }
}
```

## Architecture

The project is structured as a `PanelHandlerBase<T>` subclass split across
partial classes:

| File | Responsibility |
|------|---------------|
| `FcuPanelHandler.cs` | Lifecycle, HID report processing, startup screen |
| `FcuPanelHandlerDisplay.cs` | LCD display building and EFIS baro logic |
| `FcuPanelHandlerRegDataRefCommand.cs` | Dataref/command registration and button dispatch table |
| `FcuPanelHandlerWireDataRefCommand.cs` | Dataref subscriptions, LED wiring, Fire helpers |

The hardware layer (`Hardware/`) handles raw HID packet construction,
7-segment encoding, and the channel-based write queue.

See [`Hardware/HID-PACKET-MAP.md`](Hardware/HID-PACKET-MAP.md) for the
USB HID protocol documentation.

## License

This project is licensed under the **GNU General Public License v3.0**
(GPL-3.0). See the [LICENSE](LICENSE) file for details.

### Attribution

This project is a C# port derived from
[XSchenFly](https://github.com/schenlap/XSchenFly) by **memo5@gmx.at**,
originally released under the GPL-3.0 license. The HID protocol
implementation, 7-segment encoding tables, LCD flag definitions, and
X-Plane dataref mappings are based on that work.

C# port and modifications by **Klemens Urban**
(<klemens.urban@outlook.com>, https://github.com/klemensurban).

### Dependencies

This project depends on the
[nocscienceat.XPlanePanel](https://github.com/klemensurban/nocscienceat.XPlanePanel)
framework, which is licensed separately under the **MIT License** by
Klemens Urban.

## Repository

https://github.com/klemensurban/nocscienceat.WinWingFcu
