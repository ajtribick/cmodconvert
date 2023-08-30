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

namespace CmodConvert;

public enum AttributeType : short
{
    Position = 0,
    Color0 = 1,
    Color1 = 2,
    Normal = 3,
    Tangent = 4,
    Texture0 = 5,
    Texture1 = 6,
    Texture2 = 7,
    Texture3 = 8,
    PointSize = 9,
    NextPosition = 10,
    ScaleFactor = 11,
}
