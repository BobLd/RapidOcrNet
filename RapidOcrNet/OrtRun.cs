using Microsoft.ML.OnnxRuntime;

namespace RapidOcrNet;

/// <summary>
/// The single place that runs an <see cref="InferenceSession"/> against a
/// <see cref="CancellationToken"/>, so the <see cref="RunOptions"/> lifetime is owned in one
/// spot rather than repeated in each of the three stages.
/// </summary>
internal static class OrtRun
{
    /// <summary>
    /// Runs <paramref name="session"/> and abandons it if <paramref name="cancellationToken"/>
    /// is cancelled, including part-way through the inference itself.
    /// </summary>
    /// <remarks>
    /// ONNX Runtime reads <see cref="RunOptions.Terminate"/> once per kernel as it walks the
    /// execution plan, so setting it stops an inference that is already running instead of
    /// waiting for it to finish. That is what makes a single-run stage such as the detector
    /// interruptible at all: the caller no longer has to wait out the whole page.
    /// <para>
    /// Terminate is sticky for the lifetime of the <see cref="RunOptions"/> it is set on, so each
    /// call gets its own. The registration is disposed before the options are — the two
    /// <c>using</c> declarations are ordered for it — and
    /// <see cref="CancellationTokenRegistration.Dispose()"/> waits out a callback already in
    /// flight, so the native handle cannot be freed while the callback is writing to it.
    /// </para>
    /// <para>
    /// This works whatever the session's thread configuration: the flag is read by whichever
    /// thread is walking the graph, and under the default sequential execution mode that is the
    /// calling thread itself. A single-threaded session
    /// (<c>InitModels(numThread: 1)</c>) cancels exactly like a parallel one — which is the
    /// reason cancellation is built on Terminate rather than on
    /// <c>InferenceSession.RunAsync</c>, since that refuses any session whose intra-op pool has
    /// fewer than two threads and would have dropped cancellation there without a word.
    /// </para>
    /// <para>
    /// The flag is checked as the executor moves between nodes, so on a non-CPU execution
    /// provider it stops the graph walk rather than recalling work already dispatched to the
    /// device queue.
    /// </para>
    /// </remarks>
    public static IDisposableReadOnlyCollection<DisposableNamedOnnxValue> Run(
        InferenceSession session,
        IReadOnlyCollection<NamedOnnxValue> inputs,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!cancellationToken.CanBeCanceled)
        {
            // Nothing can ever set Terminate, so skip the RunOptions and its registration
            // entirely and leave the default path exactly as it was.
            return session.Run(inputs);
        }

        using var runOptions = new RunOptions();
        using var registration = cancellationToken.Register(
            static state => ((RunOptions)state!).Terminate = true, runOptions);

        try
        {
            // Equivalent to Run(inputs): that overload forwards the session's own output names
            // and a built-in RunOptions, so passing them explicitly changes nothing but the
            // cancellability.
            return session.Run(inputs, session.OutputNames, runOptions);
        }
        catch (OnnxRuntimeException orEx) when (cancellationToken.IsCancellationRequested)
        {
            // A terminated run fails with ORT_FAIL ("Exiting due to terminate flag being set to
            // true"), which is a cancellation wearing the wrong type.
            throw new OperationCanceledException(orEx.Message, orEx, cancellationToken);
        }
    }
}
