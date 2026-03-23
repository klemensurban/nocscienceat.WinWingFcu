// Copyright (c) 2026 Klemens Urban <klemens.urban@outlook.com>
// Based on XSchenFly by memo5@gmx.at (https://github.com/schenlap/XSchenFly)
// SPDX-License-Identifier: GPL-3.0-only

namespace nocscienceat.WinWingFcu.Hardware;

/// <summary>
/// Represents a single flag bit in the LCD data packet. Flags control label visibility
/// (SPD/MACH, HDG/TRK, managed dots, etc.) on the 7-segment displays.
/// </summary>
public class LcdFlag
{
    public FlagByteIndex ByteIndex { get; }
    public byte Mask { get; }
    public bool Value { get; set; }

    public LcdFlag(FlagByteIndex byteIndex, byte mask, bool defaultValue = false)
    {
        ByteIndex = byteIndex;
        Mask = mask;
        Value = defaultValue;
    }
}

/// <summary>
/// All LCD flags used by the FCU and EFIS displays
/// </summary>
public static class LcdFlags
{
    public static Dictionary<string, LcdFlag> CreateDefaults() => new()
    {
        ["spd"] = new(FlagByteIndex.H3, 0x08),
        ["mach"] = new(FlagByteIndex.H3, 0x04),
        ["hdg"] = new(FlagByteIndex.H0, 0x80),
        ["trk"] = new(FlagByteIndex.H0, 0x40),
        ["lat"] = new(FlagByteIndex.H0, 0x20, true),
        ["vshdg"] = new(FlagByteIndex.A5, 0x08),
        ["vs"] = new(FlagByteIndex.A5, 0x04),
        ["ftrk"] = new(FlagByteIndex.A5, 0x02),
        ["ffpa"] = new(FlagByteIndex.A5, 0x01),
        ["alt"] = new(FlagByteIndex.A4, 0x10, true),
        ["hdg_managed"] = new(FlagByteIndex.H0, 0x10),
        ["spd_managed"] = new(FlagByteIndex.H3, 0x02),
        ["alt_managed"] = new(FlagByteIndex.V1, 0x10),
        ["vs_horz"] = new(FlagByteIndex.A0, 0x10, true),
        ["vs_vert"] = new(FlagByteIndex.V2, 0x10),
        ["lvl"] = new(FlagByteIndex.A2, 0x10, true),
        ["lvl_left"] = new(FlagByteIndex.A3, 0x10, true),
        ["lvl_right"] = new(FlagByteIndex.A1, 0x10, true),
        ["fvs"] = new(FlagByteIndex.V0, 0x40),
        ["ffpa2"] = new(FlagByteIndex.V0, 0x80),
        ["fpa_comma"] = new(FlagByteIndex.V3, 0x10),
        ["mach_comma"] = new(FlagByteIndex.S1, 0x01),
        ["efisr_qnh"] = new(FlagByteIndex.EfisRB0, 0x02),
        ["efisr_hpa_dec"] = new(FlagByteIndex.EfisRB2, 0x80),
        ["efisl_qnh"] = new(FlagByteIndex.EfisLB0, 0x02),
        ["efisl_hpa_dec"] = new(FlagByteIndex.EfisLB2, 0x80),
    };
}
