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
| Recognizer batch sweep | `--filter "*RecognizerBatchBenchmarks*"` |
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

The split matters because `TextRecognizer` runs **one crop per inference** by default, so a
text-dense page issues one small dispatch per line. Per-dispatch overhead is much more
visible on a GPU provider than on CPU, and the two categories are what separate that effect
from the detector's. (`RecognizerBatchBenchmarks` below tests whether batching those crops
helps. It does not — but that had to be measured rather than assumed.)

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

**`RecognizerBatchBenchmarks`** — sweeps `RapidOcrOptions.RecBatchSize` over the dense page
under both providers, to price the one optimization the pipeline results pointed at. Each
`[Params]` value is its own BenchmarkDotNet logical group, so there is no meaningful `Ratio`
column here — compare the absolute `Mean` down each category.

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
  fills, DB post-processing). Buying a bigger GPU would not help; batching the recognizer
  might.
- **The detector speeds up uniformly (~3×) while the full pipeline varies (2.6–8×).** The
  dense page gains least in relative terms because it is recognizer-dominated and
  `TextRecognizer` issues one dispatch per crop — 106 small dispatches rather than one large
  graph. Batching those crops looked like the obvious fix; it was measured and it is not — see
  below.
- The CPU arm drifts a little between runs (e.g. 926 ms vs 774 ms for the same detector
  case) — thermal and scheduling variance on a laptop. The effect sizes above are far larger
  than that drift.

### Recognizer batching (`RecBatchSize`) — measured, and it does not pay off

`RecognizerBatchBenchmarks` tests the hypothesis the pipeline results suggested: that the
dense page gained least from WebGPU because `TextRecognizer` issued one inference per crop,
and that batching those crops would recover it. **It does not.** Same machine, default job,
`2108.11480_1.png` (~106 blocks):

| `RecBatchSize` | CPU EP | vs. 1 | WebGPU EP | vs. 1 | Allocated |
|---:|---:|---:|---:|---:|---:|
| 1 (default) | 6.765 s | — | 2.619 s | — | 9.46 GB |
| 4 | 9.592 s | **+42%** | 2.511 s | −4% | 9.63 GB |
| 8 | 7.776 s | **+15%** | 2.497 s | −5% | 9.72 GB |
| 16 | 5.920 s | −12% | 2.477 s | −5% | 9.91 GB |

The reason is that batching does not only save dispatches — it *adds work*. Crops are
right-padded to a common width, and those padding columns are real convolutions the tight-fit
path never performs. On CPU, where per-dispatch overhead is small next to compute, the added
work dominates and batching is a net loss at 4 and 8; the CPU curve's non-monotonic shape is
reproducible across three separate runs, not noise. On WebGPU the two roughly cancel, leaving
about 5%.

A first attempt blamed the Python-compatible 320px minimum batch width for the wasted compute.
Removing it changed nothing measurable — on a page of text the lines already exceed a 6.67
width/height ratio, so that floor almost never binds. The floor was kept.

### Recognizer batching also changes the output

Batching is not output-neutral, which is the more important finding. Comparing `Detect` text
block-for-block against the unbatched path over 15 test images:

| Model | batch 2 | batch 6 | batch 16 |
|---|---:|---:|---:|
| PP-OCRv6 small | 1.8% of blocks differ | 2.4% | 2.4% |
| PP-OCRv6 tiny | 7.6% | 12.9% | 14.8% |
| PP-OCRv5 latin | 1.8% | 2.9% | 2.9% |

The differences go both ways — `1 FO0D` → `1 FOOD` and `allsemantic` → `all semantic` are
batching getting it *right*, while `https://doi.org` → `https:/doi.org` and `2 DENSE RETRIEVAL`
→ `2DENSE RETRIEVAL` are it getting it wrong. It can also change **how many blocks a page
returns**: on `TIKA-1552-0_3.png` the unbatched path yields 8 blocks and batching yields 7,
because the shifted per-character confidences move blocks across the `TextScore` threshold.
With `TextScore = 0` the same image gives 8 unbatched and 10 batched — batching reads text in
two crops the tight-fit path returns blank for, and loses confidence on three others.

**Conclusion:** `RecBatchSize` stays at 1 in every preset. It is implemented, tested and
available for callers who measure a win on their own images and hardware, but it is not a
free speed-up and should not be enabled by default.

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
