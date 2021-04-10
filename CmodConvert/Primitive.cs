using System.Collections.Generic;

namespace CmodConvert
{
    internal class Primitive
    {
        public PrimitiveType PrimitiveType { get; }
        public int MaterialIndex { get; }
        public List<int> Indices { get; }

        public Primitive(PrimitiveType primitiveType, int materialIndex, int count)
        {
            PrimitiveType = primitiveType;
            MaterialIndex = materialIndex;
            Indices = new(count);
        }
    }
}
