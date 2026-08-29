# RapidOcrNet.Benchmarks

Does routing PP-OCRv6 inference through
[`Microsoft.ML.OnnxRuntime.EP.WebGpu`](https://www.nuget.org/packages/Microsoft.ML.OnnxRuntime.EP.WebGpu)
make RapidOcrNet faster than the stock CPU execution provider?

Everything here runs the **PP-OCRv6 small** model set (`PP-OCRv6_det_small.onnx` +
`PP-OCRv6_rec_small.onnx` + the PP-OCRv5 classifier, which v6 has no replacement for) with
`RapidOcrOptions.PPOCRv6`. The two arms differ in exactly one thing: whether the WebGPU
plugin EP is appended to the `SessionOptions` that
`RapidOcr.InitModels(RapidOcrModelSet, SessionOptions)` receives. Graph optimization level
and thread counts come from `RapidOcr.GetDefaultSessionOptions` in both cases.

## Prerequisites

- The v6 models in `RapidOcrNet/models/v6/` (they are not in the NuGet package — see the
  root README for where to get them). `BenchmarkAssets.EnsureAvailable` fails with a clear
  message if they are missing.
- A GPU adapter the plugin EP can use. The package ships natives for `win-x64`,
  `win-arm64`, `linux-x64` and `osx-arm64` only; on Linux a system Vulkan loader
  (`libvulkan.so.1`) must be installed.

Model and image paths are resolved as absolute paths anchored on `RapidOcrNet.sln`, not via
`CopyToOutputDirectory` — BenchmarkDotNet runs each case from a *generated* project whose
output folder is not this project's, so relative content paths would not resolve there.

## Pre-flight

```
dotnet run -c Release -- verify
```

Prints the WebGPU adapter that was selected, runs each image through both providers, breaks
the time down by stage (detector / classifier / recognizer), and checks that the two
providers produce **identical text**. A speed-up that changes the output is not a speed-up.
Run this before spending time on measurements — it is the fastest way to find out that the
EP did not load. Exit codes: `0` all good, `1` the WebGPU EP is unavailable on this machine,
`2` the two providers disagreed on the text.

## Measuring

```
dotnet build -c Release
dotnet run -c Release --no-build -- --filter "*" --job Short --noOverwrite > bench.log 2>&1
```

Then read `BenchmarkDotNet.Artifacts/<timestamp>/*-report-github.md`.

`--job Short` (3 warmup + 3 target iterations) is enough to see the shape, because a single
operation here is 0.5–5 s rather than nanoseconds. Drop `--job Short` for the default job
when the numbers matter; the full suite is 14 cases and takes roughly half an hour.

Useful filters:

| Goal | Command |
|---|---|
| Steady-state inference only | `--filter "*OcrPipelineBenchmarks*"` |
| Session creation only | `--filter "*ModelInitBenchmarks*"` |
| Detector in isolation | `--filter "*OcrPipelineBenchmarks*" --anyCategories DetectorOnly` |
| Recognizer parallelism sweep | `--filter "*RecognizerParallelBenchmarks*"` |
| One image | `--filter "*img_11*"` |
| CPU arm only (no GPU on this box) | `--filter "*.Cpu*"` |

## What the three suites mean

**`OcrPipelineBenchmarks`** — steady-state inference, sessions already created in
`[GlobalSetup]`. Two categories, each with its own CPU baseline so the `Ratio` column
compares like with like:

- `FullPipeline` — `RapidOcr.Detect`, i.e. detection + angle classification + recognition.
  This is what a caller actually pays per image.
- `DetectorOnly` — `RapidOcr.DetectBoxes`. One large convolutional graph over the whole
  page, and the part most likely to benefit from GPU offload. Subtract it from
  `FullPipeline` to attribute the remainder to the classifier and recognizer.

The split matters because `TextRecognizer` runs **one crop per inference** rather than
batching them, so a text-dense page issues one small dispatch per line. Per-dispatch
overhead is much more visible on a GPU provider than on CPU, and the two categories are
what separate that effect from the detector's.

The three images are chosen for shape, not content:

| Image | Source size | Why |
|---|---|---|
| `en.jpg` | 709×132 | Detector-dominated. Source size is **not** detector cost: `RapidOcrOptions.PPOCRv6` resizes the *short* side up to `LimitSideLen` (736), so this strip reaches the detector at ~3956×736 — the heaviest detector input of the three, despite being the smallest file. |
| `img_11.jpg` | 1280×720 | Mid-size photo, few lines. |
| `2108.11480_1.png` | 1224×1584 | Dense document page, ~106 blocks, so recognizer-dominated. Short side already exceeds the limit, so the detector sees it at native size. |

**`ModelInitBenchmarks`** — the one-off cost of creating the three sessions. A GPU provider
compiles shaders for every kernel the first time it sees the graph, which the pipeline
benchmarks deliberately exclude. Read the two tables together: WebGPU is only worth enabling
once

```
(images per process) × (per-image saving) > (extra init cost)
```

A batch job over a thousand pages and a CLI that OCRs one screenshot land on opposite sides
of that inequality.

**`RecognizerParallelBenchmarks`** — sweeps `RapidOcrOptions.RecMaxDegreeOfParallelism` over
the dense page under both providers. Each `[Params]` value is its own BenchmarkDotNet logical
group, so there is no meaningful `Ratio` column — compare the absolute `Mean` down each
category.

## Results on one machine

Recorded 2026-08-29 so the suite has something to be compared against. **These are not a
prediction for your hardware** — re-run before deciding anything.

> BenchmarkDotNet v0.15.8, default job, .NET 10.0.11, Windows 11 26200.
> Intel Core i9-14900HX (24 physical / 32 logical cores) — a strong CPU baseline.
> `Microsoft.ML.OnnxRuntime` 1.29.0 + `Microsoft.ML.OnnxRuntime.EP.WebGpu` 0.3.0.
> Two separate runs, one per adapter.

### Steady-state inference (mean, lower is better)

| Category | Image | CPU EP | WebGPU / Intel UHD | WebGPU / RTX 4070 |
|---|---|---:|---:|---:|
| FullPipeline | `en.jpg` | 2,442 ms | **439 ms** (0.18×) | **441 ms** (0.18×) |
| FullPipeline | `img_11.jpg` | 1,426 ms | **177 ms** (0.13×) | **177 ms** (0.12×) |
| FullPipeline | `2108.11480_1.png` | 6,782 ms | **2,596 ms** (0.38×) | **2,647 ms** (0.39×) |
| DetectorOnly | `en.jpg` | 1,212 ms | **344 ms** (0.29×) | **348 ms** (0.29×) |
| DetectorOnly | `img_11.jpg` | 458 ms | **145 ms** (0.32×) | **144 ms** (0.35×) |
| DetectorOnly | `2108.11480_1.png` | 926 ms | **253 ms** (0.27×) | **253 ms** (0.33×) |

### Session creation

| | CPU EP | WebGPU / Intel UHD | WebGPU / RTX 4070 |
|---|---:|---:|---:|
| `InitModels` | 499 ms | 631 ms (1.26×) | 623 ms (1.22×) |

### Reading these

- **WebGPU wins clearly at steady state**: 2.6× on the dense page, 5.6× on `en.jpg`, 8× on
  `img_11.jpg`. Managed allocations are unchanged, as expected — the EP does not touch
  RapidOcrNet's own pre/post-processing.
- **The extra startup cost is ~125 ms**, against a per-image saving of 250 ms to 4.2 s.
  Break-even is the *first* image, so the usual "GPU only pays off for batch work" caveat
  does not bite at this model size.
- **The discrete RTX 4070 is no faster than the integrated Intel UHD** — every pair is
  within noise. So this workload is not GPU-compute-bound; what is left is per-dispatch
  overhead plus the CPU-side work still inside the measurement (SkiaSharp crops, tensor
  fills, DB post-processing). Buying a bigger GPU would not help; overlapping the recognizer
  calls does — see below.
- **The detector speeds up uniformly (~3×) while the full pipeline varies (2.6–8×).** The
  dense page gains least in relative terms because it is recognizer-dominated and
  `TextRecognizer` issues one dispatch per crop — 106 small dispatches rather than one large
  graph. `RecMaxDegreeOfParallelism` closes most of that gap; see the next section.
- The CPU arm drifts a little between runs (e.g. 926 ms vs 774 ms for the same detector
  case) — thermal and scheduling variance on a laptop. The effect sizes above are far larger
  than that drift.

### Recognizer parallelism (`RecMaxDegreeOfParallelism`)

`RecognizerParallelBenchmarks` addresses the weakest result above: the dense page, whose
recognizer issues one inference per crop and so leaves the execution provider idle through
each crop's Skia resize, normalization and CTC decode. Running those inferences concurrently
overlaps that work. Same machine, default job, `2108.11480_1.png` (~106 blocks):

| `RecMaxDegreeOfParallelism` | CPU EP | vs. 1 | WebGPU EP | vs. 1 |
|---:|---:|---:|---:|---:|
| 1 (default) | 6.202 s | — | 2.573 s | — |
| 2 | 4.847 s | 1.28× | 1.747 s | 1.47× |
| 4 | 4.132 s | 1.50× | 1.369 s | 1.88× |
| 8 | 3.737 s | 1.66× | **1.318 s** | **1.95×** |
| 16 | **3.703 s** | **1.67×** | 1.413 s | 1.82× |

Allocations are **identical at every value** (9.46 GB), because nothing about the work
changes — only when it runs.

Compounding with the provider choice, the dense page goes from 6.202 s (CPU, serial) to
1.318 s (WebGPU, 8-way) — **4.7× end to end**, against the 2.6× WebGPU managed on its own.

Both curves flatten and then turn: CPU gains nothing past 8, and WebGPU is worse at 16 than
at 8. ONNX Runtime already spreads each inference across its own `IntraOpNumThreads` pool and
concurrent runs share it, so past a point this only moves the queueing. **8 is the sweet spot
on this machine**; treat that as a starting point to measure from, not a constant.

The gain scales with crop count, so it is a dense-page optimization. An image with three text
lines has almost nothing to overlap and will show close to nothing.

### Parallelism does not change the output

Unlike batching crops into a single padded inference, concurrency leaves the model input
untouched: each crop is resized, normalized, run and decoded exactly as on the serial path.
Verified across 32 test images × 5 parallelism values × 3 model sets (v6 small, v6 tiny, v5
latin) — **480 comparisons, all byte-identical**, comparing text, per-character scores *and*
the word-box polygons derived from CTC column indices.

That is what makes this preferable to batching, which changes the recognizer input and with
it a few percent of the recognized lines.

`RecMaxDegreeOfParallelism` still defaults to 1 in every preset, because it spends threads
the caller has not offered — a server already running one OCR per request wants that
concurrency at its own level, not multiplied underneath it.

## Choosing the GPU

On a switchable-graphics machine the plugin EP exposes one adapter per GPU and ONNX Runtime
picks the first, which is commonly the **integrated** one. `verify` prints every candidate
and marks the one in use. Override it with a case-insensitive substring:

```
RAPIDOCR_BENCH_WEBGPU_ADAPTER=NVIDIA dotnet run -c Release --no-build -- --filter "*"
```

The variable is inherited by the processes BenchmarkDotNet spawns, so it applies to a whole
run. An unmatched substring is an error rather than a silent fallback — attributing one
adapter's numbers to another is the exact mistake the variable exists to prevent.

## Caveats

- Results are machine-specific — GPU, driver version and core count all move them. Re-run on
  the target hardware rather than trusting a number from someone else's box.
- Appending the WebGPU EP leaves the CPU provider registered behind it, so nodes WebGPU
  cannot handle still run on CPU (ONNX Runtime logs `Some nodes were not assigned to the
  preferred execution providers` for exactly this). That is the configuration a consumer
  would ship, and it is why a win is usually partial.
- `MemoryDiagnoser` measures *managed* allocations, which are dominated by RapidOcrNet's
  own pre/post-processing and are expected to be identical across providers. GPU-side
  memory is invisible to it.
