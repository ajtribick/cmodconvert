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
using System.Linq;

namespace CmodConvert.Wavefront
{
    public class WavefrontMesh
    {
        private WavefrontMesh(
            IReadOnlyCollection<Material> materials,
            IReadOnlyCollection<Variant> positions,
            IReadOnlyCollection<Variant> texCoords,
            IReadOnlyCollection<Variant> normals,
            IReadOnlyDictionary<int, IReadOnlyCollection<ObjPrimitive>> primitiveGroups)
        {
            Materials = materials;
            Positions = positions;
            TexCoords = texCoords;
            Normals = normals;
            PrimitiveGroups = primitiveGroups;
        }

        public IReadOnlyCollection<Material> Materials { get; }
        public IReadOnlyCollection<Variant> Positions { get; }
        public IReadOnlyCollection<Variant> TexCoords { get; }
        public IReadOnlyCollection<Variant> Normals { get; }
        public IReadOnlyDictionary<int, IReadOnlyCollection<ObjPrimitive>> PrimitiveGroups { get; }

        public static WavefrontMesh Create(CmodData model)
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

            return new WavefrontMesh(
                model.Materials,
                positions,
                texCoords,
                normals,
                primitiveGroups.ToDictionary(kvp => kvp.Key, kvp => (IReadOnlyCollection<ObjPrimitive>)kvp.Value));
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

            var attributeEnumerators = new List<IEnumerator<Variant>>(5);
            try
            {
                IEnumerator<Variant>? positionEnumerator = null;
                var primaryTexture = int.MaxValue;
                IEnumerator<Variant>? texCoordEnumerator = null;
                var texCoordEnumerators = new List<IEnumerator<Variant>>(3);
                IEnumerator<Variant>? normalEnumerator = null;

                for (var i = 0; i < mesh.VertexAttributes.Count; ++i)
                {
                    var attributeType = mesh.VertexAttributes[i].AttributeType;
                    switch (attributeType)
                    {
                        case AttributeType.Position:
                            positionEnumerator = mesh.VertexAttributes[i].GetEnumerator();
                            attributeEnumerators.Add(positionEnumerator);
                            break;

                        case AttributeType.Texture0:
                            var texCoord0Enumerator = mesh.VertexAttributes[i].GetEnumerator();
                            attributeEnumerators.Add(texCoord0Enumerator);
                            primaryTexture = 0;
                            texCoordEnumerator = texCoord0Enumerator;
                            texCoordEnumerators.Add(texCoord0Enumerator);
                            break;

                        case AttributeType.Texture2:
                            var texCoord2Enumerator = mesh.VertexAttributes[i].GetEnumerator();
                            attributeEnumerators.Add(texCoord2Enumerator);
                            if (primaryTexture > 2)
                            {
                                primaryTexture = 2;
                                texCoordEnumerator = texCoord2Enumerator;
                            }

                            texCoordEnumerators.Add(texCoord2Enumerator);
                            break;

                        case AttributeType.Texture3:
                            var texCoord3Enumerator = mesh.VertexAttributes[i].GetEnumerator();
                            attributeEnumerators.Add(texCoord3Enumerator);
                            if (primaryTexture > 3)
                            {
                                primaryTexture = 3;
                                texCoordEnumerator = texCoord3Enumerator;
                            }

                            texCoordEnumerators.Add(texCoord3Enumerator);
                            break;

                        case AttributeType.Normal:
                            normalEnumerator = mesh.VertexAttributes[i].GetEnumerator();
                            attributeEnumerators.Add(normalEnumerator);
                            break;

                        default:
                            Console.WriteLine($"Warning: Unsupported attribute {attributeType} found, skipping");
                            break;
                    }
                }

                if (positionEnumerator == null)
                {
                    Console.WriteLine("Warning: No position data for mesh, skipping");
                    return null;
                }

                while (attributeEnumerators.All(e => e.MoveNext()))
                {
                    var position = positionEnumerator.Current;
                    if (!positionLookup.TryGetValue(position, out var positionIndex))
                    {
                        positionIndex = positionLookup.Count + 1;
                        positions.Add(position);
                        positionLookup.Add(position, positionIndex);
                    }

                    if (texCoordEnumerators.Select(e => e.Current).Distinct().Skip(1).Any())
                    {
                        Console.WriteLine($"Warning: Per-texture UV mapping not supported, using texcoord{primaryTexture} for all textures");
                        texCoordEnumerators.Clear();
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
            finally
            {
                foreach (var enumerator in attributeEnumerators)
                {
                    enumerator.Dispose();
                }
            }
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
    }
}
