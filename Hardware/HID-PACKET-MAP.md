# WinWing FCU/EFIS — HID Packet Map

Documents which LEDs and LCD segments share the same HID output packet,
based on the protocol implemented in `WinWingHidDevice`.

All packets are **64 bytes**. The USB HID endpoint is shared across all
three logical devices (FCU, EFIS-R, EFIS-L) — they are distinguished by
a device-address byte in each packet header.

---

## Packet types

| Type | Sent via | Queuing | Drops stale? |
|------|----------|---------|--------------|
| LED command | `WriteLed` ? `_pendingLedCommands` | Ordered list | No — all sent in order |
| LCD init | `WriteLed` (one-shot) | Same LED list | No |
| FCU LCD | `StageFcuLcd` ? `_pendingFcuData/Commit` | Latest-value slot | Yes |
| EFIS-R LCD | `StageEfisRLcd` ? `_pendingEfisR/Commit` | Latest-value slot | Yes |
| EFIS-L LCD | `StageEfisLLcd` ? `_pendingEfisL/Commit` | Latest-value slot | Yes |

The I/O pump sends everything in a single wake cycle:
**LEDs ? FCU LCD ? EFIS-L LCD ? EFIS-R LCD**.

---

## LED packets (one per LED, 14 bytes padded to 64)

Each `SetLed` call produces **one** HID packet. LEDs are never batched
into a shared packet.

```
Byte:  0     1     2     3-4   5     6     7        8
       0x02  addr  dev   0x00  0x03  0x49  led_id   brightness
```

| Device | addr | dev  |
|--------|------|------|
| FCU    | 0x10 | 0xbb |
| EFIS-R | 0x0e | 0xbf |
| EFIS-L | 0x0d | 0xbf |

---

## FCU LCD packet (data + commit = 2 packets)

All FCU display regions — speed, heading, altitude, V/S — plus all
FCU flag bits are packed into a **single 64-byte data packet**, followed
by a **commit packet**.

### Data packet layout (bytes 25-41)

```
Byte  Content                          Flag byte OR'd in
----  -------------------------------  ------------------
 25   speed[2]                          —
 26   speed[1]                          S1  (MACH decimal)
 27   speed[0]                          —
 28   heading[3]                        H3  (SPD/MACH labels, spd_managed)
 29   heading[2]                        —
 30   heading[1]                        —
 31   heading[0]                        H0  (HDG/TRK/LAT labels, hdg_managed)
 32   altitude[5]                       A5  (HDG/VS, TRK/FPA mode labels)
 33   altitude[4]                       A4  (ALT label)
 34   altitude[3]                       A3  (lvl change left)
 35   altitude[2]                       A2  (lvl change)
 36   altitude[1]                       A1  (lvl change right)
 37   altitude[0] | vs[4]              A0  (vs_horz)
 38   vs[3]                             V3  (fpa_comma)
 39   vs[2]                             V2  (vs_vert direction)
 40   vs[1]                             V1  (alt_managed)
 41   vs[0]                             V0  (VS/FPA mode labels)
```

**Key observations:**
- Byte 37 is shared between `altitude[0]`, `vs[4]`, and flag `A0`.
- All 17 FCU flag bytes (H0, H3, S1, A0–A5, V0–V3) are OR'd into this
  single data packet.
- Speed, heading, altitude, and V/S cannot be updated independently —
  they are always sent together.

### Commit packet

Same header with command byte `0x11` instead of `0x31`. Contains no
display data — signals the device to latch the preceding data packet.

---

## EFIS-R LCD packet (data + commit = 2 packets)

The EFIS-R barometer display uses its own data + commit pair,
independent of the FCU LCD.

### Data packet layout (bytes 25-29)

```
Byte  Content                          Flag byte OR'd in
----  -------------------------------  ------------------
 25   baro[3]                           —
 26   baro[2]                           EfisRB2  (hPa decimal)
 27   baro[1]                           —
 28   baro[0]                           —
 29   (flags only)                      EfisRB0  (QFE/QNH indicator)
```

Header: addr=`0x0e`, dev=`0xbf`, command=`0x1a`.

---

## EFIS-L LCD packet (data + commit = 2 packets)

Identical structure to EFIS-R, different device address.

### Data packet layout (bytes 25-29)

```
Byte  Content                          Flag byte OR'd in
----  -------------------------------  ------------------
 25   baro[3]                           —
 26   baro[2]                           EfisLB2  (hPa decimal)
 27   baro[1]                           —
 28   baro[0]                           —
 29   (flags only)                      EfisLB0  (QFE/QNH indicator)
```

Header: addr=`0x0d`, dev=`0xbf`, command=`0x1a`.

---

## Summary: what shares a packet

| Packet | Contents |
|--------|----------|
| **1 LED packet** | Exactly one LED brightness value |
| **FCU LCD data** | Speed + Heading + Altitude + V/S segments + all FCU flags (S1, H0, H3, A0–A5, V0–V3) |
| **FCU LCD commit** | Latch command only |
| **EFIS-R LCD data** | Baro 4 digits + EfisRB0 + EfisRB2 flags |
| **EFIS-R LCD commit** | Latch command only |
| **EFIS-L LCD data** | Baro 4 digits + EfisLB0 + EfisLB2 flags |
| **EFIS-L LCD commit** | Latch command only |

**Implication:** Changing any single FCU display value (e.g. speed only)
still requires rebuilding and sending the full FCU LCD data packet with
all four display regions and all flag bytes. The EFIS displays are
independent of each other and of the FCU.

---

## HID input (read)

A single 64-byte report carries button bitmasks for all connected devices:

```
Byte   Content
----  ---------------------------------------
 0     Report ID (0x01)
 1-4   FCU buttons (32-bit LE)
 5-8   EFIS-L buttons (32-bit LE, if present)
 9-12  EFIS-R buttons (32-bit LE, if present)
```

All button states arrive in one report — no per-device split on input.
