# Streaming Vision Phase 5

## Status

Generic read-only real-stream acceptance is **PASS**. Details gate
calibration is **PASS (3/3)** after calibrating the live Header sharpness
floor to `0.06`; Appraisal calibration remains
`PENDING_MANUAL_PLACEMENT`.

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

Appraisal was not opened or navigated to. `AppraisalGateCalibration =
PENDING_MANUAL_PLACEMENT`; this does not invalidate the green Details gate.

## Non-actions

No taps, swipes, key events, clipboard, navigation, Calcy, tags, cleanup,
transfer or delete operations were performed. Phase 6B/7 were not started.
