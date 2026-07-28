# Streaming Vision Phase 6A — VLM evidence bake-off

This document records the isolated, local bake-off from the baseline commit
`5c7d1d0c765efba47bf97812c0819c48d66e384f` on branch
`feature/streaming-phase6a-vlm-evidence-bakeoff`.

## Scope and safety

The runner talks only to the local Ollama HTTP API. It does not reference
`PogoInventory.Device`, `Automation`, ADB, a phone, location input, or any
inventory mutation. The runner reports `AuthorizesPhoneInput: false` and
`InputCommandsSent: 0`. No model blobs, raw evidence, private images, or
absolute machine paths are committed.

The request uses `/api/chat` with `stream: false`, `think: false`,
`temperature: 0`, and the actual JSON schema object as `format`. The parser
accepts only the documented statuses and downgrades a model-emitted `Known`
status to `Candidate`.

## Models

The four requested new models were pulled and executed, with the existing
`qwen3.5:9b` retained as the slow baseline. The local metadata manifest is
kept outside the repository and is not committed.

| Model | Parameters | Quantization | Digest prefix |
|---|---:|---|---|
| qwen3-vl:2b-instruct | 2.1B | Q4_K_M | ea422f1e7365 |
| minicpm-v4.6:1b | 752.16M | Q4_K_M | e95583acac77 |
| gemma3:4b | 4.3B | Q4_K_M | a2af6cc3eb7 |
| qwen3-vl:4b-instruct | 4.4B | Q4_K_M | ee4b975b58c1 |
| qwen3.5:9b baseline | 9.7B | Q4_K_M | 6488c96fa5f |

Hardware observed before benchmarking: NVIDIA GeForce RTX 4060 Ti, 8188 MiB.
At least 25 GB of free C: space was confirmed before pulls.

## Results

Stage 1 used 3 cases and the prescribed cold/warm pattern. The first run was
discarded as a runner configuration failure: all responses were truncated at
128 output tokens (`done_reason=length`). After raising the output ceiling to
512 tokens, the rerun produced 57 rows and 43 schema-valid responses.

| Model | Valid / rows | Valid rate | Median total ms |
|---|---:|---:|---:|
| qwen3-vl:2b-instruct | 12 / 12 | 100% | 2,993 |
| gemma3:4b | 12 / 12 | 100% | 5,872 |
| qwen3-vl:4b-instruct | 12 / 12 | 100% | 4,782 |
| minicpm-v4.6:1b | 4 / 12 | 33.3% | 3,691 |
| qwen3.5:9b baseline | 3 / 9 | 33.3% | 9,073 |

Stage 3 used all 12 manifest cases, 5 warm runs plus 1 cold run per case,
for each of the two selected models. It completed in 12.8 minutes with 139
valid responses out of 144.

| Model | Valid / rows | P50 total ms | P95 total ms | P99 total ms |
|---|---:|---:|---:|---:|
| qwen3-vl:2b-instruct | 67 / 72 | 2,911 | 4,228 | 10,476 |
| gemma3:4b | 72 / 72 | 6,144 | 18,250 | 22,519 |

The evidence pack retains raw envelopes, parsed responses, per-run Ollama
metrics, copied images, fixed semantic crops, CSV, JSON summary, ranking
markdown, and HTML index. Accuracy, false-known, and false-complete remain
null because the baseline contains synthetic fixtures only; no verified real
Android/Calcy truth was introduced by this bake-off.

The baseline fixture set has no committed `GameplayMap` screenshots. The
manifest therefore records the available `Unknown` and negative/unsupported
fixtures explicitly rather than fabricating map truth. This is a coverage
limitation, not evidence that a model recognizes GameplayMap.

## Recommendation

For the next offline semantic integration experiment, use
`qwen3-vl:2b-instruct` as the latency-first candidate and retain
`gemma3:4b` as the schema-reliable comparison. Neither is production-approved:
the required 20-Pokémon verified report with zero false Complete observations
does not exist, and all extracted values remain candidates until independently
verified.
