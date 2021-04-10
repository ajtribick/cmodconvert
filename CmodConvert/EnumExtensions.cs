using System;

namespace CmodConvert
{
    internal static class EnumExtensions
    {
        public static PrimitiveCategory Categorize(this PrimitiveType primitive) => primitive switch
        {
            PrimitiveType.TriList or PrimitiveType.TriStrip or PrimitiveType.TriFan => PrimitiveCategory.Triangle,
            PrimitiveType.LineList or PrimitiveType.LineStrip => PrimitiveCategory.Line,
            PrimitiveType.PointList or PrimitiveType.SpriteList => PrimitiveCategory.Point,
            _ => throw new ArgumentOutOfRangeException(nameof(primitive)),
        };

        public static string Command(this PrimitiveCategory category) => category switch
        {
            PrimitiveCategory.Triangle => "f",
            PrimitiveCategory.Line => "l",
            PrimitiveCategory.Point => "p",
            _ => throw new ArgumentOutOfRangeException(nameof(category)),
        };
    }
}
