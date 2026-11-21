# FTDX10 captures

Taken by [Eugene YO9VA](https://github.com/Eugen68) on his FTDX10 on 2026-11-20 with `tools/check_frames.py --seconds 5`, so each file is
the board's packets as they came, headers and all (firmware 2.0.1, SPI mode 2).
About 125 frames each.

Every capture: Main in USB, scope LEVEL +20 on 20 m and 40 m (except where
named), VFO B on 7.200.000.

| File | Set on the radio |
|---|---|
| ftdx10-14074-1k.bin | 14.074.000, span 1k, Center |
| ftdx10-14074-20k.bin | 14.074.000, span 20k, Center |
| ftdx10-14074-20k-mode2.bin | The same, a second take |
| ftdx10-14074-100k.bin | 14.074.000, span 100k, Center |
| ftdx10-14074-1m.bin | 14.074.000, span 1M, Center |
| ftdx10-14074-20k-cursor.bin | 14.074.000, span 20k, Cursor |
| ftdx10-14074-20k-fix.bin | 14.074.000, span 20k, Fix |
| ftdx10-7074-100k.bin | 7.074.000, span 100k, Center |
| ftdx10-7074-100k-fix.bin | 7.074.000, span 100k, Fix |
| ftdx10-14074-20k-3dss.bin | 14.074.000, span 20k, Center, 3DSS |
| ftdx10-14074-20k-level30.bin | 14.074.000, span 20k, Center, LEVEL +30 |
| ftdx10-ton-1k.bin | An AM carrier on 864 kHz, VFO on 863.000 USB: a 1 kHz tone |

The audio scope captures, taken on 2026-11-21 on 7.021.000, span 20k, Center,
each with one of the radio's audio scope settings changed from the first:

| File | Audio scope settings | Byte 3026 |
|---|---|---|
| ftdx10-audio-base.bin | AF-FFT att 0, oscilloscope att 0, time 1 ms (t0) | 0x00 |
| ftdx10-audio-fftatt1.bin | AF-FFT att 1 | 0x20 |
| ftdx10-audio-fftatt2.bin | AF-FFT att 2 | 0x40 |
| ftdx10-audio-oscatt1.bin | Oscilloscope att 1 | 0x08 |
| ftdx10-audio-time3.bin | Time 30 ms (t3) | 0x03 |

The layout they show is in [docs/protocol.md](../../docs/protocol.md#the-ftdx10).
