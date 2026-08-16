namespace RapidOcrNet.Tests;

/// <summary>
/// Disposing an instance whose models were never loaded. Loading is separate from construction,
/// so this is reachable whenever a load fails — and a <c>using</c> that throws on the way out
/// replaces the real error (a missing model file) with a NullReferenceException.
///
/// <para>None of these need a model on disk, so they run everywhere.</para>
/// </summary>
public class DisposeTest
{
    [Fact]
    public void TextRecognizerDisposesBeforeInit()
    {
        var recognizer = new TextRecognizer();
        recognizer.Dispose();
    }

    [Fact]
    public void TextClassifierDisposesBeforeInit()
    {
        var classifier = new TextClassifier();
        classifier.Dispose();
    }

    [Fact]
    public void TextDetectorDisposesBeforeInit()
    {
        var detector = new TextDetector();
        detector.Dispose();
    }

    [Fact]
    public void RapidOcrDisposesBeforeInit()
    {
        // The composite case, and the likeliest one to be hit: RapidOcr builds all three stages
        // in its field initializers, so a caller that constructs it and never gets as far as
        // InitModels still owns something disposable.
        var ocr = new RapidOcr();
        ocr.Dispose();
    }

    [Fact]
    public void FailedInitLeavesTheOriginalErrorIntact()
    {
        // What the fix is actually for. Before it, the FileNotFoundException below was reported
        // correctly but the `using` disposal then threw NullReferenceException over the top of
        // it, so callers saw a null-reference error instead of "your model path is wrong".
        var ocr = new RapidOcr();
        try
        {
            Assert.Throws<FileNotFoundException>(() =>
                ocr.InitModels("no-such-det.onnx", "no-such-cls.onnx", "no-such-rec.onnx",
                    "no-such-keys.txt"));
        }
        finally
        {
            ocr.Dispose();
        }
    }
}
