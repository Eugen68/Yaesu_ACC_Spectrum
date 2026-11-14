# Comparison tools

These made the figures in [docs/comparison.md](../../docs/comparison.md). They
record the ACC scope and the two SDRs at the same moment, then measure them the
same way. Everything is receive only.

- `compare-capture.csproj`, `Program.cs`: records for a given number of seconds.
  The ACC rows come straight from the board, through the YaesuAccScope library.
  The SDR frames come from a running YWC on `localhost:8080`, exactly as YWC
  sends them to its pages. YWC must not be using the ACC board itself.
  It writes `<prefix>-acc.bin`, `<prefix>-sdrA.bin`, `<prefix>-sdrB.bin` and
  `<prefix>-t0.txt`.
- `toggle.py`: runs a capture while switching one receive setting on Main
  (the attenuator, or a scope setting such as LEVEL) every second or two, and
  logs when. `seg.py` splits the capture at the switches. Switching back and
  forth cancels slow fading on the band.
- `an.py <prefix> <span Hz>`: floor spread, jitter, strongest signal and clarity
  for each source, with the ACC rows replayed at each Refine step.
- `detect.py <prefix> <span Hz>`: the share of weak signals each source shows,
  frame by frame, at a fixed 5% false-alarm rate. Signals are picked on one
  ACC receiver and one SDR, and measured on the other two, so the noise that
  picked them is not the noise they are measured in. The docstring gives the
  details. `MODE=middle` keeps only signals in the middle 45% of the span, and
  `MODE=noskirt` leaves out signals within 1 kHz of anything 25 dB above the
  floor. Those two checks traced the broadcast-band gap.
- `agg.py <prefix> <span Hz>`: runs `an.py` and `detect.py` measures for one
  recording and saves them as `<prefix>.json` in the folder named by `RES`.
- `summ.py <res folder>` and `dtab.py <res folder> <run prefix>`: the tables in
  the write-up, from those JSON files. Set `RUNS` for `summ.py`; `MINSIG`
  (default 20) is the fewest signals `dtab.py` will report a figure for.
- `steplag.py <prefix>`: how long each source takes to follow an attenuator
  step, from a `toggle.py` recording. `DWELL` is the time between switches.
- `sig.py <prefix> <span Hz> <centre Hz>`: frequency offset between the sources,
  and how far weak signals stand out of the floor on each, signal by signal.
- `scale.py <prefix> <span Hz>`: checks each SDR's frequency axis against the
  ACC's. It found YWC's 500 kHz SDR span showing 1 MHz.
- `clip.py <prefix>...`: where the ACC floor sits on the 0–255 scale, to find
  scope LEVEL settings that clip it.
- `detplot.py <res folder> <run prefix> <out.png>`: the detection-by-span chart.
- `spur.py <prefix> [offset kHz]`: how far above the floor each source is at
  one offset in a 1 MHz span. It measured the false signals from overload and
  from signals folding in from outside the span.
- `plot.py <prefix> <span Hz> <title> <out.png>`: the pictures in
  [docs/images/compare/](../../docs/images/compare/).

Python needs numpy, and matplotlib for `plot.py`. Set `ACC_COMPARE_DATA` to the
folder for the recordings (default `data`) and `ACC_PORT` to the board's port.
The recordings aren't in the repo; a minute of all four is about 10 MB.
