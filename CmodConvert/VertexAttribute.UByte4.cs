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

using CmodConvert.IO;

namespace CmodConvert;

public abstract partial class VertexAttribute
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
