using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using CmodConvert.IO;

namespace CmodConvert
{
    internal abstract partial class VertexAttribute : IEnumerable<Variant>
    {
        protected VertexAttribute(AttributeType attribute)
        {
            AttributeType = attribute;
        }

        public AttributeType AttributeType { get; }

        public abstract int Count { get; }
        public abstract int Capacity { get; set; }

        public abstract IEnumerator<Variant> GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

        public static VertexAttribute Create(AttributeType attribute, AttributeFormat format) => format switch
        {
            AttributeFormat.Float1 => new Float1(attribute),
            AttributeFormat.Float2 => new Float2(attribute),
            AttributeFormat.Float3 => new Float3(attribute),
            AttributeFormat.Float4 => new Float4(attribute),
            AttributeFormat.UByte4 => new UByte4(attribute),
            _ => throw new ArgumentOutOfRangeException(nameof(format)),
        };

        public abstract ValueTask Read(IDataReader reader);
    }
}
