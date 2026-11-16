# Getting the best picture

Five things decide how much the ACC scope shows you. The advice here comes
from measurements on my FTdx101MP on 2026-11-14. The figures and how they were
taken are in [comparison.md](comparison.md).

## 1. Keep the scope LEVEL at +20 or more, on every band

The ACC rows are the radio's own scope data, after its scope LEVEL control. If
LEVEL is too low, the noise floor sits on the bottom of the scale, and any
weak signal at the floor is cut off with it.

- **Set LEVEL to about +23.5** on Main and on Sub. At +23.5 almost none of the
  floor touched the bottom on 160, 31, 20 or 10 m, and the strongest signals I
  saw never reached the top.
- **Set it on every band.** The radio keeps a separate LEVEL for each band and
  each receiver. Setting it on 20 m doesn't change it on 160 m. My Main receiver
  was at +23.5 on 20 m and +05 on 160 and 10 m without my knowing.
- **At +05 the floor clipped.** 11% of the points sat on the bottom on 160 m,
  and 15% on 10 m.

YWC warns **LEVEL too low** on a spectrum panel when 5% or more of the points
sit on the bottom. Turn LEVEL up on that receiver until it goes.

## 2. Choose Refine for the span

Each raw row is a separate snapshot with a rough floor. Refine averages the
last few rows, so the floor settles and weak signals stand out of it. Higher
Refine shows more weak signals, but the trace takes longer to follow a signal
that starts, stops or moves. Refine is on YWC's spectrum panels, and starts at 3.

| Span | Refine 3 | Refine 5 |
|---|---|---|
| 1 and 2 kHz | Shows more than an SDR already. 1 to 1.6 s to follow a change, mostly the radio's own scope | A few more weak signals, and 0.2 to 0.4 s slower again |
| 5 to 20 kHz | Level with an SDR. 0.4 to 0.6 s to follow a change | Ahead of an SDR. 0.7 to 1.1 s |
| 50 and 100 kHz | Behind an SDR. About 0.4 s | Level with an SDR. About 0.7 s |
| 200 kHz to 1 MHz | Ahead of an SDR. About 0.4 s | Further ahead. About 0.7 s |

The times are how long the trace takes to show 90% of a sudden change. Raw
(Refine 0) is fastest, but it showed fewer weak signals than everything else
at every span from 5 kHz up.

As a rule of thumb:

- **Refine 3** for general use.
- **Refine 5** for hunting weak, steady signals, such as FT8 or a beacon, on a
  quiet band.
- **Refine 1 or Raw** when you're tuning across the band and want the trace to
  keep up.

If you write your own program with the YaesuAccScope library, `ScopeRefiner`
does the same averaging. Its default weight, 0.3, is YWC's Refine 3. The ACC
Spectrum Viewer draws the raw rows; it's for checking that the board works.

## 3. Leave PEAK and SPEED as you like them

The radio's scope PEAK and SPEED settings make no difference to the ACC rows.
They change only the radio's own display. Switching PEAK between 0 and 3, and
SPEED between 0, 2 and 4, left the floor and its roughness unchanged.

## 4. If you also have an SDR on the IF output

The ACC scope and an SDR on the radio's IF output are close. Which one shows
more weak signals depends on the span:

- **1 and 2 kHz:** the ACC scope. Its points are 1.3 and 2.5 Hz apart, so a
  carrier is a sharp spike. YWC's SDR points are 7.6 Hz apart.
- **5 to 20 kHz:** level at Refine 3.
- **50 and 100 kHz:** the SDR, unless you use Refine 5.
- **200 kHz to 1 MHz:** the ACC scope.

Use the SDR when you need:

- spans wider than 1 MHz, or the 15, 30 and 40 kHz spans;
- a trace that follows changes quickly: an SDR shows 90% of a change in
  under half a second at every span;
- a span different from the radio's own scope. Changing the ACC span changes
  the radio's scope too, because it is the radio's scope.

## 5. For the audio scope, turn the AGC on and open the filter

The audio scope (the AF-FFT and oscilloscope beside each receiver) shows the
receiver's audio, so the receive settings decide what it shows. I measured it
on 2026-11-16 with FT8 on 20 m (Main) and 17 m (Sub), 30 seconds (two FT8
cycles) at each AGC setting, RF gain fully up, IPO on and no attenuator.

- **Don't leave the AGC off.** With it off, the FT8 tones stood 42 (Main) and
  63 (Sub) steps above the AF-FFT's floor. With any AGC setting they stood 71
  to 77 steps above it.
- **With the AGC off the oscilloscope clips.** Nothing holds the audio down, so
  on a busy band it fills the whole box: Sub's ran from 2 to 250, the ends of
  its range, in 300 of 376 frames. With SLOW it used about 60 steps of the
  255 and never filled the box.
- **SLOW was best, but only just.** FAST, MID, SLOW and AUTO were all within a
  few steps of each other. SLOW gave the most (74 on Main, 77 on Sub). AUTO
  pumped least on Main, so it's a good choice too.
- **The AGC also keeps the trace's height steady.** With it off, the
  oscilloscope swells when stations transmit and shrinks to a thin line in the
  gap at the end of each FT8 cycle. With the AGC on it still thins now and
  then, but much less.
- **Open the filter to see the whole passband.** The AF-FFT shows 0 to 4 kHz,
  but only what the receive filter lets through. In DATA-U my Main filter was
  600 Hz, and the AF-FFT showed a narrow hump with a few FT8 tones on it. At
  4000 Hz it showed the whole FT8 slot, about 100 Hz to 3.6 kHz. In DATA and
  CW modes, 4000 Hz is SH code 21; in SSB it is code 23.
- **The AF gain makes no difference.** The audio scope is taken before the
  volume control.
