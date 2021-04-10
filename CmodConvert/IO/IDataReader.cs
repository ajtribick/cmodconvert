using System;
using System.Threading.Tasks;

namespace CmodConvert.IO
{
    internal interface IDataReader : IDisposable
    {
        ValueTask<float> ReadSingle();
        ValueTask<(byte, byte, byte, byte)> ReadUByte4();
    }
}
