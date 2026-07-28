# Kendte begrænsninger

- .NET build og self-test er ikke kørt i leverancemiljøet.
- Real-phone acceptance er ikke kørt.
- ROI-koordinaterne er prototypestandarder og skal kalibreres til den konkrete telefon og Pokémon GO-layout.
- Tærskler for motion, difference, similarity og skarphed er ikke kalibreret på real-phone evidence.
- CPU-observeren bruger sampled pixelmålinger. Den implementerer ikke optical flow.
- SSIM-målingen er en regional, sampled global SSIM-tilnærmelse, ikke en fuld multiscale SSIM-implementation.
- GPU-acceleration, NVDEC, CUDA og DirectML er ikke implementeret.
- Scrcpy/FFmpeg-begrænsninger fra Fase 2 gælder fortsat, herunder fast råvideoopløsning.
- Dynamisk opløsningsskift fejler lukket og understøttes ikke under samme decode-session.
- Profilerne er generiske. De genkender ikke Pokémon, CP, IV eller Appraisal-state.
- Gates frigiver ingen telefonhandling. Der findes ingen kobling til telefoninput.
- Repository-integration og ændring af eksisterende runtime-flow er ikke udført.
- `PeakQueueDepth` rapporteres som `null`, fordi gate-motoren ikke har en særskilt observationskø. Streamens bounded subscriber-drops rapporteres gennem framekilden.
- Real-phone evidence-PNG kan først verificeres efter build og faktisk streamingtest.
