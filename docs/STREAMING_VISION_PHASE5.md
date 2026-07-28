# Streaming Vision Phase 5

## Status

Generic read-only real-stream acceptance is **PASS**. Gate calibration is
**NOT ACCEPTED**: three bounded gate runs observed frames but timed out
conservatively on `MotionTooHigh`/`SharpnessTooLow` in required Header.

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

Three read-only 10-second gate observations completed with 68–80 frames and
zero outstanding leases. They timed out on required Header motion/sharpness;
no regional threshold or gate PASS is claimed. No phone placement request was
made during the run. The selected evidence frame
`local-data/validation/streaming-phase5/gate-1-10s/frames/frame-00000049-BestHeaderFrame.png`
shows the phone on the Pokémon GO map, not Details/Appraisal. Generic stream
acceptance is complete; only physical placement on a valid Details or Appraisal
screen remains before regional calibration can be rerun.

## Non-actions

No taps, swipes, key events, clipboard, navigation, Calcy, tags, cleanup,
transfer or delete operations were performed. Phase 6B/7 were not started.
