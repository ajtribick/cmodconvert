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

namespace CmodConvert.IO;

public abstract partial class CmodReader
{
    private class Binary(Stream stream) : CmodReader
    {
        public override async Task<CmodData> Read()
        {
            using var reader = new BufferedReader(stream);

            var materials = new List<Material>();
            var meshes = new List<Mesh>();

            while (true)
            {
                var token = await reader.TryReadToken().ConfigureAwait(false);
                if (!token.HasValue)
                {
                    break;
                }

                switch (token.Value)
                {
                    case Token.Material:
                        materials.Add(await ReadMaterial(reader).ConfigureAwait(false));
                        break;

                    case Token.Mesh:
                        meshes.Add(await ReadMesh(reader, materials.Count).ConfigureAwait(false));
                        break;

                    default:
                        throw new CmodException("Unexpected token in cmod file");
                }
            }

            if (materials.Count == 0)
            {
                throw new CmodException("No materials found");
            }

            if (meshes.Count == 0)
            {
                throw new CmodException("No meshes found");
            }

            return new CmodData(materials, meshes);
        }

        private static async ValueTask<Material> ReadMaterial(BufferedReader reader)
        {
            var material = new Material();
            while (true)
            {
                switch (await reader.ReadToken().ConfigureAwait(false))
                {
                    case Token.Diffuse:
                        material.Diffuse = await reader.ReadColor().ConfigureAwait(false);
                        break;

                    case Token.Specular:
                        material.Specular = await reader.ReadColor().ConfigureAwait(false);
                        break;

                    case Token.Emissive:
                        material.Emissive = await reader.ReadColor().ConfigureAwait(false);
                        break;

                    case Token.SpecularPower:
                        material.SpecularPower = await reader.ReadFloat1().ConfigureAwait(false);
                        break;

                    case Token.Opacity:
                        material.Opacity = await reader.ReadFloat1().ConfigureAwait(false);
                        break;

                    case Token.Blend:
                        material.BlendMode = await reader.ReadBlendMode().ConfigureAwait(false);
                        break;

                    case Token.Texture:
                        var textureSemantic = await reader.ReadTextureSemantic().ConfigureAwait(false);
                        var textureFile = await reader.ReadCmodString().ConfigureAwait(false);
                        material.Textures.Add(textureSemantic, textureFile);
                        break;

                    case Token.EndMaterial:
                        return material;

                    default:
                        throw new CmodException("Unexpected token in material");
                }
            }
        }

        private static async Task<Mesh> ReadMesh(BufferedReader reader, int materialsCount)
        {
            var vertexAttributes = await ReadVertexDescription(reader).ConfigureAwait(false);
            var vertexCount = await ReadVertices(reader, vertexAttributes).ConfigureAwait(false);
            var primitives = await ReadPrimitives(reader, materialsCount, vertexCount).ConfigureAwait(false);

            return new Mesh(vertexAttributes, primitives);
        }

        private static async Task<IReadOnlyList<VertexAttribute>> ReadVertexDescription(BufferedReader reader)
        {
            if (await reader.ReadToken().ConfigureAwait(false) != Token.VertexDesc)
            {
                throw new CmodException("Expected vertex description");
            }

            var attributes = new List<VertexAttribute>();

            while (true)
            {
                var token = await reader.ReadInt16().ConfigureAwait(false);
                if (token == (short)Token.EndVertexDesc)
                {
                    return attributes;
                }

                var attribute = (AttributeType)token;
                if (!Enum.IsDefined(attribute))
                {
                    throw new CmodException("Unknown vertex attribute");
                }

                if (attributes.Any(a => a.AttributeType == attribute))
                {
                    throw new CmodException("Duplicate vertex attribute");
                }

                var format = await reader.ReadAttributeFormat().ConfigureAwait(false);
                if (!Enum.IsDefined(format))
                {
                    throw new CmodException("Invalid attribute format");
                }

                attributes.Add(VertexAttribute.Create(attribute, format));
            }
        }

        private static async Task<int> ReadVertices(BufferedReader reader, IReadOnlyCollection<VertexAttribute> attributes)
        {
            if (await reader.ReadToken().ConfigureAwait(false) != Token.Vertices)
            {
                throw new CmodException("Expected vertices");
            }

            var vertexCount = await reader.ReadInt32().ConfigureAwait(false);
            if (vertexCount <= 0)
            {
                throw new CmodException("Vertex count out of range");
            }

            foreach (var attribute in attributes)
            {
                attribute.Capacity = vertexCount;
            }

            for (var i = 0; i < vertexCount; ++i)
            {
                foreach (var attribute in attributes)
                {
                    await attribute.Read(reader).ConfigureAwait(false);
                }
            }

            return vertexCount;
        }

        private static async Task<IReadOnlyList<Primitive>> ReadPrimitives(BufferedReader reader, int materialsCount, int vertexCount)
        {
            var primitives = new List<Primitive>();

            while (true)
            {
                var token = await reader.ReadInt16().ConfigureAwait(false);
                if (token == (short)Token.EndMesh)
                {
                    return primitives;
                }

                var primitiveType = (PrimitiveType)token;
                if (!Enum.IsDefined(primitiveType))
                {
                    throw new CmodException("Unknown primitive type");
                }

                var materialIndex = await reader.ReadInt32().ConfigureAwait(false);
                if (materialIndex < 0 || materialIndex >= materialsCount)
                {
                    throw new CmodException("Material index out of range");
                }

                var indexCount = await reader.ReadInt32().ConfigureAwait(false);
                if (indexCount <= 0)
                {
                    throw new CmodException("Index count out of range");
                }

                var primitive = new Primitive(primitiveType, materialIndex, indexCount);
                for (var i = 0; i < indexCount; ++i)
                {
                    var index = await reader.ReadInt32().ConfigureAwait(false);
                    if (index < 0 || index >= vertexCount)
                    {
                        throw new CmodException("Index out of range");
                    }

                    primitive.Indices.Add(index);
                }

                primitives.Add(primitive);
            }
        }
    }
}
