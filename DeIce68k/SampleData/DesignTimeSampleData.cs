using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DeIce68k.ViewModel;
using DossySerialPort;

namespace DeIce68k.SampleData
{
    public static class DesignTimeSampleData
    {

        private class DummySerial : IDossySerial
        {
            public int Available => 0;

            public event EventHandler DataReceived;

            public byte PeekByte(int timeoutms = 0)
            {
                throw new TimeoutException();
            }

            public int Read(byte[] b, int length, int timeoutms = 0)
            {
                throw new TimeoutException();
            }

            public byte ReadByte(int timeoutms = 0)
            {
                throw new TimeoutException();
            }

            public void Write(byte[] b, int offset, int length, int timeoutms = 0)
            {
                throw new TimeoutException();
            }
        }

        static DeIceAppModel _app = null;

        public static DeIceAppModel SampleDeIceAppModel { 
            get {
                if (_app == null)
                {
                    _app = new DeIceAppModel(new DummySerial(), null);
                }
                return _app;
            } 
        }

        public static RegisterSetModel SampleDataRegisterSetModel { get { return SampleDeIceAppModel.Regs; } }

        public static StatusRegisterBitsModel SampleStatusRegisterBit { get { return SampleDeIceAppModel.Regs.StatusBits.FirstOrDefault(); } }


        public static RegisterModel RegisterModelTestWord = new RegisterModel("XX", RegisterSize.Word, 0xBEEF);
        public static RegisterModel RegisterModelTestLong = new RegisterModel("XX", RegisterSize.Word, 0xDEADBEEF);



    }
}
