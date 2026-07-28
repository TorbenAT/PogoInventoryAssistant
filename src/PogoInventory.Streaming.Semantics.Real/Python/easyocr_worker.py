import base64
import io
import json
import sys
import time

sys.path.append(r"C:\Data\PokemonGo-Tools\python\python-3.13.14-embed\site-packages")
import easyocr
import numpy as np
from PIL import Image

reader = easyocr.Reader(["en"], gpu=True, verbose=False)

for line in sys.stdin:
    started = time.perf_counter()
    try:
        request = json.loads(line)
        image = Image.open(io.BytesIO(base64.b64decode(request["pngBase64"]))).convert("RGB")
        array = np.asarray(image)
        results = reader.readtext(array, detail=1, paragraph=False, batch_size=1)
        lines = []
        for bounds, text, confidence in results:
            lines.append({"text": str(text).strip(), "confidence": float(confidence), "bounds": bounds})
        response = {"id": request.get("id"), "ok": True, "lines": lines,
                    "latencyMs": (time.perf_counter() - started) * 1000.0}
    except Exception as error:
        response = {"id": None, "ok": False, "error": type(error).__name__ + ": " + str(error),
                    "latencyMs": (time.perf_counter() - started) * 1000.0}
    sys.stdout.write(json.dumps(response, separators=(",", ":")) + "\n")
    sys.stdout.flush()
