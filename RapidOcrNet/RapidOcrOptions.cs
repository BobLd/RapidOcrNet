// Apache-2.0 license

namespace RapidOcrNet
{
    public sealed record RapidOcrOptions
    {
        public static readonly RapidOcrOptions Default = new RapidOcrOptions()
        {
            Padding = 50,
            ImgResize = 1024,
            BoxScoreThresh = 0.5f,
            BoxThresh = 0.3f,
            UnClipRatio = 1.6f,
            DoAngle = true,
            MostAngle = false
        };

        public int Padding { get; init; }
        public int ImgResize { get; init; }
        public float BoxScoreThresh { get; init; }
        public float BoxThresh { get; init; }
        public float UnClipRatio { get; init; }
        public bool DoAngle { get; init; }
        public bool MostAngle { get; init; }
    }
}
