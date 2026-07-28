# Streaming Vision Phase 5

## Status

Phase 5 real-phone acceptance is **not accepted**. The phone was contacted only
through read-only ADB/scrcpy video operations; `InputCommandsSent` remained 0.
The requested 30s, 30s and 60s observe runs completed without decoded frames,
so gate execution, regional calibration, timing acceptance and screenshot
evidence were not run.

## Device and local toolchain

- Device: `192.168.1.185:5555`, `ONEPLUS_A6013`, Android 11, physical
  `1080x2340` portrait.
- Preflight stream resolution: `886x1920` at max-size 1920.
- ADB: platform-tools `37.0.0-14910828`, ADB `1.0.41`.
- scrcpy: official v4.0 ZIP, SHA-256
  `75DBEB5B00E6F64292F26F70900AE55CA397786BDFB0B9BBEB481A0549047457`.
  Server SHA-256:
  `84924BD564A1EB6089C872C7521F968058977F91F5FF02514A8C74AFF3210F3A`.
- FFmpeg: `8.1.2-essentials_build-www.gyan.dev`; ZIP SHA-256
  `DB580001CAA24AC104C8CB856CD113A87B0A443F7BDF47D8C12B1D740584A2EC`.

The binaries are local ignored files under `tools/local/`; no binary is
committed.

## Observed result

The three requested runs produced zero encoded/decoded/published frames and
were recorded as not accepted. An isolated raw-protocol diagnostic did read
H.264 SPS/PPS bytes from the same device and local scrcpy server, so this does
not prove the application-level observe path. No gate or calibration claim is
made.

During diagnosis, the transport was corrected to obtain metadata from the
first packet before initializing FFmpeg, to preserve FFmpeg/server errors, and
to query display dimensions before server startup. These changes are offline
validated below, but the real observe path still needs a future bounded
investigation before Phase 5 can be accepted.

## Explicit non-actions

No control channel, taps, swipes, navigation, state-changing ADB command,
randomized timing or anti-detection behavior was used. Phase 6 was not started.
