// Copyright (c) 2026 Klemens Urban <klemens.urban@outlook.com>
// Based on XSchenFly by memo5@gmx.at (https://github.com/schenlap/XSchenFly)
// SPDX-License-Identifier: GPL-3.0-only

namespace nocscienceat.WinWingFcu.Hardware;

/// <summary>Bitmask identifying which WinWing devices are connected.</summary>
[Flags]
public enum DeviceMask
{
    /// <summary>No supported WinWing device detected.</summary>
    None = 0,

    /// <summary>Main FCU unit.</summary>
    Fcu = 1,

    /// <summary>Right EFIS unit.</summary>
    EfisR = 2,

    /// <summary>Left EFIS unit.</summary>
    EfisL = 4
}

/// <summary>
/// LED identifiers on the WinWing FCU/EFIS hardware.
/// Values 0-99 = FCU, 100-199 = EFIS-R, 200-299 = EFIS-L.
/// The offset is subtracted before sending the HID command.
/// </summary>
public enum FcuLed
{
    /// <summary>FCU panel backlight.</summary>
    Backlight = 0,

    /// <summary>FCU LCD backlight.</summary>
    ScreenBacklight = 1,

    /// <summary>FCU LOC annunciator.</summary>
    LocGreen = 3,

    /// <summary>FCU AP1 annunciator.</summary>
    Ap1Green = 5,

    /// <summary>FCU AP2 annunciator.</summary>
    Ap2Green = 7,

    /// <summary>FCU A/THR annunciator.</summary>
    AthrGreen = 9,

    /// <summary>FCU EXPED annunciator, green channel.</summary>
    ExpedGreen = 11,

    /// <summary>FCU APPR annunciator.</summary>
    ApprGreen = 13,

    /// <summary>FCU FLAG annunciator.</summary>
    FlagGreen = 17,

    /// <summary>FCU EXPED annunciator, yellow channel.</summary>
    ExpedYellow = 30,

    /// <summary>Right EFIS panel backlight.</summary>
    EfisRBacklight = 100,

    /// <summary>Right EFIS LCD backlight.</summary>
    EfisRScreenBacklight = 101,

    /// <summary>Right EFIS FLAG annunciator.</summary>
    EfisRFlagGreen = 102,

    /// <summary>Right EFIS FD annunciator.</summary>
    EfisRFdGreen = 103,

    /// <summary>Right EFIS LS annunciator.</summary>
    EfisRLsGreen = 104,

    /// <summary>Right EFIS CSTR annunciator.</summary>
    EfisRCstrGreen = 105,

    /// <summary>Right EFIS WPT annunciator.</summary>
    EfisRWptGreen = 106,

    /// <summary>Right EFIS VORD annunciator.</summary>
    EfisRVordGreen = 107,

    /// <summary>Right EFIS NDB annunciator.</summary>
    EfisRNdbGreen = 108,

    /// <summary>Right EFIS ARPT annunciator.</summary>
    EfisRArptGreen = 109,

    /// <summary>Left EFIS panel backlight.</summary>
    EfisLBacklight = 200,

    /// <summary>Left EFIS LCD backlight.</summary>
    EfisLScreenBacklight = 201,

    /// <summary>Left EFIS FLAG annunciator.</summary>
    EfisLFlagGreen = 202,

    /// <summary>Left EFIS FD annunciator.</summary>
    EfisLFdGreen = 203,

    /// <summary>Left EFIS LS annunciator.</summary>
    EfisLLsGreen = 204,

    /// <summary>Left EFIS CSTR annunciator.</summary>
    EfisLCstrGreen = 205,

    /// <summary>Left EFIS WPT annunciator.</summary>
    EfisLWptGreen = 206,

    /// <summary>Left EFIS VORD annunciator.</summary>
    EfisLVordGreen = 207,

    /// <summary>Left EFIS NDB annunciator.</summary>
    EfisLNdbGreen = 208,

    /// <summary>Left EFIS ARPT annunciator.</summary>
    EfisLArptGreen = 209
}

/// <summary>
/// Indexes into the LCD flag-byte buffer consumed by <see cref="WinWingHidDevice"/>.
/// Each entry identifies the payload byte that carries overlay bits for one FCU or EFIS display region.
/// </summary>
public enum FlagByteIndex
{
    /// <summary>Heading display low-byte flags, including HDG/TRK/LAT labels.</summary>
    H0 = 0,

    /// <summary>Heading display high-byte flags, including SPD/MACH labels.</summary>
    H3 = 1,

    /// <summary>Shared altitude/VS transition byte written alongside the first VS digit.</summary>
    A0 = 2,

    /// <summary>Altitude display byte for the least-significant altitude digit.</summary>
    A1 = 3,

    /// <summary>Altitude display byte for the next altitude digit.</summary>
    A2 = 4,

    /// <summary>Altitude display byte for the next altitude digit.</summary>
    A3 = 5,

    /// <summary>Altitude display byte carrying the ALT label bit.</summary>
    A4 = 6,

    /// <summary>Altitude display high-byte flags, including HDG/VS and TRK/FPA mode labels.</summary>
    A5 = 7,

    /// <summary>Vertical-speed display byte used for VS direction overlay bits.</summary>
    V2 = 8,

    /// <summary>Vertical-speed display byte used for decimal/comma overlay bits.</summary>
    V3 = 9,

    /// <summary>Vertical-speed display low-byte flags, including VS/FPA mode labels.</summary>
    V0 = 10,

    /// <summary>Vertical-speed display byte carrying the managed ALT indicator.</summary>
    V1 = 11,

    /// <summary>Speed display byte carrying the MACH decimal point.</summary>
    S1 = 12,

    /// <summary>Right EFIS barometer flags for the QFE/QNH indicators.</summary>
    EfisRB0 = 13,

    /// <summary>Right EFIS barometer flags for the HPA decimal indicator.</summary>
    EfisRB2 = 14,

    /// <summary>Left EFIS barometer flags for the QFE/QNH indicators.</summary>
    EfisLB0 = 15,

    /// <summary>Left EFIS barometer flags for the HPA decimal indicator.</summary>
    EfisLB2 = 16
}
