# Oscilloscope traces

Every trace from the bench goes here, so anyone checking the work can see what each
ACC line does on an FTdx101MP. They're taken on my Siglent SDS1104X-E with the
probe at 10X, 1 V/div, DC coupling, and the ground on ACC pin 2, and read off the
scope over its LAN port with [tools/scope_shot.py](../tools/scope_shot.py). The
pictures are in [docs/images/scope/](images/scope/), the samples behind them
in [captures/scope/](../captures/scope/), and the measured numbers in the Levels table
in [docs/bench-notes.md](bench-notes.md). In every picture the bottom
graticule line is 0 V and each division up is 1 V, except in
[Two and three probes at once](#two-and-three-probes-at-once).

**Pin numbers are ACC socket pins.** The probe goes on the breakout board's
terminal for that pin, shown in brackets, at the free end of the pin's 470 ohm
resistor. See the table in [Wiring](hardware.md#wiring).

**File names.** The script names each picture from the ACC pin on each channel,
a note of what it shows, the timebase and the time it was taken, for example
`acc-pin12-cnt-tx_burst_100us_2026-11-10_1107.png`. Pictures are never
overwritten. Before saving, the script prints each channel's ACC pin and
terminal, to check against where the probe is.

Taken on 2026-11-10 with one probe, the ESP32 module out of the board, and the
long ground lead unless it says otherwise.

## Stage 1, no link: what the radio sends on its own

**ACC pin 12 (terminal 38), CNT TX, the heartbeat.** The line idles high at
3.36 V and drops for a short burst once a second.

![ACC pin 12 at 200 ms/div: a steady line at 3.36 V with a thin dip once a second.](images/scope/acc-pin12-cnt-tx_once-a-second_200ms_2026-11-10_1107.png)

One whole burst, about 500 us long:

![ACC pin 12 at 100 us/div: one burst of serial data about 5 divisions long.](images/scope/acc-pin12-cnt-tx_burst_100us_2026-11-10_1107.png)

The first byte, bit by bit: start bit, then `a` (0x61) least significant bit
first, 1 0 0 0 0 1 1 0, then the stop bit. Each bit is 8.5 us, about 117,660
baud. The next byte, `l`, starts at the right-hand edge. The whole burst decodes
as `alive` and a zero byte.

![ACC pin 12 at 10 us/div: the start bit, the eight bits of 0x61 and the stop bit, then the start of the next byte.](images/scope/acc-pin12-cnt-tx_first-byte_10us_2026-11-10_1108.png)

**The other lines, idle.** With no reply to the heartbeat, every other line sits
high and still, between 3.31 and 3.38 V.

| ACC pin (terminal) | Signal | Idle level | Picture |
|---|---|---|---|
| 11 (39) | CNT RX | 3.37 V | [acc-pin11-cnt-rx_idle](images/scope/acc-pin11-cnt-rx_idle_200ms_2026-11-10_1114.png) |
| 6 (13) | CS | 3.31 V | [acc-pin6-cs_idle](images/scope/acc-pin6-cs_idle_200ms_2026-11-10_1110.png) |
| 5 (10) | CLOCK | 3.32 V | [acc-pin5-clk_idle](images/scope/acc-pin5-clk_idle_200ms_2026-11-10_1111.png) |
| 4 (11) | DATA2 | 3.31 V | [acc-pin4-data2_idle](images/scope/acc-pin4-data2_idle_200ms_2026-11-10_1112.png) |
| 3 (14) | DATA1 | 3.34 V | [acc-pin3-data1_idle](images/scope/acc-pin3-data1_idle_200ms_2026-11-10_1109.png) |

## Stage 1, link on: the radio streams

ACC pins 11 and 12 linked (terminals 39 and 38), so the radio's heartbeat comes
back to it.

**ACC pin 6 (terminal 13), CS.** Low 25 times a second: 25.002 Hz, low for
36.54 ms and high for 3.45 ms. Each low is one 4096-byte frame.

![ACC pin 6 at 10 ms/div: low for most of each 40 ms cycle, with a short high between frames.](images/scope/acc-pin6-cs_link-25hz_10ms_2026-11-10_1205.png)

**ACC pin 5 (terminal 10), CLOCK.** 1.97 MHz, in bursts of 8 clocks, one burst a
byte, with a gap between bytes. A byte every 8.9 us, so 4096 bytes take 36.5 ms,
which matches CS. The edges ring, to 3.72 V and -0.40 V; see the note under
DATA1.

![ACC pin 5 at 200 ns/div: the 2 MHz clock, with ringing on each edge.](images/scope/acc-pin5-clk_link-clock_200ns_2026-11-10_1207.png)

![ACC pin 5 at 10 us/div: bursts of 8 clocks with a gap between each.](images/scope/acc-pin5-clk_link-clock-bursts_10us_2026-11-10_1207.png)

**ACC pin 4 (terminal 11), DATA2.** This is the data. In each 40 ms cycle it
switches for about 27.8 ms, then stays low for about 12.2 ms. CS is high for
3.45 ms of that, so the last 8.7 ms or so of each frame, about 980 bytes, is
zeros.

![ACC pin 4 at 10 us/div: data bytes switching between 0 and 3.3 V.](images/scope/acc-pin4-data2_link-data_10us_2026-11-10_1212.png)

![ACC pin 4 at 10 ms/div: about 28 ms of data, then about 12 ms low, in each frame.](images/scope/acc-pin4-data2_link-frames_10ms_2026-11-10_1212.png)

**ACC pin 3 (terminal 14), DATA1.** No data, with the Sub receiver on or off. It
stays high, with about 0.3 V of ripple that follows the frames: crosstalk from
the clock and DATA2 wires beside it. These two were taken with the probe's short
ground spring instead of the long lead, and the ripple still peaks at about
3.9 V, so some of the ringing on the clock and data is coupling between the
cable's wires, not only the probe's ground lead.

![ACC pin 3 at 10 ms/div, Sub receiver on: high, with frame-shaped ripple.](images/scope/acc-pin3-data1_link-sub-on-spring_10ms_2026-11-10_1215.png)

![ACC pin 3 at 10 ms/div, Sub receiver off: the same.](images/scope/acc-pin3-data1_link-sub-off-spring_10ms_2026-11-10_1218.png)

## Stage 1, link off: the data stops

Link taken off. CS stays high for the 14 seconds on screen, and DATA1's ripple has
gone, so the radio sends no frames. The heartbeat reply is what turns the data on.

![ACC pin 6 at 1 s/div with the link off: high and still.](images/scope/acc-pin6-cs_link-off_1s_2026-11-10_1224.png)

![ACC pin 3 at 10 ms/div with the link off: high, no ripple.](images/scope/acc-pin3-data1_link-off_10ms_2026-11-10_1223.png)

## Stage 2, ESP32 in: the radio still streams

The erased ESP32 in the board, powered from the PC through its COM socket, on all
six lines. Link on. Each probe on the ESP32's terminal for that line, the same
point as in stage 1, so the two can be compared directly. Every line looks the
same as in stage 1.

**ACC pin 12 (terminal 38), CNT TX.** The heartbeat, `alive` and a zero byte, at 8.49 us a bit,
0 to 3.28 V, exactly once a second: 15 bursts in a row, 0.9996 to 1.0002 s apart.
Terminal 39 is joined to 38 by the link, so it isn't shown again. (The scope's
frequency readout says 20 Hz here, and 26.5 Hz and 32.7 kHz on the stage 1
shots. It can't measure a signal that comes in short bursts, so ignore it.)

![ACC pin 12 at 100 us/div, ESP32 in: the alive burst.](images/scope/acc-pin12-cnt-tx_esp32-in-burst_100us_2026-11-10_1627.png)

![ACC pin 12 at 1 s/div, ESP32 in: a burst once a second.](images/scope/acc-pin12-cnt-tx_esp32-in-heartbeat_1s_2026-11-10_1626.png)

**ACC pin 6 (terminal 13), CS.** 25.00 Hz, low 36.54 ms, period 40.00 ms, high
3.32 V.

![ACC pin 6 at 10 ms/div, ESP32 in: one low per frame, 25 a second.](images/scope/acc-pin6-cs_esp32-in-frames_10ms_2026-11-10_1630.png)

**ACC pin 5 (terminal 10), CLOCK.** 1.96 MHz in bursts of 8, one about every
9 us. The edges are the same as in stage 1: 43 to 45 ns from 10% to 90%, against
45 to 46 ns, and overshoot to 3.68 V and -0.40 V, against 3.64 V and -0.40 V.

![ACC pin 5 at 200 ns/div, ESP32 in: the clock edges, the same as without it.](images/scope/acc-pin5-clk_esp32-clock_200ns_2026-11-10_1631.png)

![ACC pin 5 at 10 us/div, ESP32 in: bursts of 8 clocks.](images/scope/acc-pin5-clk_esp32-clock-bursts_10us_2026-11-10_1631.png)

**ACC pin 4 (terminal 11), DATA2.** About 28 ms of data in each 40 ms frame, then
low, 0 to 3.32 V.

![ACC pin 4 at 10 ms/div, ESP32 in: data, then quiet, in each frame.](images/scope/acc-pin4-data2_esp32-frames_10ms_2026-11-10_1633.png)

![ACC pin 4 at 10 us/div, ESP32 in: data bytes.](images/scope/acc-pin4-data2_esp32-data_10us_2026-11-10_1633.png)

**ACC pin 3 (terminal 14), DATA1.** Still no data. High, mean 3.31 V, with
0.8 V of frame-shaped ripple peaking at 3.84 V (long ground lead).

![ACC pin 3 at 10 ms/div, ESP32 in: high, with frame-shaped ripple.](images/scope/acc-pin3-data1_esp32-in-long-lead_10ms_2026-11-10_1635.png)

## Two and three probes at once

Taken on 2026-11-13. These show how the lines line up with each other, so the
channels are stacked instead of all starting at the bottom line: 2 V/div, with
C1 (yellow) at the top, C2 (pink) below it and C3 (blue) at the bottom. The arrow
on the left of each trace marks its 0 V. Pins 11 and 12 aren't probed together:
the link joins terminals 39 and 38, so both probes would be on the same point and
show the same trace.

**The data starting after power-on.** C1 on ACC pin 12 (terminal 38), CNT TX, and
C2 on ACC pin 6 (terminal 13), CS. ESP32 out, link on. The scope was armed with
the radio off and triggered on the first heartbeat after switching on. CS goes
high about 0.47 s before that first heartbeat. The heartbeat then comes once a
second, and the frames start on the second one, exactly 1.0 s after the first:
the radio needs one heartbeat to come back before it streams. From there CS goes
low every 40.0 ms. Both lines idle at about 3.3 V.

![ACC pins 12 and 6 at 500 ms/div from power-on: the heartbeat once a second on top, and CS starting its 25 Hz frames at the second heartbeat.](images/scope/acc-pin12-cnt-tx_acc-pin6-cs_link-start_500ms_2026-11-13_1459.png)

**The start of one frame.** C1 on ACC pin 6 (terminal 13), CS, and C2 on ACC pin 5
(terminal 10), CLOCK. ESP32 out, link on. The first clock edge comes 4.07 us after
CS falls, then a burst of 8 clocks at 1.95 MHz every 9.0 us. CS goes from 3.24 V to
0 V. The clock swings 3.84 V to -0.64 V with its ringing. While the radio streams,
the clock rests low between bytes and before CS falls. With no link on 2026-11-10
it sat high at 3.32 V. SPI mode 2, which the firmware uses, reads it correctly.

![ACC pins 6 and 5 at 2 us/div: CS falls, and about 4 us later the first burst of 8 clocks, then one every 9 us.](images/scope/acc-pin6-cs_acc-pin5-clk_link-frame-start_2us_2026-11-13_1457.png)

**The first bytes of a frame.** C1 CS, C2 CLOCK and C3 on ACC pin 4 (terminal 11),
DATA2. ESP32 out, link on. The first clock comes 4.07 us after CS falls, at
1.95 MHz. Read on the rising clock edges, most significant bit first, the first two
bytes are 0xB1 and 0x90. They're the first two points of Main's scope row, so they
change from frame to frame. DATA2 swings 3.60 V to -0.32 V, and CS is high at
3.32 V.

![ACC pins 6, 5 and 4 at 2 us/div, ESP32 out: CS falls, then two bytes of data clocked out.](images/scope/acc-pin6-cs_acc-pin5-clk_acc-pin4-data2_link-first-bytes_2us_2026-11-13_1147.png)

**Stage 2: the first bytes at the ESP32.** The same three probes with the ESP32 in
and running the firmware. The first clock comes 4.03 us after CS falls, at
1.97 MHz, a byte every 8.9 to 9.0 us. The first two bytes are 0xBF and 0xD2. CS is
high at 3.24 V, the clock swings 3.84 V to -0.48 V, and DATA2 peaks at 3.60 V: the
same as with the ESP32 out.

![ACC pins 6, 5 and 4 at 2 us/div, ESP32 in: the same as without it.](images/scope/acc-pin6-cs_acc-pin5-clk_acc-pin4-data2_esp32-first-bytes_2us_2026-11-13_1141.png)

The samples behind these four are in [captures/scope/](../captures/scope/), one file
per channel.
