// Copyright (c) 2026 Klemens Urban <klemens.urban@outlook.com>
// Based on XSchenFly by memo5@gmx.at (https://github.com/schenlap/XSchenFly)
// SPDX-License-Identifier: GPL-3.0-only

using nocscienceat.WinWingFcu.Hardware;

namespace nocscienceat.WinWingFcu.Panels;

public partial class FcuPanelHandler
{
    /// <summary>
    /// Rebuilds and sends the full FCU LCD packet (speed + heading + altitude + V/S + all flags).
    /// Called directly from each FCU dataref callback.
    /// </summary>
    private void BuildAndSendFcuLcd()
    {
        float speed = _airspeedKtsMach;
        float heading = _headingMag;
        float alt = _altitude;
        float vs = _verticalVelocity;
        int hdgTrk = _hdgTrkMode;

        // Mach mode: convert 0.xx to display value
        if (_isMach != 0 && speed < 1)
            speed = (speed + 0.005f) * 100;

        // V/S sign handling
        if (vs < 0)
        {
            vs = Math.Abs(vs);
            _flags["vs_vert"].Value = false;
        }
        else
        {
            _flags["vs_vert"].Value = true;
        }

        _flags["fpa_comma"].Value = false;

        // Build display strings
        string spdStr, hdgStr, altStr, vsStr;

        if (_spdDashed != 0)
            spdStr = "---";
        else
            spdStr = SevenSegmentEncoder.FixLength(((int)speed).ToString(), 3);

        if (_hdgDashed != 0)
            hdgStr = "---";
        else
            hdgStr = SevenSegmentEncoder.FixLength(((int)heading).ToString(), 3);

        altStr = SevenSegmentEncoder.FixLength(((int)alt).ToString(), 5);

        if (_vsDashed != 0)
        {
            vsStr = "----";
            _flags["vs_vert"].Value = false;
        }
        else if (hdgTrk == 0) // V/S mode
        {
            // Small 0 for hundred-feet chars in v/s mode
            string vsHundreds = SevenSegmentEncoder.FixLength(((int)(vs / 100)).ToString(), 2);
            vsStr = vsHundreds.PadRight(4, '#');
        }
        else // FPA mode
        {
            string vsHundreds = SevenSegmentEncoder.FixLength(((int)(vs / 100)).ToString(), 2);
            vsStr = vsHundreds.PadRight(4, ' ');
            _flags["fpa_comma"].Value = true;
        }

        // Update mode flags
        _flags["spd_managed"].Value = _spdManaged != 0;
        _flags["hdg_managed"].Value = _hdgManaged != 0;
        _flags["alt_managed"].Value = _altManaged != 0;
        _flags["spd"].Value = _isMach == 0;
        _flags["mach"].Value = _isMach != 0;
        _flags["mach_comma"].Value = _isMach != 0;
        _flags["hdg"].Value = hdgTrk == 0;
        _flags["trk"].Value = hdgTrk != 0;
        _flags["fvs"].Value = hdgTrk == 0;
        _flags["vshdg"].Value = hdgTrk == 0;
        _flags["vs"].Value = hdgTrk == 0;
        _flags["ftrk"].Value = hdgTrk != 0;
        _flags["ffpa"].Value = hdgTrk != 0;
        _flags["ffpa2"].Value = hdgTrk != 0;

        // EXPED LED
        bool expedDesired = _apVerticalMode >= 112;
        if (expedDesired != _expedLedState)
        {
            _expedLedState = expedDesired;
            _hidDevice!.SetLed(FcuLed.ExpedGreen, (byte)(_ledBrightness * (expedDesired ? 1 : 0)));
        }

        // Build flag bytes
        var flagBytes = BuildFlagBytes();

        // Encode segments
        var s = SevenSegmentEncoder.Encode(3, spdStr);
        var h = SevenSegmentEncoder.EncodeSwapped(3, hdgStr);
        var a = SevenSegmentEncoder.EncodeSwapped(5, altStr);
        var v = SevenSegmentEncoder.EncodeSwapped(4, vsStr);

        _hidDevice!.SetFcuLcd(s, h, a, v, flagBytes);
    }

    private void BuildAndSendEfisRLcd()
    {
        UpdateEfisLcd(_baroStdFO != 0, _baroUnitFO, _baroCopilot, "efisr_qnh", "efisr_hpa_dec", _hidDevice!.SetEfisRLcd);
    }

    private void BuildAndSendEfisLLcd()
    {
        UpdateEfisLcd(_baroStdCapt != 0, _baroUnitCapt, _baroPilot, "efisl_qnh", "efisl_hpa_dec", _hidDevice!.SetEfisLLcd);
    }

    private void UpdateEfisLcd(bool std, int unit, float baroSource, string qnhFlagKey, string hpaDecFlagKey, Action<byte[], byte[]> setLcd)
    {
        _flags[qnhFlagKey].Value = !std;
        _flags[hpaDecFlagKey].Value = unit == 0 && !std;

        int baroDisplay = GetEfisBaroDisplay(std, unit, baroSource);
        var flagBytes = BuildFlagBytes();
        string baroStr = FormatEfisBaroDisplay(baroDisplay, unit);
        var baroBytes = SevenSegmentEncoder.EncodeEfis(4, baroStr);
        setLcd(baroBytes, flagBytes);
    }

    private static int GetEfisBaroDisplay(bool std, int unit, float baro)
    {
        if (std)
            return -1;

        if (baro < 100)
            baro = (baro + 0.005f) * 100;

        return unit != 0
            ? (int)((baro * 33.86388f + 50) / 100)
            : (int)baro;
    }

    private static string FormatEfisBaroDisplay(int baroDisplay, int unit) =>
        baroDisplay < 0
            ? "Std "
            : unit != 0
                // Real EFIS hPa presentation uses a blank leading digit, e.g. 998 instead of 0998.
                ? baroDisplay.ToString().PadLeft(4, ' ')
                : SevenSegmentEncoder.FixLength(baroDisplay.ToString(), 4);

    private void MarkEfisRDisplayDirtyIfChanged()
    {
        string baroStr = FormatEfisBaroDisplay(GetEfisBaroDisplay(_baroStdFO != 0, _baroUnitFO, _baroCopilot), _baroUnitFO);
        if (baroStr != _lastEfisRBaroStr)
        {
            _lastEfisRBaroStr = baroStr;
            BuildAndSendEfisRLcd();
        }
    }

    private void MarkEfisLDisplayDirtyIfChanged()
    {
        string baroStr = FormatEfisBaroDisplay(GetEfisBaroDisplay(_baroStdCapt != 0, _baroUnitCapt, _baroPilot), _baroUnitCapt);
        if (baroStr != _lastEfisLBaroStr)
        {
            _lastEfisLBaroStr = baroStr;
            BuildAndSendEfisLLcd();
        }
    }

    /// <summary>
    /// Builds the flag byte array from all flag definitions, used to OR into LCD data packets.
    /// </summary>
    private byte[] BuildFlagBytes()
    {
        var flagByteCount = Enum.GetValues<FlagByteIndex>().Length;
        var bl = new byte[flagByteCount];
        foreach (var flag in _flags.Values)
        {
            if (flag.Value)
                bl[(int)flag.ByteIndex] |= flag.Mask;
        }
        return bl;
    }
}
