# Hardware: parts, wiring and the first connection

How I built the board for my FTdx101MP, and how I checked the radio's levels
before connecting the ESP32. For the short version, step by step, use the
[setup guide](setup-guide.md). Everything here is at your own risk; read
[fuse-risk.md](fuse-risk.md) first.

## What I used

- **A 13-pin mini-DIN extension cable, male to female.** The male end goes
  into the radio's ACC socket. Mine is
  [this one from Amazon UK](https://www.amazon.co.uk/dp/B0BRQDDTTZ) (B0BRQDDTTZ, £18.99), with all 13 pins wired.
- **A ready-made 13-pin plug with wires**, the ACC lead from the IC-7300MK2. It
  plugs into the extension cable's female end, so nothing is cut and the board
  is easy to plug in and unplug. Its wires go to the breakout board's terminals.
- **An ESP32-S3 DevKitC-1 N16R8** (marked HW678) on a screw-terminal breakout
  board. Mine is [this one from Amazon UK](https://www.amazon.co.uk/dp/B0F8N8LZ4G) (B0F8N8LZ4G).
  I chose an ESP32 because I couldn't find a UK supplier of an FTDI FT4222
  USB-to-SPI board, the chip the FT-710 uses inside for its scope. The ESP32-S3
  is cheap and easy to get, and its native USB is fast enough for the data.
- **Six 470 ohm resistors**, one in series with each signal wire. See
  [Resistor value and the 2 MHz clock](#resistor-value-and-the-2-mhz-clock).
- **A USB data lead** to the PC, for the socket marked USB. It carries the frames
  and can flash the board. A second lead in the socket marked COM is optional:
  it gives the status text and a steadier port for erasing.

```
Radio ACC socket <- cable male | cable female <- IC-7300MK2 plug -> wires -> breakout
```

**The wire colours in this repository are those of the IC-7300MK2 lead, which
came from Icom with the plug already soldered on. They aren't the colours you get
by cutting the Amazon extension cable in two. If you cut that cable, meter every
wire to find its pin, and don't go by the colours here.**

## The board's LED

The board is an ESP32-S3 DevKitC-1 N16R8, marked HW678, and its RGB LED is on GPIO48
in GRB order, so it's free for status lights. It's an addressable LED, so it needs a
data stream, not a plain high or low. It's driven with the
[Adafruit NeoPixel](https://github.com/adafruit/Adafruit_NeoPixel) library and kept
dim, because it's dazzling at full power.

Some boards differ: version 1.1 of the DevKitC-1 has its LED on GPIO38, which this
project uses for pin 12. Check yours before wiring it. See "The cable and the board"
in [docs/plan.md](plan.md).

## Wiring

![The ESP32-S3 on its screw-terminal board, with the ACC wires marked. Bottom row, the 3V3 to GND row: ACC pin 5 CLOCK (green wire) to GPIO10, pin 4 DATA2 (yellow) to GPIO11, pin 6 CS (blue) to GPIO13, pin 3 DATA1 (orange) to GPIO14, each through 470 ohms, and pin 2 GND (red) to the GND terminal at the end, no resistor. Top row: pin 11 CNT RX (pink) to GPIO39 and pin 12 CNT TX (light blue) to GPIO38, each through 470 ohms.](images/acc-wiring.jpg)

**Pin numbers.** In this repository, "pin" always means a pin of the radio's ACC
socket, numbered as in the radio's manual. The breakout board's screw terminals are
marked with the ESP32's GPIO numbers, so they're called terminals, such as
terminal 38. This table converts one to the other.

**The wire colours are those of my ready-made Icom lead, not the Amazon extension
cable's wires if it's cut in two.**

| ACC pin | Signal | Wire | Breakout terminal |
|---|---|---|---|
| 2 | GND | Red | GND, no resistor |
| 3 | DATA1 | Orange | 14 |
| 4 | DATA2 | Yellow | 11 |
| 5 | CLOCK | Green | 10 |
| 6 | CS | Blue | 13 |
| 11 | CNT RX | Pink | 39 |
| 12 | CNT TX | Light blue | 38 |

The six signal wires that go to the ESP32 (ACC pins 3, 4, 5, 6, 11 and 12) each
have a 470 ohm resistor in series at the terminal end. Ground (ACC pin 2) goes
straight to GND.

These pins are **not connected** and have no resistors. Heat-shrink over the end of
each wire, one wire at a time, is all they need:

- Pin 1, brown: Power ON, the remote power-on line
- Pin 7, purple: 3.3 V through 1k, which can't power anything
- Pin 8, gray: 13.8 V behind a fuse
- Pin 9, white: the radio's PTT line
- Pin 10, black: the radio's SEND line
- Pin 13, light green: a second ground; pin 2 is the one I use

The details, and the checks to make before connecting anything, are in "Wiring to
the ESP32-S3" in [docs/plan.md](plan.md).

### Resistor value and the 2 MHz clock

The resistors are there for protection while the radio's levels are still unknown.
The [FTDX10 projects](../README.md#credits-and-background) this follows wire straight through with no resistors, and it
works. A resistor at the ESP32 end also softens fast edges and damps ringing. That
may help with the crosstalk [SLTPLAN](https://github.com/SLTPLAN/FTDX10_WF_HUB) worked around by lowering the ESP32's drive
strength on the pin 11 line.

The fastest line is the 2 MHz SPI clock. Each half-cycle lasts 250 ns. With a short
wire into the ESP32 (about 30 pF in all), the resistor slows each edge by roughly
the rise time below. The protection columns show the most current that can flow in
two faults: a 3.3 V line shorted to ground or crossed with another line, and a line
that turns out to be 5 V, pushing current into the ESP32's protection diodes. Less
current is better.

| Resistor | Edge rise time (10 to 90%) | Share of a half-cycle | 3.3 V line shorted or crossed | Line at 5 V, into the ESP32 | Protection compared with 220 ohm |
|---|---|---|---|---|---|
| 220 ohm | about 15 ns | 6% | 15 mA | 7.7 mA | 1 x |
| 330 ohm | about 22 ns | 9% | 10 mA | 5.2 mA | 1.5 x |
| 470 ohm | about 30 ns | 12% | 7 mA | 3.6 mA | 2.1 x |
| 1k | about 65 ns | 26%, still works but getting soft | 3.3 mA | 1.7 mA | 4.5 x |

The table shows 470 ohm as the best balance: twice the protection of 220 ohm, at
12% of the clock's half-cycle, and that's why it was chosen.

The 5 V figures assume the ESP32's pin is clamped at about 3.3 V. Espressif doesn't
publish a safe current into a pin, so treat any 5 V line as a reason to stop and add
level shifting, not something the resistor makes safe. No resistor protects against
pin 8: 13.8 V through 470 ohm is still about 22 mA, which is why pin 8 isn't
connected at all.

Clock and data each have the same resistor, so they're delayed together and stay in
step. If the scope shows rounded clock edges at the ESP32 pin, use a lower value
on the clock line.

### Yes, ground is the red wire

Red for ground looks like the last thing any sane person would do, but the colours
aren't my choice. They come with the ready-made 13-pin lead (the IC-7300MK2's ACC
plug), and they follow the resistor colour code by pin number, not by what the wire
does: pin 1 brown, 2 red, 3 orange, 4 yellow, 5 green, 6 blue, 7 purple, 8 gray,
9 white and 10 black. Pink, light blue and light green cover pins 11 to 13. Pin 2 is
ground, so ground is red.

The upside is that anyone who knows the code can read a wire's pin number from its
colour. The downside is that the usual meanings don't apply:

- **Red is ground.** Mark it GND with a label or a band of tape.
- **Gray is 13.8 V** (pin 8). It's not connected.
- **Black is SEND** (pin 10), not ground. It's not connected.
- Blue and light blue are different signals, and so are green and light green.

**These are the colours of the ready-made Icom lead, not the Amazon extension
cable's.** Other leads may use other colours, so check yours with a meter before
wiring.

## Safe first connection

The radio's levels on these pins are measured before anything is connected to them.
The ESP32 goes on only once every line is known to be 3.3 V or less. There are two
stages:

1. **Stage 1:** the breakout on the radio, nothing else connected, and the
   oscilloscope on each line.
2. **Stage 2:** the radio powered off, the ESP32 connected, then power on and check
   again.

Three rules apply all the way through:

- **Unplug the radio's DC lead before plugging or unplugging anything.** Switching
  the radio off on its front panel isn't enough. Switch the power supply off
  before pulling the DC plug or putting it back. On my FTdx101MP the contacts
  flash if the plug goes in with the supply on. The lead may hold a little charge
  for a while after the supply is switched off, which doesn't matter.
- **Move probes and wires only with the DC lead out.** A probe that slips on a live
  terminal can short two lines.
- **The scope's ground clips go only on pin 2, the red wire.** A mains-powered
  scope's ground is mains earth, so a ground clip on any other line shorts that line
  to earth.

### Before you start

- Every unused wire (pins 1, 7, 8, 9, 10 and 13) has its own heat-shrink over the
  end.
- With the cable out of the radio, pin 8 meters open to every other pin and to the
  shell. See [docs/fuse-risk.md](fuse-risk.md).
- The 470 ohm resistors can stay in place. With no ESP32 connected, no current flows
  through them, so the far end of each resistor sits at the same voltage as the
  radio's pin. A 10 Mohm scope probe draws less than a microamp, which drops less
  than a millivolt across 470 ohms. Measuring at the free end of the resistor is
  the same as measuring at the radio.
- Set up each scope channel:
  - Probe at 10X, and the channel set to 10X to match. Check this on the scope's
    calibration output, which should read 3 V peak to peak.
  - 1 V/div with the trace near the bottom of the screen, so the screen shows 0 to
    about 7 V and a 5 V line can't go off the top.
  - DC coupling.
  - Trigger on the channel you're watching: rising edge, about 1.5 V, DC
    coupling, Normal mode.

### Stage 1: radio only, no ESP32

1. With the DC lead out, plug the breakout into the ACC socket. Clip the probe
   ground to ACC pin 2 (the GND terminal), and put the probe tip on the free end
   of ACC pin 12's resistor (terminal 38).
2. Plug in the DC lead and switch the radio on. Wait for it to finish starting.
3. **ACC pin 12 (CNT TX, out of the radio), terminal 38:** expect a short burst once a second. This
   is the `alive` heartbeat: 6 bytes at 115200 baud, about 0.5 ms long, so try
   1 ms/div. Note the high level.
4. Switch off, pull the DC lead, and move the probe to the next line. Repeat for
   **ACC pin 11** (CNT RX, an input to the radio, terminal 39) and **ACC pins 3,
   4, 5 and 6** (the SPI lines, terminals 14, 11, 10 and 13). Without a reply to the heartbeat, the SPI lines should stay idle. Note
   each line's idle level. With a 4-channel scope, you can watch pins 12, 6, 5 and
   4 at once, all ground clips on pin 2.
5. **Link ACC pins 11 and 12 (terminals 39 and 38).** With the DC lead out, join the free ends of the pin 11
   and pin 12 resistors with a short wire. The radio's heartbeat now comes back to
   it through 940 ohms, which is the reply it wants. Power on and look again:
   - Pin 12 still bursts once a second, and pin 11 shows the same burst.
   - After two or three seconds, **pin 6 (CS)** goes low 25 times a second,
     **pin 5 (CLK)** shows a 2 MHz clock while CS is low, and **pin 4 (DATA2)**
     carries data. Note whether **pin 3** does anything, and whether that
     changes with the Sub receiver on.
   - Note the high level of every line.
6. **Take the link off** (DC lead out first), power on, and watch pins 5 and 6.
   The data should stop within two or three seconds, while pin 12 carries on once a
   second. That shows the heartbeat reply is what turns the data on.

**Pass:** every line's high level is 3.3 V or less. The ESP32-S3's absolute maximum
is 3.6 V. **If any line goes above 3.6 V, stop.** Don't connect the ESP32. That line
needs level shifting first, and the resistor doesn't make it safe. Write the levels
in [docs/bench-notes.md](bench-notes.md) either way.

### Stage 2: adding the ESP32

1. **Make sure the ESP32's pins are all inputs.** Old firmware on the board may
   drive some of these GPIOs as outputs, so erase it first, as in
   [Erasing the ESP32](#erasing-the-esp32) below. After an erase, every GPIO is
   an input. None of the GPIOs used here (10, 11, 13, 14, 38 and 39) is a
   strapping pin, so the board starts normally with the radio connected.
2. Pull the radio's DC lead. Connect the ESP32's GND terminal to pin 2 **first**,
   then the six resistor ends to their GPIOs, as in [Wiring](#wiring).
3. Leave the pin 11 to pin 12 link on, so the radio streams while the ESP32
   only listens.
4. **Power the ESP32 first:** plug its USB lead into the PC. Then plug in the
   radio's DC lead and switch on. When finishing, switch the radio off first and
   the ESP32 last. That way, the radio never drives a line into an ESP32 that has
   no power. An unpowered ESP32 would be fed through its protection diodes.
5. Repeat the checks from stage 1, step 5, with the probes at the ESP32 terminals.
   The levels should be the same as before. On mine they were, and the clock
   edges too: the ESP32's few pF behind 470 ohms adds only about 2 ns, which
   doesn't show. See [Stage 2, ESP32 in](scope-traces.md#stage-2-esp32-in-the-radio-still-streams).
6. When firmware takes over the heartbeat reply on GPIO39, take the link off
   first, with the DC lead out. Otherwise the radio's pin 12 and the ESP32 would
   both drive the same line. The 940 ohms keep that below 4 mA, but it's still a
   fault.

### Erasing the ESP32

Erasing wipes everything in the ESP32's flash, so it runs no program at all and
every GPIO stays an input. Do it with the ESP32 off the breakout and nothing wired
to its terminals except what's always there.

**Find the ESP32's COM port first.** Plug the ESP32's **UART** socket, marked
**COM** on my board, into the PC with a USB data lead. Open Device Manager and look under "Ports (COM & LPT)". The
board's port is the one that appears when you plug it in and goes when you unplug
it. On a board with a CH343 chip it's named "USB-Enhanced-SERIAL CH343". **Never pick the
radio's ports.** The Yaesu's ports are a pair named "Silicon Labs Dual CP2105" or
"Silicon Labs CP210x", Enhanced and Standard. If in doubt, unplug the radio's USB
lead while you do this.

Then use either of these.

**Option 1: in the browser, nothing to install.** This needs Chrome or Edge.

1. Open Espressif's [ESP Tool](https://espressif.github.io/esptool-js/) page.
2. Under Program, leave the baud rate as it is and click **Connect**.
3. Choose the ESP32's port from the list and click Connect. The page shows the
   chip, "ESP32-S3".
4. Click **Erase Flash** and wait for it to finish. It takes a few seconds.
5. Click **Disconnect**.

A good run looks like this (MAC address left out):

```
Serial port WebSerial VendorID 0x1a86 ProductID 0x55d3
Connecting...
Detecting chip type...
ESP32-S3
Chip is ESP32-S3 (QFN56) (revision v0.2)
Features: Wi-Fi,BLE,Embedded PSRAM 8MB (AP_3v3)
Crystal is 40MHz
Uploading stub...
Running stub...
Stub running...
Changing baudrate to 921600
Changed
Flash ID: 184068
Erasing flash (this may take a while)...
Chip erase completed successfully in 4.819s
```

The lines to check:

- **VendorID 0x1a86** is WCH, the maker of the board's CH343 USB chip, so it's the
  ESP32's port and not the radio's. The radio's Silicon Labs chip is 0x10c4.
- **ESP32-S3** and **Embedded PSRAM 8MB** confirm the N16R8 board.
- **Chip erase completed successfully** is the one that matters.

Your revision, MAC address and timing may differ.

**Option 2: the erase script in this repository.** It needs Python 3.10 or later.
Install the tools once, from the top folder of the repository:

```
python -m pip install -r tools/requirements.txt
```

That installs the pinned esptool version, and Pillow for the scope script. To keep
them apart from your other Python packages, make a virtual environment first with
`python -m venv .venv` and use `.venv\Scripts\python` (on Windows) in place of
`python`. Then run:

```
python tools/erase_esp32.py
```

Start with the board unplugged. The script lists the serial ports, then waits
for you to plug the board's UART (COM) socket in, and takes the one port that
appears. That's the board in your hand, even if something else with the same USB
chip is plugged in. Then it:

1. **Keeps esptool off anything that may be a radio.** It only goes on if the
   port's USB chip is WCH's (0x1A86, the board's CH343) or Espressif's (0x303A),
   and never on COM3 or COM4. Asking a chip what it is pulses DTR and RTS, and on
   a radio's port those can key the transmitter. The Yaesu's ports are Silicon
   Labs (0x10C4), so they're never touched.
2. **Asks the chip what it is,** and stops unless it's an ESP32-S3. Anything
   that isn't an ESP32 can't answer, so it's never erased.
3. **Shows the chip, flash size, PSRAM and MAC address** and asks you to type
   `yes`.
4. Runs esptool's erase.

`python tools/erase_esp32.py --list` only lists the ports and their USB IDs, and
`--port COM5` names the port instead of plugging in, with the same checks.

A good run on my board, with the MAC address left out:

```
Port: COM5  1A86:55D3  USB-Enhanced-SERIAL CH343 (COM5)

Asking the chip what it is: -m esptool --port COM5 flash-id
It's an ESP32-S3:
  Chip type:          ESP32-S3 (QFN56) (revision v0.2)
  Features:           Wi-Fi, BT 5 (LE), Dual Core + LP Core, 240MHz, Embedded PSRAM 8MB (AP_3v3)
  Detected flash size: 16MB

Erasing wipes all of the flash on COM5.
Erase it? Type yes: yes
...
Erasing flash memory (this may take a while)...
Flash memory erased successfully in 2.7 seconds.
```

To run esptool yourself, with your board's port in place of COM5:

```
python -m esptool --chip esp32s3 --port COM5 erase-flash
```

Older versions of esptool, before 5.0, spell the command `erase_flash`.

**If it can't connect** ("Failed to connect" or "No serial data received"), put
the board into download mode by hand. Hold the **BOOT** button, press and release
**RST**, then release BOOT, and try again. Once it's erased, press RST to restart
the board.

After an erase, the board's LED stays off. If you open a serial monitor on the
UART port, the board repeats a line like `invalid header: 0xffffffff`. That's
normal for an erased board. It means there's no program to run, which is what you
want.

**If the board is powered from its other socket, marked USB,** Windows keeps
connecting and dropping it, about every 3 seconds, as port 303A:1001. That's
normal for an erased board too: each time the chip retries, its own USB restarts.
Use the COM socket, where the CH343 chip keeps the port steady.
