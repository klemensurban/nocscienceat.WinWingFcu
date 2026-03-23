// Copyright (c) 2026 Klemens Urban <klemens.urban@outlook.com>
// Based on XSchenFly by memo5@gmx.at (https://github.com/schenlap/XSchenFly)
// SPDX-License-Identifier: GPL-3.0-only

namespace nocscienceat.WinWingFcu.Hardware;

/// <summary>
/// Encodes alphanumeric strings into 7-segment display byte arrays using the WinWing
/// FCU/EFIS hardware encoding schemes. Three encoding modes are supported:
/// direct (speed display), nibble-swapped (heading/alt/vs), and EFIS-specific (baro displays).
/// </summary>
public static class SevenSegmentEncoder
{
    //      A
    //      ---
    //   F | G | B
    //      ---
    //   E |   | C
    //      ---
    //       D
    // FCU segment wiring: A=0x80, B=0x40, C=0x20, D=0x10, E=0x02, F=0x08, G=0x04
    private static readonly Dictionary<char, byte> Representations = new()
    {
        ['0'] = 0xfa, ['1'] = 0x60, ['2'] = 0xd6, ['3'] = 0xf4,
        ['4'] = 0x6c, ['5'] = 0xbc, ['6'] = 0xbe, ['7'] = 0xe0,
        ['8'] = 0xfe, ['9'] = 0xfc,
        ['A'] = 0xee, ['B'] = 0xfe, ['C'] = 0x9a, ['D'] = 0x76,
        ['E'] = 0x9e, ['F'] = 0x8e, ['G'] = 0xbe, ['H'] = 0x6e,
        ['I'] = 0x60, ['J'] = 0x70, ['K'] = 0x0e, ['L'] = 0x1a,
        ['M'] = 0xa6, ['N'] = 0x26, ['O'] = 0xfa, ['P'] = 0xce,
        ['Q'] = 0xec, ['R'] = 0x06, ['S'] = 0xbc, ['T'] = 0x1e,
        ['U'] = 0x7a, ['V'] = 0x32, ['W'] = 0x58, ['X'] = 0x6e,
        ['Y'] = 0x7c, ['Z'] = 0xd6,
        ['-'] = 0x04, ['#'] = 0x36, ['/'] = 0x60, ['\\'] = 0xa0,
        [' '] = 0x00, ['.'] = 0x10,
    };

    // EFIS segment wiring: A=0x10, B=0x20, C=0x40, D=0x08, E=0x04, F=0x01, G=0x02
    private static readonly Dictionary<char, byte> EfisRepresentations = new()
    {
        ['0'] = 0x7d, ['1'] = 0x60, ['2'] = 0x3e, ['3'] = 0x7a,
        ['4'] = 0x63, ['5'] = 0x5b, ['6'] = 0x5f, ['7'] = 0x70,
        ['8'] = 0x7f, ['9'] = 0x7b,
        ['A'] = 0x77, ['B'] = 0x7f, ['C'] = 0x1d, ['D'] = 0x6e,
        ['E'] = 0x1f, ['F'] = 0x17, ['G'] = 0x5f, ['H'] = 0x67,
        ['I'] = 0x60, ['J'] = 0x68, ['K'] = 0x07, ['L'] = 0x0d,
        ['M'] = 0x56, ['N'] = 0x46, ['O'] = 0x7d, ['P'] = 0x37,
        ['Q'] = 0x73, ['R'] = 0x06, ['S'] = 0x5b, ['T'] = 0x0f,
        ['U'] = 0x6d, ['V'] = 0x4c, ['W'] = 0x29, ['X'] = 0x67,
        ['Y'] = 0x6b, ['Z'] = 0x3e,
        ['-'] = 0x02, ['#'] = 0x4e, ['/'] = 0x60, ['\\'] = 0x50,
        [' '] = 0x00, ['.'] = 0x08,
    };

    private static byte SwapNibbles(byte x) => (byte)(((x & 0x0F) << 4) | ((x & 0xF0) >> 4));

    /// <summary>
    /// Pads <paramref name="value"/> with leading zeros to length <paramref name="length"/>.
    /// </summary>
    public static string FixLength(string value, int length) => value.PadLeft(length, '0');

    /// <summary>
    /// Direct 7-segment encoding (used for Speed display).
    /// Returns byte array of length <paramref name="numSegments"/>.
    /// </summary>
    public static byte[] Encode(int numSegments, string text)
    {
        var d = new byte[numSegments];
        var upper = text.ToUpperInvariant();
        for (int i = 0; i < Math.Min(numSegments, upper.Length); i++)
        {
            if (Representations.TryGetValue(upper[i], out byte val))
                d[numSegments - 1 - i] = val;
        }
        return d;
    }

    /// <summary>
    /// Nibble-swapped 7-segment encoding (used for Heading, Altitude, V/S displays).
    /// Returns byte array of length <paramref name="numSegments"/> + 1 due to inter-digit bit sharing.
    /// </summary>
    public static byte[] EncodeSwapped(int numSegments, string text)
    {
        var d = new byte[numSegments + 1];
        var raw = Encode(numSegments, text);
        Array.Copy(raw, d, numSegments);

        // Swap nibbles on all bytes
        for (int i = 0; i < d.Length; i++)
            d[i] = SwapNibbles(d[i]);

        // Fix wired segment mapping: shift high nibbles between adjacent digits
        for (int i = 0; i < d.Length - 1; i++)
        {
            d[numSegments - i] = (byte)((d[numSegments - i] & 0x0f) | (d[numSegments - 1 - i] & 0xf0));
            d[numSegments - 1 - i] = (byte)(d[numSegments - 1 - i] & 0x0f);
        }

        return d;
    }

    /// <summary>
    /// EFIS-specific 7-segment encoding (used for barometer displays on EFIS-L and EFIS-R).
    /// Returns byte array of length <paramref name="numSegments"/> using the EFIS segment map.
    /// </summary>
    public static byte[] EncodeEfis(int numSegments, string text)
    {
        var d = new byte[numSegments];
        var upper = text.ToUpperInvariant();
        for (int i = 0; i < Math.Min(numSegments, upper.Length); i++)
        {
            if (EfisRepresentations.TryGetValue(upper[i], out byte val))
                d[numSegments - 1 - i] = val;
        }
        return d;
    }
}
