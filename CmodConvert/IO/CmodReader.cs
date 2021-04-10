using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace CmodConvert.IO
{
    internal abstract partial class CmodReader
    {
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
}
