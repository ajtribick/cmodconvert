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

using System.Text;

namespace CmodConvert.IO;

public abstract partial class CmodReader
{
    private CmodReader()
    { }

    public static CmodReader Create(Stream stream)
    {
        Span<byte> header = stackalloc byte[16];
        if (stream.Read(header) != 16)
        {
            throw new CmodException("Unknown format");
        }

        return Encoding.ASCII.GetString(header) switch
        {
            "#celmodel__ascii" => new Ascii(stream),
            "#celmodel_binary" => new Binary(stream),
            _ => throw new CmodException("Unknown CMOD format"),
        };
    }

    public abstract Task<CmodData> Read();
}
