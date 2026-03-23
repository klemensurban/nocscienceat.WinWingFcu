# WinWing FCU -- Channel-Based Write Architecture

## Overview

The HID write path uses a channel-based single-consumer model, similar to
`JavaSimulatorPanelHandlerBase.DrainSerialWriteQueueAsync`.

The key difference from the OVH serial panel: the WinWing HID protocol
packs multiple display regions into one packet. Each dataref callback
rebuilds the affected packet(s) and writes them directly to the channel.

---

## Architecture

### Single HID write channel

```csharp
private readonly Channel<byte[]> _hidWriteChannel =
    Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions { SingleReader = true });
```

One background task (`DrainWriteChannelAsync`) drains the channel and
writes each packet to `_stream` sequentially -- same pattern as
`DrainSerialWriteQueueAsync` in the OVH base class.

### No staging slots

Packets are written to the channel as complete 64-byte arrays via
`WritePackets()`, ready to send. No intermediate `_pendingFcuData` /
`_pendingFcuCommit` / etc.

### No MarkDisplayDirty / UpdateAllDisplays

Instead of a global dirty flag that rebuilds everything, each dataref
callback directly produces the minimal set of HID packets affected:

- FCU datarefs -> `BuildAndSendFcuLcd()`
- EFIS-R datarefs -> `BuildAndSendEfisRLcd()`
- EFIS-L datarefs -> `BuildAndSendEfisLLcd()`

### Packet-level aggregation

Because the FCU LCD packet contains speed + heading + altitude + V/S +
all flag bytes in one 64-byte payload, the aggregation happens at the
packet level:

```
dataref callback (e.g. heading changed)
  -> update cached heading value
  -> call BuildAndSendFcuLcd()
    -> reads ALL current cached values (speed, hdg, alt, vs, flags)
    -> builds the single FCU LCD data packet + commit packet
    -> writes both to the channel
```

This is conceptually the same as the OVH `SendToHardware("K_U1", "1")`
pattern -- but the "message" is a pre-built 64-byte HID packet instead
of a serial string.

### EFIS packets are independent

EFIS-R and EFIS-L each have their own packet structure. A baro value
change only needs to build and enqueue the affected EFIS packet pair.
Baro float values use change-detection (`MarkEfisRDisplayDirtyIfChanged`,
`MarkEfisLDisplayDirtyIfChanged`) to avoid redundant sends.

### LED packets stay the same

LED commands are already independent 14-byte packets. They go directly
into the channel -- no aggregation needed.

---

## Data Flow

```
+-----------------------------------+
|  X-Plane dataref callbacks        |
|  (via SubscribeEnqueuedAsync)     |
+-----------------------------------+
             | update cached state
             v
+-----------------------------------+
|  Build affected packet(s)         |
|  +---------------------------+    |
|  | FCU LCD: always full      |    |  <- speed/hdg/alt/vs/flags all in one packet
|  | EFIS-R:  baro + flags     |    |  <- independent packet
|  | EFIS-L:  baro + flags     |    |  <- independent packet
|  | LED:     single command   |    |  <- independent packet
|  +---------------------------+    |
+-----------------------------------+
             | WritePackets(data, commit)
             v
+-----------------------------------+
|  DrainWriteChannelAsync           |
|  await foreach (packet in         |
|    channel.Reader.ReadAllAsync)   |
|  {                                |
|      _stream.Write(packet);       |
|  }                                |
+-----------------------------------+
```

---

## Packet Grouping Rules

| Trigger | Packets produced |
|---------|-----------------|
| Speed/Heading/Alt/VS/any FCU flag change | FCU LCD data + FCU LCD commit (2 packets) |
| EFIS-R baro or flag change | EFIS-R data + EFIS-R commit (2 packets) |
| EFIS-L baro or flag change | EFIS-L data + EFIS-L commit (2 packets) |
| Single LED change | 1 LED packet |
| Brightness change (backlights) | N LED packets (one per affected LED) |

---

## Stale Frame Handling

With a channel, every packet is sent (no stale-frame dropping).

### Measure before optimizing

Before adding change-detection or coalescing, instrument the suspected
hot paths with counters and/or timers to confirm they are actually
high-frequency. Candidates to measure:

| Path | What to log | Why |
|------|-------------|-----|
| `BuildAndSendFcuLcd()` calls/sec | Counter per second | Heading and altitude float datarefs may fire at sim frame rate |
| `BuildAndSendEfisRLcd()` / `EfisL` calls/sec | Counter per second | Already mitigated by baro string caching -- verify it works |
| Channel depth (`_hidWriteChannel.Reader.Count`) | Periodic sample | Detects backpressure / write stalls |
| `_stream.Write` duration | Stopwatch per call | Confirms ~1ms assumption |

**Options to handle high-frequency updates (if needed):**

1. **Accept it.** The HID write is fast (~1ms per 64-byte packet). Even
   at 30 dataref updates/sec, the channel stays shallow.

2. **Change-detection at the source.** Same pattern as the existing
   EFIS baro caching: only enqueue if the output differs from the last
   sent value.

3. **Coalesce in the channel reader.** Skip packets if a newer one of
   the same type is already queued.

4. **Rate-limit with a timer.** Throttle how often packets are built.

---

## Comparison with OVH Serial Pattern

| Aspect | OVH (Serial) | FCU (HID) |
|--------|-------------|-----------|
| Channel payload | `string` (`"K_U1,1;"`) | `byte[64]` (raw HID packet) |
| Aggregation | None needed - each command is independent | FCU LCD must aggregate 4 display regions + flags into one packet |
| Writer | `_serialPort.WriteLine(message)` | `_stream.Write(packet)` |
| Ordering | Strict FIFO | Strict FIFO |
| Drop stale | No | No (option to add coalescing later) |
| Change detection | `HasValueChanged` / `_ledStateCache` | Baro string cache; could extend to FCU display strings |

---

## Open Questions

- **Should the commit packet be a separate channel entry or bundled
  with the data packet?** Currently sent as two entries via
  `WritePackets(data, commit)`, preserving write order. Bundling
  (e.g. `Channel<byte[][]>`) would guarantee atomicity but adds
  allocation.

- **Is there value in a typed channel** (e.g. `Channel<HidWriteCommand>`
  with an enum tag for FCU/EFIS-R/EFIS-L/LED) to enable optional
  coalescing by type later?
