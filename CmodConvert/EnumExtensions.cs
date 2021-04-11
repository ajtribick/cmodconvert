/* CmodConvert - converts Celestia CMOD files to Wavefront OBJ/MTL format.
 * Copyright (C) 2021  Andrew Tribick
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation; either version 2
 * of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
 */

using System;

namespace CmodConvert
{
    public static class EnumExtensions
    {
        public static PrimitiveCategory Categorize(this PrimitiveType primitive) => primitive switch
        {
            PrimitiveType.TriList or PrimitiveType.TriStrip or PrimitiveType.TriFan => PrimitiveCategory.Triangle,
            PrimitiveType.LineList or PrimitiveType.LineStrip => PrimitiveCategory.Line,
            PrimitiveType.PointList or PrimitiveType.SpriteList => PrimitiveCategory.Point,
            _ => throw new ArgumentOutOfRangeException(nameof(primitive)),
        };

        internal static string Command(this PrimitiveCategory category) => category switch
        {
            PrimitiveCategory.Triangle => "f",
            PrimitiveCategory.Line => "l",
            PrimitiveCategory.Point => "p",
            _ => throw new ArgumentOutOfRangeException(nameof(category)),
        };
    }
}
