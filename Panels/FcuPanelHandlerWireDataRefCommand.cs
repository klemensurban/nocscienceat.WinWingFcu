// Copyright (c) 2026 Klemens Urban <klemens.urban@outlook.com>
// Based on XSchenFly by memo5@gmx.at (https://github.com/schenlap/XSchenFly)
// SPDX-License-Identifier: GPL-3.0-only

using Microsoft.Extensions.Logging;
using nocscienceat.WinWingFcu.Hardware;

namespace nocscienceat.WinWingFcu.Panels;

public partial class FcuPanelHandler
{
    // =====================================================================
    // Helper methods for the dispatch table -- keep handlers concise
    // =====================================================================

    /// <summary>Sends an X-Plane command on press, ignores release. Fire-and-forget.</summary>
    private Task FireCommand(string commandKey, bool pressed)
    {
        if (pressed)
            _ = _connector.SendCommandAsync(GetCommand(commandKey));
        return Task.CompletedTask;
    }

    /// <summary>Toggles a dataref between 0/1 on press. Fire-and-forget.</summary>
    private Task FireToggleDataRef(string dataRefKey, bool pressed)
    {
        if (pressed)
            _ = _connector.SetDataRefValueAsync(GetDataRefPath(dataRefKey), 1);
        return Task.CompletedTask;
    }

    /// <summary>Sets a dataref to an absolute value on press. Fire-and-forget.</summary>
    private Task FireSetDataRef(string dataRefKey, int value, bool pressed)
    {
        if (pressed)
            _ = _connector.SetDataRefValueAsync(GetDataRefPath(dataRefKey), value);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Subscribes to all X-Plane datarefs needed by the FCU and EFIS displays.
    /// Organized by functional area, following the OVH pattern.
    /// </summary>
    private async Task SubscribeToDataRefsAsync(CancellationToken ct)
    {
        // ===== FCU LCD display values =====
        await SubscribeEnqueuedAsync(GetDataRefPath("SpdDashed"), (int v) => { _spdDashed = v; BuildAndSendFcuLcd(); });
        await SubscribeEnqueuedAsync(GetDataRefPath("HdgDashed"), (int v) => { _hdgDashed = v; BuildAndSendFcuLcd(); });
        await SubscribeEnqueuedAsync(GetDataRefPath("VsDashed"), (int v) => { _vsDashed = v; BuildAndSendFcuLcd(); });
        await SubscribeEnqueuedAsync(GetDataRefPath("AirspeedKtsMach"), (float v) => { _airspeedKtsMach = v; BuildAndSendFcuLcd(); });
        await SubscribeEnqueuedAsync(GetDataRefPath("SpdManaged"), (int v) => { _spdManaged = v; BuildAndSendFcuLcd(); });
        await SubscribeEnqueuedAsync(GetDataRefPath("IsMach"), (int v) => { _isMach = v; BuildAndSendFcuLcd(); });
        await SubscribeEnqueuedAsync(GetDataRefPath("HeadingMag"), (float v) => { _headingMag = v; BuildAndSendFcuLcd(); });
        await SubscribeEnqueuedAsync(GetDataRefPath("HdgManaged"), (int v) => { _hdgManaged = v; BuildAndSendFcuLcd(); });
        await SubscribeEnqueuedAsync(GetDataRefPath("HdgTrkMode"), (int v) => { _hdgTrkMode = v; BuildAndSendFcuLcd(); });
        await SubscribeEnqueuedAsync(GetDataRefPath("Altitude"), (float v) => { _altitude = v; BuildAndSendFcuLcd(); });
        await SubscribeEnqueuedAsync(GetDataRefPath("AltManaged"), (int v) => { _altManaged = v; BuildAndSendFcuLcd(); });
        await SubscribeEnqueuedAsync(GetDataRefPath("VerticalVelocity"), (float v) => { _verticalVelocity = v; BuildAndSendFcuLcd(); });
        await SubscribeEnqueuedAsync(GetDataRefPath("ApVerticalMode"), (int v) => { _apVerticalMode = v; BuildAndSendFcuLcd(); });

        // ===== EFIS-R baro display values =====
        await SubscribeEnqueuedAsync(GetDataRefPath("BaroCopilot"), (float v) => { _baroCopilot = v; MarkEfisRDisplayDirtyIfChanged(); });
        await SubscribeEnqueuedAsync(GetDataRefPath("BaroStdFO"), (int v) => { _baroStdFO = v; BuildAndSendEfisRLcd(); });
        await SubscribeEnqueuedAsync(GetDataRefPath("BaroUnitFO"), (int v) => { _baroUnitFO = v; BuildAndSendEfisRLcd(); });

        // ===== EFIS-L baro display values =====
        await SubscribeEnqueuedAsync(GetDataRefPath("BaroPilot"), (float v) => { _baroPilot = v; MarkEfisLDisplayDirtyIfChanged(); });
        await SubscribeEnqueuedAsync(GetDataRefPath("BaroStdCapt"), (int v) => { _baroStdCapt = v; BuildAndSendEfisLLcd(); });
        await SubscribeEnqueuedAsync(GetDataRefPath("BaroUnitCapt"), (int v) => { _baroUnitCapt = v; BuildAndSendEfisLLcd(); });

        // ===== FCU panel brightness (float 0..1 scaled to 0..255) =====
        await SubscribeEnqueuedAsync(GetDataRefPath("Brightness"), (float v) =>
        {
            _ledBrightness = (byte)Math.Clamp((int)(v * 255), 0, 255);
            _hidDevice?.SetLed(FcuLed.Backlight, _ledBrightness);
            _hidDevice?.SetLed(FcuLed.ExpedYellow, _ledBrightness);
            if (_hidDevice?.DeviceMask.HasFlag(DeviceMask.EfisR) == true)
                _hidDevice.SetLed(FcuLed.EfisRBacklight, _ledBrightness);
            if (_hidDevice?.DeviceMask.HasFlag(DeviceMask.EfisL) == true)
                _hidDevice.SetLed(FcuLed.EfisLBacklight, _ledBrightness);
        });
        await SubscribeEnqueuedAsync(GetDataRefPath("BrightnessLcd"), (float v) =>
        {
            byte lcd = (byte)Math.Clamp((int)(v * 235 + 20), 0, 255);
            _hidDevice?.SetLed(FcuLed.ScreenBacklight, lcd);
            if (_hidDevice?.DeviceMask.HasFlag(DeviceMask.EfisR) == true)
                _hidDevice.SetLed(FcuLed.EfisRScreenBacklight, lcd);
            if (_hidDevice?.DeviceMask.HasFlag(DeviceMask.EfisL) == true)
                _hidDevice.SetLed(FcuLed.EfisLScreenBacklight, lcd);
        });

        // ===== FCU status LEDs (on/off based on dataref value) =====
        await SubscribeLedAsync("Ap1Engage", FcuLed.Ap1Green);
        await SubscribeLedAsync("Ap2Engage", FcuLed.Ap2Green);
        await SubscribeLedAsync("ApprLed", FcuLed.ApprGreen);
        await SubscribeLedAsync("AthrLed", FcuLed.AthrGreen);
        await SubscribeLedAsync("LocLed", FcuLed.LocGreen);

        // ===== EFIS-R LEDs =====
        if (_hidDevice?.DeviceMask.HasFlag(DeviceMask.EfisR) == true)
        {
            await SubscribeLedAsync("R_ArptLed", FcuLed.EfisRArptGreen);
            await SubscribeLedAsync("R_NdbLed", FcuLed.EfisRNdbGreen);
            await SubscribeLedAsync("R_VorDLed", FcuLed.EfisRVordGreen);
            await SubscribeLedAsync("R_WptLed", FcuLed.EfisRWptGreen);
            await SubscribeLedAsync("R_CstrLed", FcuLed.EfisRCstrGreen);
            await SubscribeLedAsync("R_FdLed", FcuLed.EfisRFdGreen);
            await SubscribeLedAsync("R_LsLed", FcuLed.EfisRLsGreen);
        }

        // ===== EFIS-L LEDs =====
        if (_hidDevice?.DeviceMask.HasFlag(DeviceMask.EfisL) == true)
        {
            await SubscribeLedAsync("L_ArptLed", FcuLed.EfisLArptGreen);
            await SubscribeLedAsync("L_NdbLed", FcuLed.EfisLNdbGreen);
            await SubscribeLedAsync("L_VorDLed", FcuLed.EfisLVordGreen);
            await SubscribeLedAsync("L_WptLed", FcuLed.EfisLWptGreen);
            await SubscribeLedAsync("L_CstrLed", FcuLed.EfisLCstrGreen);
            await SubscribeLedAsync("L_FdLed", FcuLed.EfisLFdGreen);
            await SubscribeLedAsync("L_LsLed", FcuLed.EfisLLsGreen);
        }

        _logger.LogInformation("[FCU] Subscribed to all datarefs");
    }

    /// <summary>
    /// Subscribes to a dataref and updates an LED based on the value (on if > 0).
    /// Uses the current panel brightness for the on-state.
    /// </summary>
    private Task SubscribeLedAsync(string dataRefKey, FcuLed led)
    {
        return SubscribeEnqueuedAsync(GetDataRefPath(dataRefKey), (int v) =>
        {
            _hidDevice?.SetLed(led, v > 0 ? _ledBrightness : (byte)0);
        });
    }
}