// Copyright (c) 2026 Klemens Urban <klemens.urban@outlook.com>
// Based on XSchenFly by memo5@gmx.at (https://github.com/schenlap/XSchenFly)
// SPDX-License-Identifier: GPL-3.0-only

using System.Buffers;
using System.Threading.Channels;
using HidSharp;
using Microsoft.Extensions.Logging;

namespace nocscienceat.WinWingFcu.Hardware;

/// <summary>
/// Known WinWing FCU device configurations (VID 0x4098).
/// </summary>
public static class WinWingDevices
{
    public static readonly (int Pid, string Name, DeviceMask Mask)[] KnownDevices =
    [
        (0xbb10, "FCU", DeviceMask.Fcu),
        (0xbc1e, "FCU + EFIS-R", DeviceMask.Fcu | DeviceMask.EfisR),
        (0xbc1d, "FCU + EFIS-L", DeviceMask.Fcu | DeviceMask.EfisL),
        (0xba01, "FCU + EFIS-L + EFIS-R", DeviceMask.Fcu | DeviceMask.EfisL | DeviceMask.EfisR),
    ];
}

/// <summary>
/// Manages the USB HID connection to a WinWing FCU device. Handles device discovery,
/// reading button states, writing LED commands, and LCD display updates.
/// Thread-safe: writes go through a channel-based single-consumer queue,
/// reads run on a dedicated task.
/// </summary>
public sealed class WinWingHidDevice : IDisposable
{
    private readonly ILogger _logger;
    private HidStream? _stream;
    private Task? _writeTask;

    /// <summary>
    /// Single-consumer write channel. All HID packets (LEDs, LCD data, LCD commits)
    /// are written here and drained sequentially by a single background task.
    /// </summary>
    private readonly Channel<byte[]> _hidWriteChannel = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions { SingleReader = true });

    /// <summary>
    /// Fired on the HidSharp reader thread when a complete HID report arrives.
    /// Subscribers must not block and must not retain the buffer reference beyond
    /// the callback — copy the data and return quickly.
    /// </summary>
    public event Action<byte[]>? ReportReceived;

    public DeviceMask DeviceMask { get; private set; }
    public bool IsOpen => _stream != null;

    public WinWingHidDevice(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Discovers and opens the first matching WinWing FCU device.
    /// If <paramref name="forcePid"/> is non-zero, only that PID is tried.
    /// </summary>
    public bool FindAndOpen(int vid, int forcePid)
    {
        if (forcePid != 0)
        {
            IEnumerable<HidDevice> devices = DeviceList.Local.GetHidDevices(vid, forcePid);
            HidDevice? device = devices.FirstOrDefault();
            if (device != null)
            {
                DeviceMask Mask = WinWingDevices.KnownDevices.FirstOrDefault(d => d.Pid == forcePid).Mask;
                DeviceMask = Mask != 0 ? Mask : DeviceMask.Fcu;
                _stream = device.Open();
                _logger.LogInformation("[FCU] Opened forced device PID 0x{Pid:X4}", forcePid);
                return true;
            }
            _logger.LogWarning("[FCU] Forced PID 0x{Pid:X4} not found", forcePid);
            return false;
        }

        foreach (var (pid, name, mask) in WinWingDevices.KnownDevices)
        {
            _logger.LogDebug("[FCU] Searching for WinWing {Name} ...", name);
            var devices = DeviceList.Local.GetHidDevices(vid, pid);
            var device = devices.FirstOrDefault();
            if (device != null)
            {
                DeviceMask = mask;
                _stream = device.Open();
                _logger.LogInformation("[FCU] Found WinWing {Name}", name);
                return true;
            }
        }

        _logger.LogWarning("[FCU] No compatible WinWing device found");
        return false;
    }

    private Task? _readTask;

    /// <summary>Starts the background write-channel consumer and async read loop.</summary>
    public void StartIo(CancellationToken ct)
    {
        _writeTask = Task.Run(() => DrainWriteChannelAsync(ct), ct);
        _readTask = Task.Run(() => ReadLoopAsync(ct), ct);
    }

    /// <summary>
    /// Async read loop that fires ReportReceived for each incoming HID report.
    /// Reads run WITHOUT the stream lock — on Windows, HidSharp uses separate
    /// kernel handles for read and write, so concurrent I/O is safe at the OS level.
    /// The stream lock only serializes writes.
    /// </summary>
    private async Task ReadLoopAsync(CancellationToken ct)
    {
        const int ReportSize = 64;

        while (!ct.IsCancellationRequested)
        {
            if (_stream == null) break;
            try
            {
                byte[] report = ArrayPool<byte>.Shared.Rent(ReportSize);
                try
                {
                    int totalBytes = await _stream.ReadAsync(report.AsMemory(0, ReportSize), ct);

                    if (totalBytes >= ReportSize)
                    {
                        ReportReceived?.Invoke(report);
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(report);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[FCU] HID read error");
                await Task.Delay(50, ct);
            }
        }
    }

    /// <summary>Sends an LED brightness command for a single LED.</summary>
    public void SetLed(FcuLed led, byte brightness)
    {
        int ledValue = (int)led;
        byte[] data;

        if (ledValue < 100) // FCU
        {
            data = [0x02, 0x10, 0xbb, 0, 0, 3, 0x49, (byte)ledValue, brightness, 0, 0, 0, 0, 0];
        }
        else if (ledValue < 200 && DeviceMask.HasFlag(DeviceMask.EfisR)) // EFIS-R
        {
            data = [0x02, 0x0e, 0xbf, 0, 0, 3, 0x49, (byte)(ledValue - 100), brightness, 0, 0, 0, 0, 0];
        }
        else if (ledValue >= 200 && ledValue < 300 && DeviceMask.HasFlag(DeviceMask.EfisL)) // EFIS-L
        {
            data = [0x02, 0x0d, 0xbf, 0, 0, 3, 0x49, (byte)(ledValue - 200), brightness, 0, 0, 0, 0, 0];
        }
        else
        {
            return;
        }

        _hidWriteChannel.Writer.TryWrite(data);
    }

    /// <summary>Sends LED brightness to multiple LEDs.</summary>
    public void SetLeds(FcuLed[] leds, byte brightness)
    {
        foreach (var led in leds)
            SetLed(led, brightness);
    }

    /// <summary>Sends the LCD init packet.</summary>
    public void InitLcd()
    {
        var data = new byte[64];
        data[0] = 0xf0;
        data[1] = 0x02;
        _hidWriteChannel.Writer.TryWrite(data);
    }

    /// <summary>
    /// Updates the main FCU LCD (speed, heading, altitude, vertical speed) with flag bits.
    /// </summary>
    public void SetFcuLcd(byte[] speed, byte[] heading, byte[] altitude, byte[] vs, byte[] flagBytes)
    {
        byte pkgNr = 1;

        // Data packet
        var data = new byte[64];
        data[0] = 0xf0; data[1] = 0x00; data[2] = pkgNr;
        data[3] = 0x31; data[4] = 0x10; data[5] = 0xbb;
        data[8] = 0x02; data[9] = 0x01;
        data[12] = 0xff; data[13] = 0xff;
        data[14] = 0x02;
        data[17] = 0x20;

        // Speed (3 direct segments)
        data[25] = speed[2];
        data[26] = (byte)(speed[1] | flagBytes[(int)FlagByteIndex.S1]);
        data[27] = speed[0];

        // Heading (3+1 swapped segments)
        data[28] = (byte)(heading[3] | flagBytes[(int)FlagByteIndex.H3]);
        data[29] = heading[2];
        data[30] = heading[1];
        data[31] = (byte)(heading[0] | flagBytes[(int)FlagByteIndex.H0]);

        // Altitude (5+1 swapped segments)
        data[32] = (byte)(altitude[5] | flagBytes[(int)FlagByteIndex.A5]);
        data[33] = (byte)(altitude[4] | flagBytes[(int)FlagByteIndex.A4]);
        data[34] = (byte)(altitude[3] | flagBytes[(int)FlagByteIndex.A3]);
        data[35] = (byte)(altitude[2] | flagBytes[(int)FlagByteIndex.A2]);
        data[36] = (byte)(altitude[1] | flagBytes[(int)FlagByteIndex.A1]);

        // V/S (4+1 swapped segments) — shares byte with altitude[0]
        data[37] = (byte)(altitude[0] | vs[4] | flagBytes[(int)FlagByteIndex.A0]);
        data[38] = (byte)(vs[3] | flagBytes[(int)FlagByteIndex.V3]);
        data[39] = (byte)(vs[2] | flagBytes[(int)FlagByteIndex.V2]);
        data[40] = (byte)(vs[1] | flagBytes[(int)FlagByteIndex.V1]);
        data[41] = (byte)(vs[0] | flagBytes[(int)FlagByteIndex.V0]);

        // Commit packet
        var commit = new byte[64];
        commit[0] = 0xf0; commit[1] = 0x00; commit[2] = pkgNr;
        commit[3] = 0x11; commit[4] = 0x10; commit[5] = 0xbb;
        commit[8] = 0x03; commit[9] = 0x01;
        commit[12] = 0xff; commit[13] = 0xff;
        commit[14] = 0x02;

        _hidWriteChannel.Writer.TryWrite(data);
        _hidWriteChannel.Writer.TryWrite(commit);
    }

    /// <summary>Updates the EFIS-R barometer LCD (vendor two-packet approach).</summary>
    public void SetEfisRLcd(byte[] baro, byte[] flagBytes)
    {
        if (!DeviceMask.HasFlag(DeviceMask.EfisR)) return;

        byte pkgNr = 1;

        // Data packet (command 0x1A)
        var data = new byte[64];
        data[0] = 0xf0; data[1] = 0x00; data[2] = pkgNr;
        data[3] = 0x1a; data[4] = 0x0e; data[5] = 0xbf;
        data[8] = 0x02; data[9] = 0x01;
        data[12] = 0xfb; data[13] = 0xa0;
        data[14] = 0x04;
        data[17] = 0x09;

        data[25] = baro[3];
        data[26] = (byte)(baro[2] | flagBytes[(int)FlagByteIndex.EfisRB2]);
        data[27] = baro[1];
        data[28] = baro[0];
        data[29] = flagBytes[(int)FlagByteIndex.EfisRB0];

        // Separate commit packet (command 0x11)
        var commit = new byte[64];
        commit[0] = 0xf0; commit[1] = 0x00; commit[2] = pkgNr;
        commit[3] = 0x11; commit[4] = 0x0e; commit[5] = 0xbf;
        commit[8] = 0x03; commit[9] = 0x01;
        commit[12] = 0xfb; commit[13] = 0xa0;
        commit[14] = 0x04;

        _hidWriteChannel.Writer.TryWrite(data);
        _hidWriteChannel.Writer.TryWrite(commit);
    }


    /// <summary>Updates the EFIS-L barometer LCD (vendor two-packet approach).</summary>
    public void SetEfisLLcd(byte[] baro, byte[] flagBytes)
    {
        if (!DeviceMask.HasFlag(DeviceMask.EfisL)) return;

        byte pkgNr = 1;

        // Data packet (command 0x1A)
        var data = new byte[64];
        data[0] = 0xf0; data[1] = 0x00; data[2] = pkgNr;
        data[3] = 0x1a; data[4] = 0x0d; data[5] = 0xbf;
        data[8] = 0x02; data[9] = 0x01;
        data[12] = 0xfb; data[13] = 0xa0;
        data[14] = 0x04;
        data[17] = 0x09;

        data[25] = baro[3];
        data[26] = (byte)(baro[2] | flagBytes[(int)FlagByteIndex.EfisLB2]);
        data[27] = baro[1];
        data[28] = baro[0];
        data[29] = flagBytes[(int)FlagByteIndex.EfisLB0];

        // Separate commit packet (command 0x11)
        var commit = new byte[64];
        commit[0] = 0xf0; commit[1] = 0x00; commit[2] = pkgNr;
        commit[3] = 0x11; commit[4] = 0x0d; commit[5] = 0xbf;
        commit[8] = 0x03; commit[9] = 0x01;
        commit[12] = 0xfb; commit[13] = 0xa0;
        commit[14] = 0x04;

        _hidWriteChannel.Writer.TryWrite(data);
        _hidWriteChannel.Writer.TryWrite(commit);
    }

    /// <summary>
    /// Single background task that drains the write channel and sends each packet
    /// to the HID stream sequentially.
    /// </summary>
    private async Task DrainWriteChannelAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var packet in _hidWriteChannel.Reader.ReadAllAsync(ct))
            {
                if (_stream == null) continue;
                try
                {
                    _stream.Write(packet);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[FCU] HID write error");
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    }

    public void Dispose()
    {
        _hidWriteChannel.Writer.TryComplete();
        try { _writeTask?.Wait(TimeSpan.FromSeconds(2)); } catch { }
        try { _readTask?.Wait(TimeSpan.FromSeconds(2)); } catch { }
        _stream?.Close();
        _stream?.Dispose();
        _stream = null;
    }
}

