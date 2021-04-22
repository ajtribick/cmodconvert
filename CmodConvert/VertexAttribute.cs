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
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using CmodConvert.IO;

namespace CmodConvert
{
    public abstract partial class VertexAttribute : IEnumerable<Variant>
    {
        private VertexAttribute(AttributeType attribute)
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
