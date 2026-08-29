// Apache-2.0 license

using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.EP.WebGpu;

namespace RapidOcrNet.Benchmarks;

/// <summary>Which ONNX Runtime execution provider a benchmark builds its sessions with.</summary>
internal enum ExecutionProviderKind
{
    /// <summary>Stock RapidOcrNet behaviour: the built-in CPU provider.</summary>
    Cpu,

    /// <summary>CPU provider with the WebGPU plugin EP appended in front of it.</summary>
    WebGpu
}

/// <summary>
/// Builds the <see cref="SessionOptions"/> the benchmarks hand to
/// <see cref="RapidOcr.InitModels(RapidOcrModelSet, SessionOptions)"/>.
/// </summary>
/// <remarks>
/// Both variants start from <see cref="RapidOcr.GetDefaultSessionOptions"/>, so graph
/// optimization level and thread counts are identical and the execution provider is the
/// only difference between the two arms of the comparison.
/// </remarks>
internal static class ExecutionProviders
{
    private const string WebGpuLibraryRegistrationName = "webgpu_ep";

    /// <summary>
    /// Case-insensitive substring matched against the adapter description to pick which GPU
    /// the WebGPU EP runs on, e.g. <c>set RAPIDOCR_BENCH_WEBGPU_ADAPTER=NVIDIA</c>. Unset,
    /// ONNX Runtime's own first choice is used - which on a switchable-graphics laptop is
    /// commonly the integrated GPU, so the variable is the difference between measuring the
    /// iGPU and the discrete card.
    /// </summary>
    public const string AdapterEnvironmentVariable = "RAPIDOCR_BENCH_WEBGPU_ADAPTER";

    private static readonly Lazy<OrtEpDevice> s_webGpuDevice = new(RegisterAndFindWebGpuDevice);

    /// <summary>
    /// True when the WebGPU plugin EP loaded and reported a usable adapter. Checked before
    /// running the WebGPU arm so an unsupported machine fails with an explanation instead
    /// of silently benchmarking the CPU provider twice.
    /// </summary>
    public static bool IsWebGpuAvailable(out string? error)
    {
        try
        {
            _ = s_webGpuDevice.Value;
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Every adapter the WebGPU plugin EP offers, in the order ONNX Runtime returns them.
    /// </summary>
    public static IReadOnlyList<OrtEpDevice> WebGpuDevices()
    {
        _ = s_webGpuDevice.Value; // forces the one-time library registration
        return WebGpuDevices(OrtEnv.Instance(), WebGpuEp.GetEpName());
    }

    /// <summary>Describes one adapter in a single line.</summary>
    public static string Describe(OrtEpDevice device)
    {
        OrtHardwareDevice hardware = device.HardwareDevice;

        string name = hardware.Metadata.Entries.TryGetValue("Description", out string? description)
            ? description
            : $"vendor 0x{hardware.VendorId:X4} device 0x{hardware.DeviceId:X4}";

        return $"{hardware.Type} '{name}' ({hardware.Vendor})";
    }

    /// <summary>
    /// Describes the adapter the plugin EP selected. Worth printing with any result: on a
    /// laptop with switchable graphics the EP may well have picked the integrated GPU, and
    /// a number measured there says nothing about the discrete one.
    /// </summary>
    public static string DescribeWebGpuDevice() => Describe(s_webGpuDevice.Value);

    /// <summary>
    /// Creates session options for <paramref name="kind"/>. The caller owns the returned
    /// instance and must dispose it once the sessions have been created - ONNX Runtime
    /// copies the settings into each session, so the options object is not needed
    /// afterwards.
    /// </summary>
    /// <param name="kind">Which execution provider to configure.</param>
    /// <param name="numThread">
    /// Intra/inter-op thread count passed through to <see cref="RapidOcr.GetDefaultSessionOptions"/>.
    /// 0 leaves ONNX Runtime's default (one thread per logical core).
    /// </param>
    public static SessionOptions Create(ExecutionProviderKind kind, int numThread = 0)
    {
        SessionOptions options = RapidOcr.GetDefaultSessionOptions(numThread);

        /* From NuGet package
        // Note: Error handling is omitted for brevity, except for the device-discovery check below.

        using Microsoft.ML.OnnxRuntime;
        using Microsoft.ML.OnnxRuntime.EP.WebGpu;

        // Register the WebGPU EP plugin library
        var env = OrtEnv.Instance();
        env.RegisterExecutionProviderLibrary("webgpu_ep", WebGpuEp.GetLibraryPath());

        // Find the WebGPU EP device
        OrtEpDevice? webGpuDevice = null;
        foreach (var d in env.GetEpDevices())
        {
            if (d.EpName == WebGpuEp.GetEpName())
            {
                webGpuDevice = d;
                break;
            }
        }
        if (webGpuDevice is null)
        {
            throw new InvalidOperationException("No WebGPU device found.");
        }

        // Create a session with the WebGPU EP
        using var sessionOptions = new SessionOptions();
        sessionOptions.AppendExecutionProvider(env, new[] { webGpuDevice }, new Dictionary<string, string>());

        using var session = new InferenceSession("model.onnx", sessionOptions);
         */

        if (kind == ExecutionProviderKind.WebGpu)
        {
            try
            {
                // Appending leaves the CPU provider registered behind WebGPU, so any node
                // the plugin cannot handle still runs - which is exactly the configuration
                // a consumer would ship, and why the win can be partial.
                options.AppendExecutionProvider(
                    OrtEnv.Instance(),
                    new[] { s_webGpuDevice.Value },
                    new Dictionary<string, string>());
            }
            catch
            {
                options.Dispose();
                throw;
            }
        }

        return options;
    }

    private static OrtEpDevice RegisterAndFindWebGpuDevice()
    {
        OrtEnv env = OrtEnv.Instance();

        // Registration is per-process and throws if the same name is registered twice;
        // Lazy<T> guarantees this runs once. Each benchmark case gets a fresh process,
        // so there is no state carried between cases either.
        env.RegisterExecutionProviderLibrary(WebGpuLibraryRegistrationName, WebGpuEp.GetLibraryPath());

        string epName = WebGpuEp.GetEpName();
        string? wanted = Environment.GetEnvironmentVariable(AdapterEnvironmentVariable);

        OrtEpDevice? first = null;
        foreach (OrtEpDevice device in env.GetEpDevices())
        {
            if (device.EpName != epName)
            {
                continue;
            }

            first ??= device;

            if (string.IsNullOrEmpty(wanted) || Describe(device).Contains(wanted, StringComparison.OrdinalIgnoreCase))
            {
                return device;
            }
        }

        if (first is not null)
        {
            // Silently falling back to a different GPU would attribute one adapter's numbers
            // to another, which is the whole point of the variable.
            throw new InvalidOperationException(
                $"No WebGPU adapter matched {AdapterEnvironmentVariable}='{wanted}'. Available: " +
                string.Join("; ", WebGpuDevices(env, epName).Select(Describe)));
        }

        throw new InvalidOperationException(
            $"The WebGPU plugin EP registered but exposed no '{epName}' device. On Windows this usually " +
            "means an outdated GPU driver; on Linux, a missing Vulkan loader (libvulkan.so.1).");
    }

    private static List<OrtEpDevice> WebGpuDevices(OrtEnv env, string epName)
    {
        var devices = new List<OrtEpDevice>();
        foreach (OrtEpDevice device in env.GetEpDevices())
        {
            if (device.EpName == epName)
            {
                devices.Add(device);
            }
        }

        return devices;
    }
}
