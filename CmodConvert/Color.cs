using static System.FormattableString;

namespace CmodConvert
{
    internal readonly struct Color
    {
        public float Red { get; init; }
        public float Green { get; init; }
        public float Blue { get; init; }

        public override string ToString() => Invariant($"{Red:R} {Green:R} {Blue:R}");
    }
}
