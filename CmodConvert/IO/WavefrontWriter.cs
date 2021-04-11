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
using System.IO;
using System.Text;
using System.Threading.Tasks;
using CmodConvert.Wavefront;

namespace CmodConvert.IO
{
    internal class WavefrontWriter
    {
        private readonly string _objFile;
        private readonly string _mtlFile;

        public WavefrontWriter(string objFile, string mtlFile)
        {
            _objFile = objFile;
            _mtlFile = mtlFile;
        }

        public Task Write(WavefrontMesh mesh) => Task.WhenAll(WriteMtl(mesh.Materials), WriteObj(mesh));

        private async Task WriteMtl(IEnumerable<Material> materials)
        {
            using var stream = new FileStream(_mtlFile, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
            using var writer = new StreamWriter(stream, Encoding.ASCII);

            var i = 0;
            foreach (var material in materials)
            {
                writer.WriteLine($"newmtl material{i}");
                if (material.Diffuse.HasValue)
                {
                    await writer.WriteLineAsync($"Kd {material.Diffuse.Value}").ConfigureAwait(false);
                }

                if (material.Emissive.HasValue)
                {
                    await writer.WriteLineAsync($"Ka {material.Emissive.Value}").ConfigureAwait(false);
                }

                if (material.Specular.HasValue)
                {
                    await writer.WriteLineAsync($"Ks {material.Specular.Value}").ConfigureAwait(false);
                }

                if (material.SpecularPower.HasValue)
                {
                    await writer.WriteLineAsync($"Ns {material.SpecularPower.Value:R}").ConfigureAwait(false);
                }

                if (material.Opacity.HasValue)
                {
                    await writer.WriteLineAsync($"d {material.Opacity.Value:R}").ConfigureAwait(false);
                }

                if (material.BlendMode.HasValue && material.BlendMode.Value != BlendMode.Normal)
                {
                    Console.WriteLine("Warning: Blend mode not supported, ignoring");
                }

                if (material.Textures.Count > 1)
                {
                    Console.WriteLine("Warning: Multi-texturing not supported, using only base texture");
                }

                if (material.Textures.TryGetValue(TextureSemantic.Diffuse, out var diffuseTexture))
                {
                    await writer.WriteLineAsync($"map_Kd {diffuseTexture}").ConfigureAwait(false);
                }

                if (material.Textures.TryGetValue(TextureSemantic.Emissive, out var emissiveTexture))
                {
                    await writer.WriteLineAsync($"map_Ka {emissiveTexture}").ConfigureAwait(false);
                }

                if (material.Textures.TryGetValue(TextureSemantic.Specular, out var specularTexture))
                {
                    await writer.WriteLineAsync($"map_Ks {specularTexture}").ConfigureAwait(false);
                }

                ++i;
            }
        }

        private async Task WriteObj(WavefrontMesh mesh)
        {
            using var stream = new FileStream(_objFile, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
            using var writer = new StreamWriter(stream, Encoding.ASCII);

            var objDirectory = Path.GetDirectoryName(_objFile);
            var mtlRelative = objDirectory != null ? Path.GetRelativePath(objDirectory, _mtlFile) : _mtlFile;

            await writer.WriteLineAsync($"mtllib {mtlRelative}").ConfigureAwait(false);

            foreach (var position in mesh.Positions)
            {
                await writer.WriteLineAsync($"v {position}").ConfigureAwait(false);
            }

            foreach (var texCoord in mesh.TexCoords)
            {
                await writer.WriteLineAsync($"vt {texCoord}").ConfigureAwait(false);
            }

            foreach (var normal in mesh.Normals)
            {
                await writer.WriteLineAsync($"vn {normal}").ConfigureAwait(false);
            }

            foreach (var group in mesh.PrimitiveGroups)
            {
                await writer.WriteLineAsync($"usemtl material{group.Key}").ConfigureAwait(false);
                foreach (var primitive in group.Value)
                {
                    await writer.WriteLineAsync($"{primitive.Category.Command()} {string.Join(' ', primitive.Vertices)}").ConfigureAwait(false);
                }
            }
        }

    }
}
