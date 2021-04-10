using CmodConvert.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CmodConvert
{
    internal abstract partial class VertexAttribute
    {
        private class UByte4 : VertexAttribute
        {
            private readonly List<(byte, byte, byte, byte)> _data = new();

            public UByte4(AttributeType attribute) : base(attribute) { }

            public override int Count => _data.Count;

            public override int Capacity
            {
                get => _data.Capacity;
                set => _data.Capacity = value;
            }

            public override IEnumerator<Variant> GetEnumerator() => _data.Select(f => new Variant(f.Item1, f.Item2, f.Item3, f.Item4)).GetEnumerator();

            public override async ValueTask Read(IDataReader reader)
            {
                var bytes = await reader.ReadUByte4();
                _data.Add(bytes);
            }
        }
    }
}
