using CmodConvert.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CmodConvert
{
    internal abstract partial class VertexAttribute
    {
        private class Float3 : VertexAttribute
        {
            private readonly List<(float, float, float)> _data = new();

            public Float3(AttributeType attribute) : base(attribute) { }

            public override int Count => _data.Count;

            public override int Capacity
            {
                get => _data.Capacity;
                set => _data.Capacity = value;
            }

            public override IEnumerator<Variant> GetEnumerator() => _data.Select(f => new Variant(f.Item1, f.Item2, f.Item3)).GetEnumerator();

            public override async ValueTask Read(IDataReader reader)
            {
                var a = await reader.ReadSingle().ConfigureAwait(false);
                var b = await reader.ReadSingle().ConfigureAwait(false);
                var c = await reader.ReadSingle().ConfigureAwait(false);
                _data.Add((a, b, c));
            }
        }
    }
}
