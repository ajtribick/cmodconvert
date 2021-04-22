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
using System.Buffers;
using System.Buffers.Binary;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace CmodConvert.IO
{
    internal sealed class BufferedReader : IDataReader
    {
        private readonly Stream _stream;
        private readonly byte[] _buffer;
        private int _length;
        private int _position;
        private bool _disposed;

        public BufferedReader(Stream stream, int capacity = 4096)
        {
            try
            {
                _stream = stream;
                _buffer = ArrayPool<byte>.Shared.Rent(capacity);
            }
            catch
            {
                if (_buffer != null)
                {
                    ArrayPool<byte>.Shared.Return(_buffer);
                }

                throw;
            }
        }

        public async ValueTask<Memory<byte>> ReadBytes(int length)
        {
            var remaining = _length - _position;
            if (remaining < length)
            {
                Array.Copy(_buffer, _position, _buffer, 0, remaining);
                _position = 0;
                _length = remaining;
                while (_length < length)
                {
                    var bytesRead = await _stream.ReadAsync(_buffer.AsMemory()[_length..]).ConfigureAwait(false);
                    if (bytesRead == 0)
                    {
                        break;
                    }

                    _length += bytesRead;
                }

                length = Math.Min(_length, length);
            }

            var start = _position;
            _position += length;
            return _buffer.AsMemory().Slice(start, length);
        }

        public async ValueTask<Memory<byte>> ReadBytesRequired(int length)
        {
            var remaining = _length - _position;
            if (remaining < length)
            {
                Array.Copy(_buffer, _position, _buffer, 0, remaining);
                _position = 0;
                _length = remaining;
                while (_length < length)
                {
                    var bytesRead = await _stream.ReadAsync(_buffer.AsMemory()[_length..]).ConfigureAwait(false);
                    if (bytesRead == 0)
                    {
                        throw new CmodException("Unexpected end of stream");
                    }

                    _length += bytesRead;
                }
            }

            var start = _position;
            _position += length;
            return _buffer.AsMemory().Slice(start, length);
        }

        public async ValueTask<short> ReadInt16()
        {
            var bytes = await ReadBytesRequired(sizeof(short)).ConfigureAwait(false);
            return BinaryPrimitives.ReadInt16LittleEndian(bytes.Span);
        }

        public async ValueTask<ushort> ReadUInt16()
        {
            var bytes = await ReadBytesRequired(sizeof(ushort)).ConfigureAwait(false);
            return BinaryPrimitives.ReadUInt16LittleEndian(bytes.Span);
        }

        public async ValueTask<int> ReadInt32()
        {
            var bytes = await ReadBytesRequired(sizeof(int)).ConfigureAwait(false);
            return BinaryPrimitives.ReadInt32LittleEndian(bytes.Span);
        }

        public async ValueTask<Token> ReadToken()
        {
            var token = (Token)await ReadInt16().ConfigureAwait(false);
            return Enum.IsDefined(token) ? token : throw new CmodException("Invalid token type");
        }

        public async ValueTask<Token?> TryReadToken()
        {
            var bytes = await ReadBytes(sizeof(short)).ConfigureAwait(false);
            switch (bytes.Length)
            {
                case 0:
                    return null;
                case 1:
                    throw new CmodException("Partial token");
                default:
                    var token = (Token)BinaryPrimitives.ReadInt16LittleEndian(bytes.Span);
                    return Enum.IsDefined(token) ? token : throw new CmodException("Invalid token type");
            }
        }

        public async ValueTask<TextureSemantic> ReadTextureSemantic()
        {
            var semantic = (TextureSemantic)await ReadInt16().ConfigureAwait(false);
            return Enum.IsDefined(semantic) ? semantic : throw new CmodException("Invalid texture semantic");
        }

        public async ValueTask<AttributeFormat> ReadAttributeFormat()
        {
            var format = (AttributeFormat)await ReadInt16().ConfigureAwait(false);
            return Enum.IsDefined(format) ? format : throw new CmodException("Invalid attribute format");
        }

        public async ValueTask<BlendMode> ReadBlendMode()
        {
            var blendMode = (BlendMode)await ReadInt16().ConfigureAwait(false);
            return Enum.IsDefined(blendMode) ? blendMode : throw new CmodException("Invalid blend mode");
        }

        public async ValueTask<float> ReadSingle()
        {
            var bytes = await ReadBytesRequired(sizeof(float)).ConfigureAwait(false);
            return BinaryPrimitives.ReadSingleLittleEndian(bytes.Span);
        }

        public async ValueTask<Color> ReadColor()
        {
            if (await ReadDataType().ConfigureAwait(false) != DataType.Color)
            {
                throw new CmodException("Expected data type color");
            }

            var red = await ReadSingle().ConfigureAwait(false);
            var green = await ReadSingle().ConfigureAwait(false);
            var blue = await ReadSingle().ConfigureAwait(false);

            return new Color
            {
                Red = red,
                Green = green,
                Blue = blue,
            };
        }

        public async ValueTask<float> ReadFloat1()
        {
            if (await ReadDataType().ConfigureAwait(false) != DataType.Float1)
            {
                throw new CmodException("Expected data type float1");
            }

            return await ReadSingle().ConfigureAwait(false);
        }

        public async ValueTask<(byte, byte, byte, byte)> ReadUByte4()
        {
            var bytes = await ReadBytesRequired(4).ConfigureAwait(false);
            return (bytes.Span[0], bytes.Span[1], bytes.Span[2], bytes.Span[3]);
        }

        public async ValueTask<string> ReadCmodString()
        {
            if (await ReadDataType().ConfigureAwait(false) != DataType.String)
            {
                throw new CmodException("Expected data type string");
            }

            var length = await ReadUInt16().ConfigureAwait(false);
            var data = await ReadBytesRequired(length).ConfigureAwait(false);
            return Encoding.ASCII.GetString(data.Span);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                if (_buffer != null)
                {
                    ArrayPool<byte>.Shared.Return(_buffer);
                }

                _disposed = true;
            }
        }

        private async ValueTask<DataType> ReadDataType()
        {
            var dataType = (DataType)await ReadInt16().ConfigureAwait(false);
            return Enum.IsDefined(dataType) ? dataType : throw new CmodException("Invalid data type");
        }

        private enum DataType : short
        {
            Float1 = 1,
            Float2 = 2,
            Float3 = 3,
            Float4 = 4,
            String = 5,
            UInt32 = 6,
            Color = 7,
        }
    }
}
