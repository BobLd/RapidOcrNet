# RapidOcrNet

[![Windows](https://github.com/BobLd/RapidOcrNet/actions/workflows/tests-windows.yml/badge.svg)](https://github.com/BobLd/RapidOcrNet/actions/workflows/tests-windows.yml)
[![Linux](https://github.com/BobLd/RapidOcrNet/actions/workflows/tests-linux.yml/badge.svg)](https://github.com/BobLd/RapidOcrNet/actions/workflows/tests-linux.yml)
[![macOS](https://github.com/BobLd/RapidOcrNet/actions/workflows/tests-macos.yml/badge.svg)](https://github.com/BobLd/RapidOcrNet/actions/workflows/tests-macos.yml)

Cross-platform OCR processing library using PaddleOCR ONNX models, and based on original code from RapidAI's [RapidOCR](https://github.com/RapidAI/RapidOCR).

Available as NuGet package: https://www.nuget.org/packages/RapidOcrNet/

The code was optimised to remove dependencies on `System.Drawing` and `OpenCV`. Image processing is now done only using `SkiaSharp` and `PContourNet`. The library targets **net8.0** and **net10.0**, is **AOT-compatible**, and ships the **PP-OCRv5** models out of the box.
v4 and v3 are also supported — see [#3](https://github.com/BobLd/RapidOcrNet/issues/3). v6 models are also supported, see [Choosing models (PP-OCRv5 vs PP-OCRv6)](#choosing-models-pp-ocrv5-vs-pp-ocrv6)

## How the pipeline works
A single `Detect()` call runs three ONNX models in sequence:

1. **Detector (DBNet)** — finds polygons (text boxes) in the input image.
2. **Classifier (cls)** — predicts whether each crop is upside-down (0° vs 180°) and rotates it if so.
3. **Recognizer (CRNN)** — reads the characters in each upright crop and returns text + per-char confidences.

You get back an `OcrResult` containing one `TextBlock` per detected line, plus optional word-level / character-level polygons.

## Installing the models
All ONNX models can be downloaded from: https://github.com/RapidAI/RapidOCR/blob/main/python/rapidocr/default_models.yaml

You need 4 files for the pipeline to work. The defaults bundled with the NuGet package are the PP-OCRv5 **latin** set:
- Detection: `ch_PP-OCRv5_mobile_det.onnx`
- Classification: `ch_PP-LCNet_x0_25_textline_ori_cls_mobile.onnx`
- Recognition: `latin_PP-OCRv5_rec_mobile_infer.onnx`
- Model dictionary: `ppocrv5_latin_dict.txt`

When using the NuGet package, these are copied to `models/v5/` next to your binary automatically. If you want a different language (Chinese, Japanese, Korean, etc.), download the matching `*_rec_mobile_infer.onnx` + `*_dict.txt` pair and pass their paths to `InitModels` (see [Using custom models / other languages](#using-custom-models--other-languages)).

> **PP-OCRv6 is also supported.** v6 ships a single multilingual recognizer in three sizes (tiny / small / medium) and reuses the v5 classifier. Because the v6 files are large (~176 MB for all three sizes), they are **not** bundled in the NuGet — you download them yourself and select them via a preset. See [Choosing models (PP-OCRv5 vs PP-OCRv6)](#choosing-models-pp-ocrv5-vs-pp-ocrv6).

## Quick start
The shortest path to recognized text — load defaults, run, print:
```csharp
using RapidOcrNet;
using SkiaSharp;

using var ocr = new RapidOcr();
ocr.InitModels();                                  // loads bundled PP-OCRv5 latin models

OcrResult result = ocr.Detect("image.png", RapidOcrOptions.Default);

Console.WriteLine(result.StrRes);                  // full recognized text, one line per block
foreach (var block in result.TextBlocks)
{
    Console.WriteLine($"{block.Text}  (avg score: {block.CharScores!.Average():F2})");
}
```

`Detect` has two overloads — pass a file path (`string`) for convenience, or an `SKBitmap` if you already have the image decoded.

## Awaiting, cancelling and reporting progress
`DetectAsync` and `DetectBoxesAsync` mirror their synchronous counterparts, and both overloads (`string` path or `SKBitmap`) are available:
```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
var progress = new Progress<(int Completed, int Total)>(
    p => Console.WriteLine($"{p.Completed}/{p.Total} lines"));

OcrResult result = await ocr.DetectAsync("image.png", RapidOcrOptions.Default, progress, cts.Token);
```

**Cancellation** is observed at every stage boundary, between crops, and *inside each ONNX run* — every inference carries a `RunOptions` whose terminate flag the runtime checks once per kernel. So a cancelled page stops where it stands, including part-way through detection, which is a single run over the whole image and on a large scan the longest stretch with no boundary to wait for. A cancelled call throws `OperationCanceledException` and disposes the crops it had allocated.

**Progress** is reported during recognition as `(lines recognised, lines detected)`. Nothing is reported before detection finishes, because the line count is not known until then.

> **What `async` buys you here.** OCR is CPU-bound end to end — ONNX inference, Skia resampling, contour finding, CTC decoding — so `DetectAsync` moves the work to the thread pool rather than freeing a thread. Await it to keep a UI thread responsive; it will not raise throughput on a server. ONNX Runtime's `InferenceSession.RunAsync` would not change that: it schedules the same blocking `Run` onto the intra-op thread pool, contending with the parallelism of the inference itself, and its C# binding additionally requires every output preallocated at exactly the right shape. It is deliberately not used.

## Drawing the boxes back onto the image
Each `TextBlock` exposes the four corners of its detected polygon in `BoxPoints` (in clockwise order, in source-image pixel coordinates).
```csharp
using var bmp = SKBitmap.Decode("image.png");
OcrResult result = ocr.Detect(bmp, RapidOcrOptions.Default);

using var canvas = new SKCanvas(bmp);
using var paint  = new SKPaint { Color = SKColors.Red, IsStroke = true };

foreach (var block in result.TextBlocks)
{
    var p = block.BoxPoints;
    canvas.DrawLine(p[0], p[1], paint);
    canvas.DrawLine(p[1], p[2], paint);
    canvas.DrawLine(p[2], p[3], paint);
    canvas.DrawLine(p[3], p[0], paint);
}

using var fs = File.Create("image_ocr.png");
bmp.Encode(fs, SKEncodedImageFormat.Png, 100);
```

## Word-level and character-level boxes
By default the recognizer only returns one polygon per **line**. To also get one polygon **per word** (or per character), enable `ReturnWordBox`:
```csharp
var options = RapidOcrOptions.Default with { ReturnWordBox = true };
OcrResult result = ocr.Detect("image.png", options);

foreach (var block in result.TextBlocks)
{
    Console.WriteLine(block.Text);
    foreach (var word in block.WordResults!)      // null when ReturnWordBox = false
    {
        Console.WriteLine($"   {word.Text}  score={word.Score}");
    }
}
```

Behavior:
- **Latin/numeric lines:** one `WordBox` per whitespace-separated word.
- **Lines containing CJK characters:** one `WordBox` per character (whitespace is meaningless inside CJK).
- Setting `ReturnSingleCharBox = true` forces Latin/numeric lines to per-character granularity as well.

The polygons are reconstructed from CTC time-column positions, so they align tightly with the actual glyphs — useful for layout extraction, highlighting, or rebuilding searchable PDFs.

## Choosing an options preset
There are two ready-made presets on `RapidOcrOptions`:

| Preset | Pre-padding | Resize strategy | Best for |
|---|---|---|---|
| `RapidOcrOptions.Default` | 50 px white border | `ImgResize = 1024` (legacy max-side cap) | The bundled PP-OCRv5 model — best accuracy on most photos and screenshots. |
| `RapidOcrOptions.PythonCompat` | 0 (no border) | `LimitSideLen = 736` (short-side adaptive, like Python `rapidocr`) | Reproducing results from the Python `rapidocr` library or feeding very wide / very tall inputs. |

Use a `with`-expression to tweak any individual option:
```csharp
var options = RapidOcrOptions.Default with
{
    ReturnWordBox = true,
    DoAngle = false,       // skip the 180° classifier if you know text is upright
    TextScore = 0.7f,      // drop low-confidence lines more aggressively
};
```

### Key options at a glance
| Option | What it does |
|---|---|
| `Padding` | Extra white border added before detection. Helps when text touches the image edge. `Default` uses 50; `PythonCompat` uses 0. |
| `ImgResize` | If `> 0`, caps the longer side at this size. Set to `0` to use Python-style short-side adaptive resize via `LimitSideLen`. |
| `LimitSideLen` | When `ImgResize == 0`, the detector upscales the short side up to this value (default 736). |
| `MaxSideLen` / `MinSideLen` | Hard bounds on image size before detection. |
| `WidthHeightRatio` / `MinHeight` | Adds a **vertical letterbox** to very wide / very short inputs so the detector sees enough vertical context. `-1` disables. |
| `TextScore` | Filter threshold on the **average per-character score** of each line. Blocks below this are dropped. |
| `ClsThresh` | Confidence the classifier needs before it actually applies a 180° flip. Default 0.9 — anything lower would risk inverting clean upright text and producing garbage. |
| `ClsPreserveAspectRatio` | When `true`, crops are aspect-preserved + midgray-padded before classification (matches Python rapidocr). When `false`, legacy stretch-to-192×48. |
| `BoxScoreThresh` / `BoxThresh` / `UnClipRatio` | DBNet hyperparameters — keep at defaults unless you're tuning detection. |
| `DoAngle` | Run the 180° classifier at all. Disable if you know your text is upright. |
| `MostAngle` | Decide each crop's angle from the **majority vote** across all crops, instead of per-crop. |
| `ReturnWordBox` / `ReturnSingleCharBox` | See the word/char-box section above. |

## GPU acceleration and custom session options
The ONNX session is configurable. Build a `SessionOptions` (see the [ONNX Runtime API docs](https://onnxruntime.ai/docs/api/csharp/api/Microsoft.ML.OnnxRuntime.SessionOptions.html)) and pass it to `InitModels`:
```csharp
using var ocr = new RapidOcr();

using var sessionOptions = RapidOcr.GetDefaultSessionOptions();   // sensible defaults: ORT_ENABLE_EXTENDED
try   { sessionOptions.AppendExecutionProvider_CUDA(); }          // NVIDIA GPU
catch { sessionOptions.AppendExecutionProvider_CPU();  }          // fallback

ocr.InitModels(sessionOptions);
```

`GetDefaultSessionOptions(int numThread = 0)` also takes an optional thread count if you'd rather keep CPU execution but pin Inter/Intra-op parallelism.

Cancellation is independent of all of this. The terminate flag is read by whichever thread is walking the graph — under the default sequential execution mode, the calling thread itself — so `InitModels(numThread: 1)` cancels exactly like a fully parallel session. On a non-CPU execution provider the flag stops the graph walk rather than recalling work already dispatched to the device queue, so cancellation is prompt but not instant.

## Choosing models (PP-OCRv5 vs PP-OCRv6)
`RapidOcrModelSet` bundles a complete set of models (detector + classifier + recognizer + dictionary) plus the detector's normalization, so you can switch model families with a single argument. Pass a preset to `InitModels`:

```csharp
using var ocr = new RapidOcr();

ocr.InitModels(RapidOcrModelSet.PPOCRv5Latin);   // same as the parameterless InitModels()
// or:
ocr.InitModels(RapidOcrModelSet.PPOCRv6Small);   // PP-OCRv6, balanced size/accuracy
```

Available presets:

| Preset | Models | Notes |
|---|---|---|
| `RapidOcrModelSet.PPOCRv5Latin` | bundled PP-OCRv5 latin set | The library default (`InitModels()` with no args uses this). |
| `RapidOcrModelSet.PPOCRv6Tiny` | PP-OCRv6 tiny | Smallest / fastest, lower accuracy (~6 MB). |
| `RapidOcrModelSet.PPOCRv6Small` | PP-OCRv6 small | Balanced; matches the Python `rapidocr` v6 default (~31 MB). |
| `RapidOcrModelSet.PPOCRv6Medium` | PP-OCRv6 medium | Largest / most accurate, slowest (~138 MB). |
| `RapidOcrModelSet.PPOCRv6` | alias for `PPOCRv6Small` | Convenience default for v6. |

All v6 presets are **multilingual** (Latin + CJK and more) and reuse the v5 classifier model — PP-OCRv6 ships none of its own.

### Getting the v6 model files
The v6 files are not in the NuGet. Download them from the [RapidOCR default models list](https://github.com/RapidAI/RapidOCR/blob/main/python/rapidocr/default_models.yaml) and place them under `models/v6/` next to your binary, using these exact names (this is what the presets resolve to):

| Size | Detector | Recognizer | Dictionary |
|---|---|---|---|
| tiny | `PP-OCRv6_det_tiny.onnx` | `PP-OCRv6_rec_tiny.onnx` | `ppocrv6_tiny_dict.txt` |
| small | `PP-OCRv6_det_small.onnx` | `PP-OCRv6_rec_small.onnx` | `ppocrv6_small_dict.txt` |
| medium | `PP-OCRv6_det_medium.onnx` | `PP-OCRv6_rec_medium.onnx` | `ppocrv6_medium_dict.txt` |

(The v5 classifier `ch_PP-LCNet_x0_25_textline_ori_cls_mobile.onnx` stays in `models/v5/`, where the NuGet already puts it.) If you prefer different paths, build a `RapidOcrModelSet` yourself:

```csharp
var v6 = RapidOcrModelSet.PPOCRv6Small with
{
    DetModelPath = "/my/models/PP-OCRv6_det_small.onnx",
    RecModelPath = "/my/models/PP-OCRv6_rec_small.onnx",
    KeysPath     = "/my/models/ppocrv6_small_dict.txt",
};
ocr.InitModels(v6);
```

### Recommended options for v6
Pair v6 with the **`RapidOcrOptions.PPOCRv6`** preset — it matches the detector preprocessing these models were exported for (short-side adaptive resize to 736, no border) and gives the best results:

```csharp
ocr.InitModels(RapidOcrModelSet.PPOCRv6Small);
OcrResult result = ocr.Detect("image.png", RapidOcrOptions.PPOCRv6);
```

> ⚠️ **Do not use `RapidOcrOptions.Default` with v6.** `Default`'s 1024 long-side cap and 50&nbsp;px white border are tuned for the bundled v5 model; they starve the v6 detector of resolution and produce missed or garbled boxes on small images. `PPOCRv6` is a named alias of `PythonCompat` (the Python-`rapidocr`-faithful preprocessing). v6's strength is **multilingual** coverage (Latin + CJK and more in one model) — on Latin-only inputs it performs similarly to the Latin-specialised v5 model.

`InitModels(RapidOcrModelSet, …)` also has an overload taking a custom `SessionOptions` for GPU / threading, exactly like the other `InitModels` overloads.

## Using custom models / other languages
Pass explicit paths if you've downloaded different ONNX files (e.g. Chinese, Japanese, or a heavier `_server_` recognizer):
```csharp
using var ocr = new RapidOcr();

ocr.InitModels(
    detPath:  "models/v5/ch_PP-OCRv5_det_server.onnx",
    clsPath:  "models/v5/ch_PP-LCNet_x0_25_textline_ori_cls_mobile.onnx",
    recPath:  "models/v5/korean_PP-OCRv5_rec_mobile.onnx",
    keysPath: "models/v5/ppocrv5_korean_dict.txt");

// Or, with custom session options:
// ocr.InitModels(detPath, clsPath, recPath, keysPath, sessionOptions);
```

The recognizer's character set is defined by the `*_dict.txt` file — it **must** match the `*_rec_*.onnx` you load, otherwise the output will be gibberish.

## Notice
Based on source code originally developed in the RapidOCR project (Apache-2.0 license).
- https://github.com/RapidAI/RapidOCR

Uses parts of source code originally developed in the PdfPig project (Apache-2.0 license).
- https://github.com/UglyToad/PdfPig

The dependency on OpenCV was removed thanks to the PContour library and its C# port.
- https://github.com/LingDong-/PContour
- https://github.com/BobLd/PContourNet

The models made available are from the PaddleOCR project (Apache-2.0 license) and were downloaded from https://github.com/RapidAI/RapidOCR/blob/main/python/rapidocr/default_models.yaml
