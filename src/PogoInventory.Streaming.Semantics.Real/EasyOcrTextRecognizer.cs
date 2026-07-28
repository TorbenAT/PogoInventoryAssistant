using PogoInventory.HeaderText;
using PogoInventory.Vision.Imaging;
using PogoInventory.Vision.Models;

namespace PogoInventory.Streaming.Semantics.Real;

public sealed class EasyOcrTextRecognizer : ITextRecognizer
{
    private readonly EasyOcrJsonLinesWorker _worker;

    public EasyOcrTextRecognizer(EasyOcrJsonLinesWorker worker) => _worker = worker ?? throw new ArgumentNullException(nameof(worker));

    public async Task<IReadOnlyList<RecognizedTextLine>> RecognizeAsync(byte[] framePng, NormalizedRegion roi, CancellationToken cancellationToken = default, HeaderRegionKind regionKind = HeaderRegionKind.Name)
    {
        var frame = PngDecoder.Decode(framePng);
        var pixels = HeaderOcrCropScaler.CropAndUpscale(frame, roi.ToPixels(frame.Width, frame.Height), HeaderOcrGeometry.ComputeUpscale(roi.ToPixels(frame.Width, frame.Height).Width, roi.ToPixels(frame.Width, frame.Height).Height));
        var result = await _worker.RecognizeAsync(PngEncoder.Encode(pixels), cancellationToken).ConfigureAwait(false);
        return result.Lines.Select(line => new RecognizedTextLine { Text = line.Text, Confidence = line.Confidence, NormalizedBounds = roi }).ToArray();
    }
}
