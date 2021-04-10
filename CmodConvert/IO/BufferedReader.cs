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
            var bytes = await ReadBytesRequired(sizeof(short) + sizeof(float) * 3).ConfigureAwait(false);
            if ((DataType)BinaryPrimitives.ReadInt16LittleEndian(bytes.Span) != DataType.Color)
            {
                throw new CmodException("Expected datat type color");
            }

            bytes = bytes[sizeof(short)..];

            return new Color
            {
                Red = BinaryPrimitives.ReadSingleLittleEndian(bytes.Span),
                Green = BinaryPrimitives.ReadSingleLittleEndian(bytes.Span[sizeof(float)..]),
                Blue = BinaryPrimitives.ReadSingleLittleEndian(bytes.Span[(sizeof(float) * 2)..]),
            };
        }

        public async ValueTask<float> ReadFloat1()
        {
            var bytes = await ReadBytesRequired(sizeof(short) + sizeof(float)).ConfigureAwait(false);
            if ((DataType)BinaryPrimitives.ReadInt16LittleEndian(bytes.Span) != DataType.Float1)
            {
                throw new CmodException("Expected data type float1");
            }

            return BinaryPrimitives.ReadSingleLittleEndian(bytes.Span[sizeof(short)..]);
        }

        public async ValueTask<(byte, byte, byte, byte)> ReadUByte4()
        {
            var bytes = await ReadBytesRequired(4).ConfigureAwait(false);
            return (bytes.Span[0], bytes.Span[1], bytes.Span[2], bytes.Span[3]);
        }

        public async ValueTask<string> ReadCmodString()
        {
            var header = await ReadBytesRequired(sizeof(short) * 2).ConfigureAwait(false);
            if ((DataType)BinaryPrimitives.ReadInt16LittleEndian(header.Span) != DataType.String)
            {
                throw new CmodException("Expected a string");
            }

            var length = BinaryPrimitives.ReadUInt16LittleEndian(header.Span[sizeof(short)..]);
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
    }
}
