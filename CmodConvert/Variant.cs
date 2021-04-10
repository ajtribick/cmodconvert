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
using System.Runtime.InteropServices;
using static System.FormattableString;

namespace CmodConvert
{
    [StructLayout(LayoutKind.Explicit)]
    internal readonly struct Variant : IEquatable<Variant>
    {
        [FieldOffset(0)]
        private readonly AttributeFormat _format;

        [FieldOffset(2)]
        private readonly byte _b1;

        [FieldOffset(3)]
        private readonly byte _b2;

        [FieldOffset(4)]
        private readonly byte _b3;

        [FieldOffset(5)]
        private readonly byte _b4;

        [FieldOffset(4)]
        private readonly float _f1;

        [FieldOffset(8)]
        private readonly float _f2;

        [FieldOffset(12)]
        private readonly float _f3;

        [FieldOffset(16)]
        private readonly float _f4;

        public Variant(float f)
        {
            _format = AttributeFormat.Float1;
            _b1 = _b2 = _b3 = _b4 = 0;
            _f1 = f;
            _f2 = _f3 = _f4 = 0.0f;
        }

        public Variant(float f1, float f2)
        {
            _format = AttributeFormat.Float2;
            _b1 = _b2 = _b3 = _b4 = 0;
            _f1 = f1;
            _f2 = f2;
            _f3 = _f4 = 0.0f;
        }

        public Variant(float f1, float f2, float f3)
        {
            _format = AttributeFormat.Float3;
            _b1 = _b2 = _b3 = _b4 = 0;
            _f1 = f1;
            _f2 = f2;
            _f3 = f3;
            _f4 = 0.0f;
        }

        public Variant(float f1, float f2, float f3, float f4)
        {
            _format = AttributeFormat.Float4;
            _b1 = _b2 = _b3 = _b4 = 0;
            _f1 = f1;
            _f2 = f2;
            _f3 = f3;
            _f4 = f4;
        }

        public Variant(byte b1, byte b2, byte b3, byte b4)
        {
            _format = AttributeFormat.UByte4;
            _f1 = _f2 = _f3 = _f4 = 0.0f;
            _b1 = b1;
            _b2 = b2;
            _b3 = b3;
            _b4 = b4;
        }

        public override bool Equals(object? obj)
        {
            return obj is Variant other && Equals(other);
        }

        public bool Equals(Variant other)
        {
            if (_format != other._format)
            {
                return false;
            }

            return _format switch
            {
                AttributeFormat.Float1 => _f1 == other._f1,
                AttributeFormat.Float2 => _f1 == other._f1 && _f2 == other._f2,
                AttributeFormat.Float3 => _f1 == other._f1 && _f2 == other._f2 && _f3 == other._f3,
                AttributeFormat.Float4 => _f1 == other._f1 && _f2 == other._f2 && _f3 == other._f3 && _f4 == other._f4,
                AttributeFormat.UByte4 => _b1 == other._b1 && _b2 == other._b2 && _b3 == other._b3 && _b4 == other._b4,
                _ => false,
            };
        }

        public override int GetHashCode()
        {
            var hashCode = new HashCode();
            hashCode.Add(_format);
            switch (_format)
            {
                case AttributeFormat.Float1:
                    hashCode.Add(_f1);
                    break;

                case AttributeFormat.Float2:
                    hashCode.Add(_f1);
                    hashCode.Add(_f2);
                    break;

                case AttributeFormat.Float3:
                    hashCode.Add(_f1);
                    hashCode.Add(_f2);
                    hashCode.Add(_f3);
                    break;

                case AttributeFormat.Float4:
                    hashCode.Add(_f1);
                    hashCode.Add(_f2);
                    hashCode.Add(_f3);
                    hashCode.Add(_f4);
                    break;

                case AttributeFormat.UByte4:
                    hashCode.Add(_b1);
                    hashCode.Add(_b2);
                    hashCode.Add(_b3);
                    hashCode.Add(_b4);
                    break;
            }

            return hashCode.ToHashCode();
        }

        public override string ToString() => Invariant(_format switch
        {
            AttributeFormat.Float1 => $"{_f1:R}",
            AttributeFormat.Float2 => $"{_f1:R} {_f2:R}",
            AttributeFormat.Float3 => $"{_f1:R} {_f2:R} {_f3:R}",
            AttributeFormat.Float4 => $"{_f1:R} {_f2:R} {_f3:R} {_f4:R}",
            AttributeFormat.UByte4 => $"{_b1} {_b2} {_b3} {_b4}",
            _ => $"#ERROR#",
        });

        public static bool operator ==(Variant a, Variant b) => a.Equals(b);

        public static bool operator !=(Variant a, Variant b) => !a.Equals(b);
    }
}
