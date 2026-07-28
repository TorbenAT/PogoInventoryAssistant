# Streaming Vision Phase 6A

## Status

Phase 6A is a technical offline benchmark delivery. It remains isolated from
live streaming, Device, Automation, navigation and all phone input.

## Runtime evidence

- Python: 3.13.14 embeddable runtime, SHA-256
  `90B4E5B9898B72D744650524BFF92377C367F44BD5FBD09E3148656C080AD907`.
- GPU: NVIDIA GeForce RTX 4060 Ti, driver 591.86, 8188 MiB.
- Framework: PyTorch 2.11.0+cu128; CUDA 12.8; `torch.cuda.is_available()` true;
  tensor smoke test completed on `cuda:0`.
- Alternative OCR: EasyOCR 1.7.2, GPU mode, fixed crop only.
- All runtimes, caches and model weights are outside Git under
  `C:\Data\PokemonGo-Tools`.

## Benchmarks

### Embedding-equivalent model

Torchvision `ResNet18_Weights.DEFAULT` was used as a documented visual
embedding-equivalent baseline (penultimate representation), not as a semantic
Known/Complete authority. Batch size was 8 over 50 timed GPU runs:

- P50: 3.570 ms
- P95: 3.883 ms
- P99: 4.016 ms
- Peak VRAM: 112,626,688 bytes

Report: `C:\Data\PokemonGo-Tools\manifests\phase6a-resnet18-gpu.json`.

### Alternative OCR

EasyOCR ran on a fixed `886x420` header crop from the bounded local stream
evidence. Five timed GPU runs produced P50 69.848 ms, P95/P99 73.289 ms and
peak VRAM 344,411,648 bytes. The crop has no verified field truth, so outputs
are diagnostics only; False Known and False Complete are `null`, not zero.

Report: `C:\Data\PokemonGo-Tools\manifests\phase6a-easyocr.json`.

The existing deterministic consensus benchmark remains the fail-closed
baseline and reports False Complete = 0 on its synthetic truth case.

## Truth boundary

`data/phase6a-truth-manifest.synthetic.json` is the only truth manifest used
for correctness claims in this phase. It contains `SyntheticKnown` evidence
only. Real phone screenshots and the EasyOCR crop are `Unverifiable`; no real
field accuracy, False Known or False Complete claim is made for them.

## Safety

No live semantic integration was performed. No tap, swipe, key event,
clipboard, navigation, Calcy, tag, cleanup, transfer or delete operation was
used. `InputCommandsSent = 0`.
