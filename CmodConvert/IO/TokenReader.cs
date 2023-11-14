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

using System.Globalization;
using System.Text;

namespace CmodConvert.IO;

internal sealed class TokenReader : IDataReader
{
    private static readonly char[] s_endTokenChars = [' ', '\t'];

    private readonly TextReader _reader;
    private string? _line;
    private int _position;
    private bool _disposed;

    public TokenReader(Stream stream)
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
                        var endToken = _line.IndexOfAny(s_endTokenChars, _position + 1);
                        if (endToken > 0)
                        {
                            _position = endToken + 1;
                            return _line[current..endToken];
                        }

                        _position = _line.Length;
                        return _line[current..];
                }
            }
        }
    }

    public async ValueTask<string> NextToken()
        => (await TryNextToken().ConfigureAwait(false)) ?? throw new CmodException("Unexpected end of stream");

    public async ValueTask<int> ReadInt32()
        => int.Parse(await NextToken().ConfigureAwait(false), CultureInfo.InvariantCulture);

    public async ValueTask<float> ReadSingle()
        => float.Parse(await NextToken().ConfigureAwait(false), CultureInfo.InvariantCulture);

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
