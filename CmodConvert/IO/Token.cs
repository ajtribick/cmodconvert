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

namespace CmodConvert.IO
{
    internal enum Token : short
    {
        Material = 1001,
        EndMaterial = 1002,
        Diffuse = 1003,
        Specular = 1004,
        SpecularPower = 1005,
        Opacity = 1006,
        Texture = 1007,
        Mesh = 1009,
        EndMesh = 1010,
        VertexDesc = 1011,
        EndVertexDesc = 1012,
        Vertices = 1013,
        Emissive = 1014,
        Blend = 1015,
    }
}
