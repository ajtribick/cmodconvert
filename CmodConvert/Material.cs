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
using System.Collections.Generic;

namespace CmodConvert
{
    internal class Material
    {
        public Color? Diffuse { get; set; }
        public Color? Specular { get; set; }
        public Color? Emissive { get; set; }
        public float? SpecularPower { get; set; }
        public float? Opacity { get; set; }
        public BlendMode? BlendMode { get; set; }
        public List<string?> Textures { get; } = new List<string?>();

        public void AddTexture(int index, string texture)
        {
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
            if (index < Textures.Count && Textures[index] != null) throw new CmodException("Multiple entries for texture");
            while (Textures.Count <= index)
            {
                Textures.Add(null);
            }

            Textures[index] = texture;
        }
    }
}
