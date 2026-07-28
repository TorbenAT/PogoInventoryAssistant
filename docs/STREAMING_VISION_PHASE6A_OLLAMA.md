# Streaming Vision Phase 6A — Ollama benchmark

This branch reuses the existing local Ollama service through an isolated
HTTP adapter. It remains offline and read-only: no Device, Automation, live
streaming, navigation, Calcy, tagging, transfer, or delete reference was
added. Ollama output is a candidate diagnostic only and can never become
`FieldReadingStatus.Known` by itself.

## Installation and models

The API was available at `http://localhost:11434`. The Ollama executable was
found under the existing user installation but was not on PATH. The catalog
reported:

| Model | Digest | Size | Parameters | Quantization | Capabilities | Phase 6A use |
|---|---|---:|---|---|---|---|
| `nomic-embed-text:latest` | `0a109f...45e59f` | 274 MB | 137M | F16 | embedding | text-only similarity support |
| `qwen2.5-coder:14b` | `9ec889...6849` | 8.99 GB | 14.8B | Q4_K_M | completion/tools/insert | text-only smoke test; not vision |
| `qwen3.5:9b` | `6488c9...893ea7` | 6.59 GB | 9.7B | Q4_K_M | vision/completion/tools/thinking | vision candidate diagnostic |
| `qwen3.6:latest` | `07d352...f28522` | 23.94 GB | 36.0B | Q4_K_M | vision/completion/tools/thinking | not loaded; unnecessary for this VRAM |

Exact digests and full local model metadata are recorded outside Git in the
configured local tools manifest directory (`<tools-root>\manifests`).

## Practical probes

`nomic-embed-text` accepted six batch inputs (`Pikachu`, `Pikachu Libre`,
`Raichu`, `Fletchling`, `CP 219`, `CP 279`) and returned six vectors of stable
dimension 768. Repeating `Pikachu` returned cosine similarity 1.0. This is
text embedding, not image embedding, and is not authoritative species or CP
evidence.

`qwen2.5-coder:14b` returned deterministic JSON for a short text prompt. It
was not used as an image analyzer.

`qwen3.5:9b` accepted the committed `PokemonDetails.png` screenshot. A full
screen smoke test took approximately 84.3 seconds, reported a JSON-like
diagnostic, and used approximately 5.52 GB model VRAM according to Ollama
`/api/ps`; `nvidia-smi` moved from about 1.8 GiB to 7.6 GiB used during the
request. The result did not match the required object-field schema and was
therefore rejected as `InvalidModelResponse`.

The bounded benchmark ran six embeddings and five warm vision requests. All
five VLM results were invalid-schema candidates, with no `Known` or Complete
promotion. This is a schema/quality diagnostic, not semantic accuracy.

## Adapter and contracts

`PogoInventory.Streaming.Semantics.Ollama` provides:

- model catalog and capability parsing through `/api/tags` and `/api/show`;
- batch text embeddings through `/api/embed` with dimension validation;
- structured vision candidate calls through `/api/chat`;
- timeout, HTTP, invalid JSON, missing-property, and schema fail-closed paths;
- SHA-256 input-image evidence, prompt/schema version, digest, and metrics.

The adapter reads `POGO_OLLAMA_BASE_URL` or `OLLAMA_HOST`, defaulting to
`http://localhost:11434`. Model names are explicit CLI/configuration values;
the runtime never downloads models.

## Tests and safety

Package-free fake HTTP tests cover model capabilities, embeddings, valid
candidate translation, invalid responses, and the rule that a model-reported
`Known` status is reduced to `Candidate`. Phase 6A self-test: 12/12.

Benchmark safety result: `False Known = 0`, `False Complete = 0`,
`InputCommandsSent = 0`. No real truth accuracy is claimed because the clean
baseline has no verified Phase 6A truth manifest.

## Limitations

The current model prompt/schema needs an Ollama-compatible vision model output
contract before it can supply useful candidates. Even after that correction,
VLM results require independent deterministic evidence and consensus; they
cannot authorize actions or replace the existing analyzers. No live Phase 6B
integration is included.
