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
    private class Ascii : CmodReader
    {
        private readonly Stream _stream;

        public Ascii(Stream stream)
        {
            _stream = stream;
        }

        public override async Task<CmodData> Read()
        {
            using var reader = new TokenReader(_stream);
            var materials = new List<Material>();
            var meshes = new List<Mesh>();

            while (true)
            {
                var token = await reader.TryNextToken().ConfigureAwait(false);
                switch (token)
                {
                    case "material":
                        materials.Add(await ReadMaterial(reader).ConfigureAwait(false));
                        break;

                    case "mesh":
                        meshes.Add(await ReadMesh(reader, materials.Count).ConfigureAwait(false));
                        break;

                    case null:
                        return new CmodData(materials, meshes);

                    default:
                        throw new CmodException("Unexpected token in cmod file");
                }
            }
        }

        private static async Task<Material> ReadMaterial(TokenReader reader)
        {
            var material = new Material();

            while (true)
            {
                var token = await reader.NextToken().ConfigureAwait(false);
                switch (token)
                {
                    case "diffuse":
                        material.Diffuse = await reader.ReadColor().ConfigureAwait(false);
                        break;

                    case "specular":
                        material.Specular = await reader.ReadColor().ConfigureAwait(false);
                        break;

                    case "emissive":
                        material.Emissive = await reader.ReadColor().ConfigureAwait(false);
                        break;

                    case "specpower":
                        material.SpecularPower = await reader.ReadSingle().ConfigureAwait(false);
                        break;

                    case "opacity":
                        material.Opacity = await reader.ReadSingle().ConfigureAwait(false);
                        break;

                    case "blend":
                        switch (await reader.NextToken().ConfigureAwait(false))
                        {
                            case "normal":
                                material.BlendMode = BlendMode.Normal;
                                break;

                            case "add":
                                material.BlendMode = BlendMode.Additive;
                                break;

                            case "premultiplied":
                                material.BlendMode = BlendMode.PremultipliedAlpha;
                                break;

                            default:
                                throw new CmodException("Unknown blend mode");
                        }
                        break;

                    case "texture0":
                    case "texture1":
                    case "texture2":
                    case "texture3":
                        var textureSemantic = (TextureSemantic)int.Parse(token[^1..]);
                        if (!Enum.IsDefined(textureSemantic))
                        {
                            throw new CmodException("Invalid texture semantic");
                        }

                        material.Textures.Add(textureSemantic, await reader.ReadQuoted().ConfigureAwait(false));
                        break;

                    case "end_material":
                        return material;

                    default:
                        throw new CmodException("Unexpected token in material");
                }
            }
        }

        private static async Task<Mesh> ReadMesh(TokenReader reader, int materialsCount)
        {
            var vertexAttributes = await ReadVertexDescription(reader).ConfigureAwait(false);
            var vertexCount = await ReadVertices(reader, vertexAttributes).ConfigureAwait(false);
            var primitives = await ReadPrimitives(reader, materialsCount, vertexCount).ConfigureAwait(false);

            return new Mesh(vertexAttributes, primitives);
        }

        private static async Task<IReadOnlyList<VertexAttribute>> ReadVertexDescription(TokenReader reader)
        {
            if (await reader.NextToken().ConfigureAwait(false) != "vertexdesc")
            {
                throw new CmodException("Expected vertex description");
            }

            var attributes = new List<VertexAttribute>();

            while (true)
            {
                var token = await reader.NextToken().ConfigureAwait(false);
                if (token == "end_vertexdesc")
                {
                    return attributes;
                }

                var attribute = token switch
                {
                    "position" => AttributeType.Position,
                    "normal" => AttributeType.Normal,
                    "color0" => AttributeType.Color0,
                    "color1" => AttributeType.Color1,
                    "tangent" => AttributeType.Tangent,
                    "texcoord0" => AttributeType.Texture0,
                    "texcoord1" => AttributeType.Texture1,
                    "texcoord2" => AttributeType.Texture2,
                    "texcoord3" => AttributeType.Texture3,
                    "pointsize" => AttributeType.PointSize,
                    _ => throw new CmodException("Unexpected vertex attribute"),
                };

                if (attributes.Any(a => a.AttributeType == attribute))
                {
                    throw new CmodException("Duplicate vertex attribute");
                }

                var formatToken = await reader.NextToken().ConfigureAwait(false);
                var format = formatToken switch
                {
                    "f1" => AttributeFormat.Float1,
                    "f2" => AttributeFormat.Float2,
                    "f3" => AttributeFormat.Float3,
                    "f4" => AttributeFormat.Float4,
                    "ub4" => AttributeFormat.UByte4,
                    _ => throw new CmodException("Unknown vertex format"),
                };

                attributes.Add(VertexAttribute.Create(attribute, format));
            }
        }

        private static async Task<int> ReadVertices(TokenReader reader, IReadOnlyCollection<VertexAttribute> attributes)
        {
            if (await reader.NextToken().ConfigureAwait(false) != "vertices")
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

        private static async Task<IReadOnlyList<Primitive>> ReadPrimitives(TokenReader reader, int materialsCount, int vertexCount)
        {
            var primitives = new List<Primitive>();

            while (true)
            {
                var token = await reader.NextToken().ConfigureAwait(false);
                if (token == "end_mesh")
                {
                    return primitives;
                }

                var primitiveType = token switch
                {
                    "trilist" => PrimitiveType.TriList,
                    "tristrip" => PrimitiveType.TriStrip,
                    "trifan" => PrimitiveType.TriFan,
                    "linelist" => PrimitiveType.LineList,
                    "linestrip" => PrimitiveType.LineStrip,
                    "points" => PrimitiveType.PointList,
                    "sprites" => PrimitiveType.SpriteList,
                    _ => throw new CmodException("Unknown primitive type"),
                };

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
