# Streaming Vision Phase 5

## Status

Generic read-only real-stream acceptance is **PASS**. Details gate
calibration is **PASS (3/3)** after calibrating the live Header sharpness
floor to `0.06`. Appraisal gate calibration is **PASS (3/3)** after safe
named setup to AppraisalBars; setup used 3 inputs and gate calibration used 0.

Device: `192.168.1.185:5555`, `ONEPLUS_A6013`, Android 11, physical
`1080x2340` portrait, resolved stream `886x1920`. scrcpy v4.0 and FFmpeg
8.1.2 remain local ignored tools. Every operation reported
`InputCommandsSent = 0`.

## Zero-frame root cause

The bounded raw Annex-B sample was 1,541,472 bytes and contained SPS, PPS and
IDR NAL units. FFmpeg independently identified H.264 High at `886x1920` and
emitted one `886x1920x4` BGRA frame. The application used `-fflags nobuffer`
with a pipe-fed H.264 decoder; in this mode FFmpeg emitted zero rawvideo bytes.
Removing that flag while retaining `-flags low_delay` fixed the pipeline.

The first application proof published 204 frames in 8 seconds. Reports now
record TCP/encoded bytes, transport chunks, FFmpeg stdin/rawvideo bytes,
complete BGRA frames, first-byte/first-frame latency, exit codes and input
count. Phase 5 package-free self-test passes 8/8.

## Real-phone runs

All runs used `control=false`, `audio=false`, `raw_stream=true`.

| Run | Duration | TCP bytes | Decoded | Published | First frame | Shutdown |
|---|---:|---:|---:|---:|---:|---|
| final-1-30s | 30.09s | 28,957,744 | 837 | 837 | 1,690 ms | Clean |
| final-2-30s | 30.09s | 29,021,232 | 833 | 832 | 1,968 ms | Clean |
| final-3-60s | 60.08s | 58,974,752 | 1,691 | 1,691 | 1,646 ms | Clean |

All runs reported zero interruptions/freezes and clean lease shutdown.

## Gate runs

The user manually placed an ordinary Details screen showing Squawkabilly
(CP, species header, Details panel and bottom controls). A short preflight
confirmed 886x1920 frames and compatible screen topology. The first profile
attempt exposed a live-device sharpness mismatch (Header P50 about 0.065-0.075
versus the synthetic 0.18 floor); the calibrated profile now uses
`minimumSharpnessScore = 0.06`. This is a measurement-based threshold change,
not a change to required regions or volatile-region handling.

| Run | Requested | Frames | Final | P50/P95/P99 observation ms | Input | Leases | Shutdown |
|---|---:|---:|---|---:|---:|---:|---|
| details-gate-1 | 10 s | 24 | Passed | 1.418 / 5.967 / 7.607 | 0 | 0 | Clean |
| details-gate-2 | 10 s | 18 | Passed | 1.532 / 8.252 / 9.683 | 0 | 0 | Clean |
| details-gate-3 | 20 s | 21 | Passed | 1.588 / 7.724 / 9.545 | 0 | 0 | Clean |

Representative per-region metrics from the 20-second run (P50/P95/P99):

| Region | Motion | Difference | Similarity | Sharpness |
|---|---:|---:|---:|---:|
| Header | 0.00014 / 0.00137 / 0.80027 | 0.00184 / 0.00232 / 0.80046 | 0.99992 / 0.99995 / 0.99996 | 0.07256 / 0.07551 / 0.07561 |
| Panel | 0.00022 / 0.01277 / 0.80255 | 0.00084 / 0.00204 / 0.80041 | 0.99967 / 1.00000 / 1.00000 | 0.14921 / 0.15160 / 0.15176 |
| BottomControl | 0.00021 / 0.00588 / 0.80118 | 0.00095 / 0.00228 / 0.80046 | 0.99997 / 1.00000 / 1.00000 | 0.17154 / 0.17267 / 0.17273 |
| Model | 0.02888 / 0.03695 / 0.80739 | 0.00529 / 0.00723 / 0.80145 | 0.98568 / 0.99151 / 0.99255 | 0.10602 / 0.10873 / 0.10978 |
| AnimatedBackground | 0.02311 / 0.02863 / 0.80573 | 0.00431 / 0.00599 / 0.80120 | 0.99012 / 0.99427 / 0.99517 | 0.11118 / 0.11327 / 0.11363 |

The approximately 0.80 P99 motion/difference values are first-frame
initialization outliers; the gate requires consecutive stable frames and
passed with Header/Panel/BottomControl stable while Model and
AnimatedBackground remained volatile/ignored. No false transition was
observed after initialization, and no transition evidence was emitted from
the volatile regions. The three runs also serve as repeated start/stop
cycles: no zombie process, forward, lease or input was reported.

## Appraisal gate calibration

The current state was read with five bounded screenshots: four compatible
`PokemonDetails` frames and one `Unknown`; the stable consensus requirement
was met with 3-of-5 compatible frames. The existing named route was then used:

`PokemonDetails -> PokemonMenuOpen -> AppraisalIntro -> AppraisalBars`

The route sent exactly three setup inputs (Details menu, Appraise, and the
single guarded Intro Continue). No Back, raw ADB input, blind retry, modal
confirmation or alternative action was used. AppraisalBars preflight confirmed
`AppraisalBarsDetected`, all three IV bars, the fixed appraisal panel and no
Details/map/list layout. The phone remained on AppraisalBars after setup.

The Appraisal profile is
`profiles/pokemon-go-appraisal-bars-oneplus6t-portrait.json`. Required regions
are Header, AppraisalPanel, AttackBar, DefenseBar and HpBar. BottomControl is
DiagnosticOnly because the lower arrows are not a progression signal; Model
and AnimatedBackground are Volatile.

| Run | Requested | Frames | Final | P50/P95/P99 observation ms | Calibration input | Leases | Shutdown |
|---|---:|---:|---|---:|---:|---:|---|
| appraisal-gate-1 | 10 s | 19 | Passed | 1.924 / 8.454 / 9.786 | 0 | 0 | Clean |
| appraisal-gate-2 | 10 s | 21 | Passed | 1.861 / 6.717 / 8.957 | 0 | 0 | Clean |
| appraisal-gate-3 | 20 s | 24 | Passed | 1.644 / 5.931 / 7.798 | 0 | 0 | Clean |

Representative per-region metrics from the 20-second run (P50/P95/P99):

| Region | Motion | Difference | Similarity | Sharpness |
|---|---:|---:|---:|---:|
| Header | 0.00026 / 0.00916 / 0.77219 | 0.00125 / 0.00233 / 0.77054 | 0.99963 / 0.99985 / 0.99986 | 0.04107 / 0.04286 / 0.04319 |
| AppraisalPanel | 0.00072 / 0.00920 / 0.77234 | 0.00102 / 0.00268 / 0.77062 | 0.99985 / 0.99998 / 0.99999 | 0.18454 / 0.18481 / 0.18486 |
| AttackBar | 0.00000 / 0.00087 / 0.77022 | 0.00097 / 0.00271 / 0.77063 | 0.99980 / 0.99997 / 0.99999 | 0.02923 / 0.02978 / 0.03005 |
| DefenseBar | 0.00000 / 0.00160 / 0.77042 | 0.00070 / 0.00313 / 0.77073 | 0.99989 / 0.99998 / 0.99999 | 0.04664 / 0.04871 / 0.04897 |
| HpBar | 0.00025 / 0.01100 / 0.77256 | 0.00083 / 0.00237 / 0.77055 | 0.99968 / 0.99999 / 0.99999 | 0.08341 / 0.08436 / 0.08459 |
| Model | 0.00099 / 0.00294 / 0.77068 | 0.00117 / 0.00264 / 0.77061 | 0.99990 / 0.99998 / 0.99999 | 0.22805 / 0.22868 / 0.22874 |
| AnimatedBackground | 0.02173 / 0.03159 / 0.77738 | 0.00461 / 0.00707 / 0.77167 | 0.98685 / 0.99353 / 0.99358 | 0.11643 / 0.11983 / 0.12017 |

Selected thresholds are motion `0.05`, difference `0.04`, similarity `0.94`
and sharpness `0.025`. The sharpness floor is above zero and below the
measured AttackBar P50 (`0.02923`) by `0.00423`; it was selected from the
observed IV-bar distribution, not to bypass a failing gate. The roughly 0.77
P99 motion/difference values are first-frame initialization outliers; all
three runs passed after the required stable sequence. Model-only and
background-only changes produced no transition evidence, and unchanged IV
bars/Header produced no false progression. Existing Phase 3 replay tests also
cover stable A/B, no-transition, model-only, background-only and meaningful
regional changes.

`CalibrationInputCommandsSent = 0`; `TotalInputCommandsSent = 3` for setup
plus calibration. Final state is `AppraisalBars`.

## Non-actions

No taps, swipes, key events, clipboard, navigation, Calcy, tags, cleanup,
transfer or delete operations were performed. Phase 6B/7 were not started.
