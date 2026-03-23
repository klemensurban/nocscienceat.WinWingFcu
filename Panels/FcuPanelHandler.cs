// Copyright (c) 2026 Klemens Urban <klemens.urban@outlook.com>
// Based on XSchenFly by memo5@gmx.at (https://github.com/schenlap/XSchenFly)
// SPDX-License-Identifier: GPL-3.0-only

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using nocscienceat.WinWingFcu.Configuration;
using nocscienceat.WinWingFcu.Hardware;
using nocscienceat.XPlanePanel;
using nocscienceat.XPlanePanel.Services;
using nocscienceat.XPlaneWebConnector.Interfaces;

namespace nocscienceat.WinWingFcu.Panels;

/// <summary>
/// WinWing FCU panel handler. Communicates with the WinWing FCU (+EFIS-L/R) hardware
/// via USB HID. Reads button/encoder input, drives 7-segment LCD displays and LEDs,
/// and bridges everything to X-Plane datarefs and commands.
///
/// Architecture follows the OVH pattern:
///  - RegisterDataRefsAndCommands() maps logical keys -> X-Plane paths
///  - CreateDispatchTable() maps HID button IDs -> named handler lambdas
///  - SubscribeToDataRefsAsync() sets up dataref subscriptions by logical key
///  - Each dataref callback directly sends the affected display packet(s) via a channel
/// </summary>
public partial class FcuPanelHandler : PanelHandlerBase<WinWingFcuConfig>
{
    private WinWingHidDevice? _hidDevice;

    // ===== HID button state tracking =====
    private uint _lastFcuButtons;
    private uint _lastEfisRButtons;
    private uint _lastEfisLButtons;
    private bool _baselineEstablished;

    // ===== LCD flags (segment labels, managed dots, mode indicators) =====
    private readonly Dictionary<string, LcdFlag> _flags = LcdFlags.CreateDefaults();

    // ===== Cached dataref state — accessed from the work queue task only =====
    private int _spdDashed, _hdgDashed, _vsDashed;
    private float _airspeedKtsMach;
    private int _spdManaged, _isMach;
    private float _headingMag;
    private int _hdgManaged, _hdgTrkMode;
    private float _altitude;
    private int _altManaged;
    private float _verticalVelocity;
    private int _apVerticalMode;
    private float _baroCopilot, _baroPilot;
    private int _baroStdFO, _baroUnitFO, _baroStdCapt, _baroUnitCapt;
    private string _lastEfisRBaroStr = "", _lastEfisLBaroStr = "";
    private byte _ledBrightness;
    private bool _expedLedState;

    public override string PanelName => "FCU";
    public override bool IsConnected => _hidDevice?.IsOpen ?? false;

    public FcuPanelHandler(IXPlaneWebConnector connector, IConfiguration configuration, ILogger<FcuPanelHandler> logger, IDataRefCommandProvider? overrideProvider = null)
        : base(connector, configuration, logger, overrideProvider)
    {
        _ledBrightness = (byte)Math.Clamp(_config.DefaultBrightness, 0, 255);
    }

    // =====================================================================
    // Lifecycle
    // =====================================================================

    protected override async Task OnConnectedAsync(CancellationToken cancellationToken)
    {
        // 1. Open the WinWing USB HID device
        _hidDevice = new WinWingHidDevice(_logger);

        if (!_hidDevice.FindAndOpen(_config.VendorId, _config.ProductId))
        {
            _logger.LogWarning("{Panel} no compatible WinWing device found", PanelName);
            _hidDevice.Dispose();
            _hidDevice = null;
            return;
        }

        // 2. Build the button dispatch table for the detected device configuration
        CreateDispatchTable(_hidDevice.DeviceMask);

        // 3. Wire up HID report events and start the I/O pump
        _hidDevice.ReportReceived += OnHidReport;
        _hidDevice.StartIo(cancellationToken);
        _hidDevice.InitLcd();

        // 4. Show startup screen (backlights + flag LEDs on)
        ShowStartupScreen();

        // 5. Subscribe to all X-Plane datarefs (using logical keys from RegisterDataRefsAndCommands)
        await SubscribeToDataRefsAsync(cancellationToken);

        _logger.LogInformation("{Panel} connected (devices: {Mask})", PanelName, _hidDevice.DeviceMask);
    }

    protected override Task OnDisconnectingAsync()
    {
        if (_hidDevice is not null)
            _hidDevice.ReportReceived -= OnHidReport;

        _hidDevice?.Dispose();
        _hidDevice = null;
        return Task.CompletedTask;
    }

    // =====================================================================
    // HID report processing
    // =====================================================================

    /// <summary>
    /// Handles a single HID report. Fires on the read loop thread — must not block.
    /// Extracts button bitmasks from the report and detects changes against the last state.
    /// </summary>
    private void OnHidReport(byte[] data)
    {
        if (data.Length < 5 || data[0] != 0x01) return;

        // Extract 32-bit button masks from the report
        uint fcuButtons = (uint)(data[1] | (data[2] << 8) | (data[3] << 16) | (data[4] << 24));

        uint efisRButtons = 0;
        if (_hidDevice!.DeviceMask.HasFlag(DeviceMask.EfisR) && data.Length >= 13)
            efisRButtons = (uint)(data[9] | (data[10] << 8) | (data[11] << 16) | (data[12] << 24));

        uint efisLButtons = 0;
        if (_hidDevice.DeviceMask.HasFlag(DeviceMask.EfisL) && data.Length >= 9)
            efisLButtons = (uint)(data[5] | (data[6] << 8) | (data[7] << 16) | (data[8] << 24));

        // First report: capture baseline (rotary switches have non-zero resting state)
        if (!_baselineEstablished)
        {
            _lastFcuButtons = fcuButtons;
            _lastEfisRButtons = efisRButtons;
            _lastEfisLButtons = efisLButtons;
            _baselineEstablished = true;
            _logger.LogInformation("[FCU] Baseline: FCU=0x{Fcu:X8} EFISL=0x{EfisL:X8} EFISR=0x{EfisR:X8}",
                fcuButtons, efisLButtons, efisRButtons);
            return;
        }

        // Detect changed bits and dispatch to handlers
        DispatchChanges(fcuButtons, _lastFcuButtons, 0, 32);
        if (_hidDevice.DeviceMask.HasFlag(DeviceMask.EfisR))
            DispatchChanges(efisRButtons, _lastEfisRButtons, 32, 32);
        if (_hidDevice.DeviceMask.HasFlag(DeviceMask.EfisL))
            DispatchChanges(efisLButtons, _lastEfisLButtons, 64, 32);

        _lastFcuButtons = fcuButtons;
        _lastEfisRButtons = efisRButtons;
        _lastEfisLButtons = efisLButtons;
    }

    /// <summary>
    /// Compares two button masks and dispatches handlers for changed bits.
    /// Uses the dispatch table built in <see cref="CreateDispatchTable"/>.
    /// </summary>
    private void DispatchChanges(uint current, uint last, int baseId, int count)
    {
        uint changed = current ^ last;
        if (changed == 0) return;

        for (int i = 0; i < count; i++)
        {
            uint mask = 1u << i;
            if ((changed & mask) == 0) continue;

            bool pressed = (current & mask) != 0;
            int buttonId = baseId + i;

            if (!_buttonHandlers.TryGetValue(buttonId, out var entry))
            {
                _logger.LogDebug("[FCU] Button {Id} {State} (unmapped)", buttonId, pressed ? "dn" : "up");
                continue;
            }

            _logger.LogDebug("[FCU] {Label} (ID {Id}) {State}", entry.Label, buttonId, pressed ? "pressed" : "released");

            // Handler decides whether to act on press/release
            _ = entry.Handler(pressed);
        }
    }

    // =====================================================================
    // Startup screen
    // =====================================================================

    /// <summary>
    /// Turns on backlights and flag LEDs to a dim level during initialization.
    /// </summary>
    private void ShowStartupScreen()
    {
        if (_hidDevice == null) return;

        var leds = new List<FcuLed> { FcuLed.ScreenBacklight, FcuLed.Backlight };
        var ledsGreen = new List<FcuLed> { FcuLed.FlagGreen };

        if (_hidDevice.DeviceMask.HasFlag(DeviceMask.EfisR))
        {
            leds.Add(FcuLed.EfisRBacklight);
            leds.Add(FcuLed.EfisRScreenBacklight);
            ledsGreen.Add(FcuLed.EfisRFlagGreen);
        }
        if (_hidDevice.DeviceMask.HasFlag(DeviceMask.EfisL))
        {
            leds.Add(FcuLed.EfisLBacklight);
            leds.Add(FcuLed.EfisLScreenBacklight);
            ledsGreen.Add(FcuLed.EfisLFlagGreen);
        }

        _hidDevice.SetLeds([.. leds], 80);
        _hidDevice.SetLeds([.. ledsGreen], 100);
    }
}
