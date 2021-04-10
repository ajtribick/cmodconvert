using System.Collections.Generic;
using System.Linq;

namespace CmodConvert
{
    internal class Mesh
    {
        public Mesh(
            IReadOnlyList<VertexAttribute> vertexAttributes,
            IReadOnlyList<Primitive> primitives)
        {
            VertexAttributes = vertexAttributes;
            Primitives = primitives;
        }

        public int VertexCount => VertexAttributes.First().Count;
        public IReadOnlyList<VertexAttribute> VertexAttributes { get; }
        public IReadOnlyList<Primitive> Primitives { get; }
    }
}
