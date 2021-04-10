using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace CmodConvert.IO
{
    internal sealed class TokenReader : IDataReader
    {
        private readonly TextReader _reader;
        private string? _line;
        private int _position;
        private bool _disposed;

        public TokenReader(Stream stream, int capacity = 4096)
        {
            try
            {
                _reader = new StreamReader(stream, Encoding.ASCII);
            }
            catch
            {
                _reader?.Dispose();
                throw;
            }
        }

        public async ValueTask<string?> TryNextToken()
        {
            while (true)
            {
                if (_line == null || _position >= _line.Length)
                {
                    _line = await _reader.ReadLineAsync().ConfigureAwait(false);
                    if (_line == null)
                    {
                        return null;
                    }

                    _position = 0;
                }

                while (_position < _line.Length)
                {
                    var current = _position;
                    switch (_line[_position])
                    {
                        case '\t':
                        case ' ':
                            ++_position;
                            break;

                        case '"':
                            var endQuote = _line.IndexOf('"', _position + 1);
                            if (endQuote < 0)
                            {
                                throw new CmodException("Missing string literal terminator");
                            }

                            _position = endQuote + 1;
                            return _line.Substring(current, endQuote - current + 1);

                        case '#':
                            _position = _line.Length;
                            break;

                        default:
                            var endToken = _line.IndexOfAny(new[] { ' ', '\t' }, _position + 1);
                            if (endToken > 0)
                            {
                                _position = endToken + 1;
                                return _line.Substring(current, endToken - current);
                            }

                            _position = _line.Length;
                            return _line.Substring(current);
                    }
                }
            }
        }

        public async ValueTask<string> NextToken() => (await TryNextToken().ConfigureAwait(false)) ?? throw new CmodException("Unexpected end of stream");

        public async ValueTask<int> ReadInt32() => int.Parse(await NextToken().ConfigureAwait(false), CultureInfo.InvariantCulture);

        public async ValueTask<float> ReadSingle() => float.Parse(await NextToken().ConfigureAwait(false), CultureInfo.InvariantCulture);

        public async ValueTask<Color> ReadColor()
        {
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

        public async ValueTask<string> ReadQuoted()
        {
            var token = await NextToken().ConfigureAwait(false);
            return token.StartsWith('"') && token.EndsWith('"') ? token[1..^1] : throw new CmodException("Expected quoted string");
        }

        public async ValueTask<(byte, byte, byte, byte)> ReadUByte4()
        {
            var a = byte.Parse(await NextToken().ConfigureAwait(false), CultureInfo.InvariantCulture);
            var b = byte.Parse(await NextToken().ConfigureAwait(false), CultureInfo.InvariantCulture);
            var c = byte.Parse(await NextToken().ConfigureAwait(false), CultureInfo.InvariantCulture);
            var d = byte.Parse(await NextToken().ConfigureAwait(false), CultureInfo.InvariantCulture);
            return (a, b, c, d);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _reader?.Dispose();
                _disposed = true;
            }
        }
    }
}
