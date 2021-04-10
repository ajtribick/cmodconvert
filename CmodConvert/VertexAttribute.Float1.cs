using CmodConvert.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CmodConvert
{
    internal abstract partial class VertexAttribute
    {
        private class Float1 : VertexAttribute
        {
            private readonly List<float> _data = new();

            public Float1(AttributeType attribute) : base(attribute) { }

            public override int Count => _data.Count;

            public override int Capacity
            {
                get => _data.Capacity;
                set => _data.Capacity = value;
            }

            public override IEnumerator<Variant> GetEnumerator() => _data.Select(f => new Variant(f)).GetEnumerator();

            public override async ValueTask Read(IDataReader reader)
            {
                _data.Add(await reader.ReadSingle().ConfigureAwait(false));
            }
        }
    }
}
