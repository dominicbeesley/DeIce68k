using System;
using System.Collections.Generic;
using System.Text;

namespace DossySerialPort
{
    public interface IDossySerial : IDisposable
    {

        int Available { get; }
        int Read(byte[] b, int length, int timeoutms = -1, bool immediate = false);
        void Write(byte[] b, int offset, int length, int timeoutms = -1);

        //return -1 if none waiting
        int PeekByte();
        byte ReadByte(int timeoutms = 0);

    }
}
