using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CmodConvert.IO
{
    internal class WavefrontWriter
    {
        private readonly IReadOnlyCollection<Material> _materials;
        private readonly IReadOnlyCollection<Variant> _positions;
        private readonly IReadOnlyCollection<Variant> _texCoords;
        private readonly IReadOnlyCollection<Variant> _normals;
        private readonly IReadOnlyDictionary<int, IReadOnlyCollection<ObjPrimitive>> _primitiveGroups;

        private WavefrontWriter(
            IReadOnlyCollection<Material> materials,
            IReadOnlyCollection<Variant> positions,
            IReadOnlyCollection<Variant> texCoords,
            IReadOnlyCollection<Variant> normals,
            IReadOnlyDictionary<int, IReadOnlyCollection<ObjPrimitive>> primitiveGroups)
        {
            _materials = materials;
            _positions = positions;
            _texCoords = texCoords;
            _normals = normals;
            _primitiveGroups = primitiveGroups;
        }

        public static WavefrontWriter Create(CmodData model)
        {
            var vertexCount = model.Meshes.Sum(m => m.VertexCount);
            var positions = new List<Variant>(vertexCount);
            var texCoords = new List<Variant>(vertexCount);
            var normals = new List<Variant>(vertexCount);
            var positionLookup = new Dictionary<Variant, int>(vertexCount);
            var texCoordLookup = new Dictionary<Variant, int>(vertexCount);
            var normalLookup = new Dictionary<Variant, int>(vertexCount);
            var primitiveGroups = new Dictionary<int, List<ObjPrimitive>>();

            foreach (var mesh in model.Meshes)
            {
                var vertexInfo = ProcessVertices(mesh, positions, texCoords, normals, positionLookup, texCoordLookup, normalLookup);
                if (vertexInfo == null)
                {
                    continue;
                }

                ProcessPrimitives(mesh, primitiveGroups, vertexInfo);
            }

            return new WavefrontWriter(
                model.Materials,
                positions,
                texCoords,
                normals,
                primitiveGroups.ToDictionary(kvp => kvp.Key, kvp => (IReadOnlyCollection<ObjPrimitive>)kvp.Value));
        }

        public async Task WriteMaterial(TextWriter writer)
        {
            var i = 0;
            foreach (var material in _materials)
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

                var texture = material.Textures.FirstOrDefault();
                if (texture != null)
                {
                    if (material.Diffuse.HasValue)
                    {
                        await writer.WriteLineAsync($"map_Kd {texture}").ConfigureAwait(false);
                    }

                    if (material.Emissive.HasValue)
                    {
                        await writer.WriteLineAsync($"map_Ka {texture}").ConfigureAwait(false);
                    }
                }

                ++i;
            }
        }

        public async Task WriteModel(TextWriter writer, string materialFile)
        {
            await writer.WriteLineAsync($"mtllib {materialFile}").ConfigureAwait(false);

            foreach (var position in _positions)
            {
                await writer.WriteLineAsync($"v {position}").ConfigureAwait(false);
            }

            foreach (var texCoord in _texCoords)
            {
                await writer.WriteLineAsync($"vt {texCoord}").ConfigureAwait(false);
            }

            foreach (var normal in _normals)
            {
                await writer.WriteLineAsync($"vn {normal}").ConfigureAwait(false);
            }

            foreach (var group in _primitiveGroups)
            {
                await writer.WriteLineAsync($"usemtl material{group.Key}").ConfigureAwait(false);
                foreach (var primitive in group.Value)
                {
                    await writer.WriteLineAsync($"{primitive.Category.Command()} {string.Join(' ', primitive.Vertices)}").ConfigureAwait(false);
                }
            }
        }

        private static List<VertexInfo>? ProcessVertices(
            Mesh mesh,
            List<Variant> positions,
            List<Variant> texCoords,
            List<Variant> normals,
            Dictionary<Variant, int> positionLookup,
            Dictionary<Variant, int> texCoordLookup,
            Dictionary<Variant, int> normalLookup)
        {
            var vertexInfo = new List<VertexInfo>(mesh.VertexCount);
            var positionAttribute = -1;
            var texCoordAttribute = -1;
            var normalAttribute = -1;
            for (var i = 0; i < mesh.VertexAttributes.Count; ++i)
            {
                var attributeType = mesh.VertexAttributes[i].AttributeType;
                switch (attributeType)
                {
                    case AttributeType.Position:
                        positionAttribute = i;
                        break;

                    case AttributeType.Texture0:
                        texCoordAttribute = i;
                        break;

                    case AttributeType.Normal:
                        normalAttribute = i;
                        break;

                    default:
                        Console.WriteLine($"Warning: Unsupported attribute {attributeType} found, skipping");
                        break;
                }
            }

            if (positionAttribute == -1)
            {
                Console.WriteLine("Warning: No position data for mesh, skipping");
                return null;
            }

            using var positionEnumerator = mesh.VertexAttributes[positionAttribute].GetEnumerator();
            using var texCoordEnumerator = texCoordAttribute >= 0 ? mesh.VertexAttributes[texCoordAttribute].GetEnumerator() : null;
            using var normalEnumerator = normalAttribute >= 0 ? mesh.VertexAttributes[normalAttribute].GetEnumerator() : null;

            while (positionEnumerator.MoveNext() && (texCoordEnumerator?.MoveNext() ?? true) && (normalEnumerator?.MoveNext() ?? true))
            {
                var position = positionEnumerator.Current;
                if (!positionLookup.TryGetValue(position, out var positionIndex))
                {
                    positionIndex = positionLookup.Count + 1;
                    positions.Add(position);
                    positionLookup.Add(position, positionIndex);
                }

                int texCoordIndex;
                if (texCoordEnumerator == null)
                {
                    texCoordIndex = -1;
                }
                else
                {
                    var texCoord = texCoordEnumerator.Current;
                    if (!texCoordLookup.TryGetValue(texCoord, out texCoordIndex))
                    {
                        texCoordIndex = texCoordLookup.Count + 1;
                        texCoords.Add(texCoord);
                        texCoordLookup.Add(texCoord, texCoordIndex);
                    }
                }

                int normalIndex;
                if (normalEnumerator == null)
                {
                    normalIndex = -1;
                }
                else
                {
                    var normal = normalEnumerator.Current;
                    if (!normalLookup.TryGetValue(normal, out normalIndex))
                    {
                        normalIndex = normalLookup.Count + 1;
                        normals.Add(normal);
                        normalLookup.Add(normal, normalIndex);
                    }
                }

                vertexInfo.Add(new VertexInfo
                {
                    Position = positionIndex,
                    TexCoord = texCoordIndex,
                    Normal = normalIndex,
                });
            }

            return vertexInfo;
        }

        private static void ProcessPrimitives(Mesh mesh, Dictionary<int, List<ObjPrimitive>> primitiveGroups, IReadOnlyList<VertexInfo> vertexInfo)
        {
            foreach (var primitive in mesh.Primitives)
            {
                if (!primitiveGroups.TryGetValue(primitive.MaterialIndex, out var primitiveGroup))
                {
                    primitiveGroup = new List<ObjPrimitive>();
                    primitiveGroups.Add(primitive.MaterialIndex, primitiveGroup);
                }

                switch (primitive.PrimitiveType)
                {
                    case PrimitiveType.TriList:
                        ProcessTriList(primitiveGroup, vertexInfo, primitive.Indices);
                        break;

                    case PrimitiveType.TriStrip:
                        ProcessTriStrip(primitiveGroup, vertexInfo, primitive.Indices);
                        break;

                    case PrimitiveType.TriFan:
                        ProcessTriFan(primitiveGroup, vertexInfo, primitive.Indices);
                        break;

                    case PrimitiveType.LineList:
                        ProcessLineList(primitiveGroup, vertexInfo, primitive.Indices);
                        break;

                    case PrimitiveType.LineStrip:
                        ProcessLineStrip(primitiveGroup, vertexInfo, primitive.Indices);
                        break;

                    case PrimitiveType.SpriteList:
                        Console.WriteLine("Warning: point sprite sizes not supported, using points instead");
                        goto case PrimitiveType.PointList;

                    case PrimitiveType.PointList:
                        ProcessPoints(primitiveGroup, vertexInfo, primitive.Indices);
                        break;

                    default:
                        Console.WriteLine("Unknown primitive type, skipping");
                        break;
                }
            }
        }

        private static void ProcessTriList(List<ObjPrimitive> primitiveGroup, IReadOnlyList<VertexInfo> vertexInfo, IReadOnlyList<int> indices)
        {
            for (int i = 0; i < indices.Count; i += 3)
            {
                var primitive = new ObjPrimitive(PrimitiveCategory.Triangle, 3);
                primitive.Vertices.Add(vertexInfo[indices[i]]);
                primitive.Vertices.Add(vertexInfo[indices[i + 1]]);
                primitive.Vertices.Add(vertexInfo[indices[i + 2]]);
                primitiveGroup.Add(primitive);
            }
        }

        private static void ProcessTriStrip(List<ObjPrimitive> primitiveGroup, IReadOnlyList<VertexInfo> vertexInfo, IReadOnlyList<int> indices)
        {
            var a = vertexInfo[indices[0]];
            var b = vertexInfo[indices[1]];
            for (int i = 2; i < indices.Count; ++i)
            {
                var c = vertexInfo[indices[i]];
                var primitive = new ObjPrimitive(PrimitiveCategory.Triangle, 3);
                primitive.Vertices.Add(a);
                primitive.Vertices.Add(b);
                primitive.Vertices.Add(c);
                primitiveGroup.Add(primitive);
                a = b;
                b = c;
            }
        }

        private static void ProcessTriFan(List<ObjPrimitive> primitiveGroup, IReadOnlyList<VertexInfo> vertexInfo, IReadOnlyList<int> indices)
        {
            var a = vertexInfo[indices[0]];
            var b = vertexInfo[indices[1]];
            for (int i = 2; i < indices.Count; ++i)
            {
                var c = vertexInfo[indices[i]];
                var primitive = new ObjPrimitive(PrimitiveCategory.Triangle, 3);
                primitive.Vertices.Add(a);
                primitive.Vertices.Add(b);
                primitive.Vertices.Add(c);
                primitiveGroup.Add(primitive);
                b = c;
            }
        }

        private static void ProcessLineList(List<ObjPrimitive> primitiveGroup, IReadOnlyList<VertexInfo> vertexInfo, IReadOnlyList<int> indices)
        {
            for (int i = 0; i < indices.Count; i += 2)
            {
                var primitive = new ObjPrimitive(PrimitiveCategory.Line, 2);
                primitive.Vertices.Add(vertexInfo[indices[i]]);
                primitive.Vertices.Add(vertexInfo[indices[i + 1]]);
                primitiveGroup.Add(primitive);
            }
        }

        private static void ProcessLineStrip(List<ObjPrimitive> primitiveGroup, IReadOnlyList<VertexInfo> vertexInfo, IReadOnlyList<int> indices)
        {
            var primitive = new ObjPrimitive(PrimitiveCategory.Line, indices.Count);
            primitive.Vertices.AddRange(indices.Select(i => vertexInfo[i]));
            primitiveGroup.Add(primitive);
        }

        private static void ProcessPoints(List<ObjPrimitive> primitiveGroup, IReadOnlyList<VertexInfo> vertexInfo, IReadOnlyList<int> indices)
        {
            var primitive = new ObjPrimitive(PrimitiveCategory.Point, indices.Count);
            primitive.Vertices.AddRange(indices.Select(i => vertexInfo[i]));
            primitiveGroup.Add(primitive);
        }

        private readonly struct VertexInfo
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

        private class ObjPrimitive
        {
            public PrimitiveCategory Category { get; }
            public List<VertexInfo> Vertices { get; }

            public ObjPrimitive(PrimitiveCategory category, int capacity)
            {
                Category = category;
                Vertices = new(capacity);
            }
        }
    }
}
