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

namespace CmodConvert.Wavefront
{
    public readonly struct VertexInfo
    {
        public int Position { get; init; }
        public int TexCoord { get; init; }
        public int Normal { get; init; }

        public override string ToString()
        {
            if (TexCoord < 0)
            {
                return Normal < 0 ? Position.ToString() : $"{Position}//{Normal}";
            }

            return Normal < 0 ? $"{Position}/{TexCoord}" : $"{Position}/{TexCoord}/{Normal}";
        }
    }
}
