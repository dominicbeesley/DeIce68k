using System;
using System.Collections.Generic;
using System.Text;

namespace DossySerialPort
{
    public interface IDossySerial : IDisposable
    {
        event EventHandler DataReceived;

        int Available { get; }
        int Read(byte[] b, int length, int timeoutms = -1, bool immediate = false);
        void Write(byte[] b, int offset, int length, int timeoutms = -1);

        byte PeekByte(int timeoutms = 0);
        byte ReadByte(int timeoutms = 0);

    }
}
