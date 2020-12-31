using System;
using System.IO;

namespace Disass68k
{
    public class BEReader
    {
        private BinaryReader b;
        public uint PC
        {
            get;
            private set;
        }

        public BEReader(BinaryReader b, uint pc)
        {
            this.PC = pc;
            this.b = b;
        }



        // Note this MODIFIES THE GIVEN ARRAY then returns a reference to the modified array.
        byte[] Reverse(byte[] b)
        {
            Array.Reverse(b);
            return b;
        }

        public UInt16 ReadUInt16BE()
        {
            PC += sizeof(UInt16);
            if (BitConverter.IsLittleEndian)
                return BitConverter.ToUInt16(Reverse(ReadBytesRequired(sizeof(UInt16))), 0);
            else
                return BitConverter.ToUInt16(ReadBytesRequired(sizeof(UInt16)), 0);
        }

        public Int16 ReadInt16BE()
        {
            PC += sizeof(UInt16);
            if (BitConverter.IsLittleEndian)
                return BitConverter.ToInt16(Reverse(ReadBytesRequired(sizeof(Int16))), 0);
            else
                return BitConverter.ToInt16(ReadBytesRequired(sizeof(Int16)), 0);
        }

        public UInt32 ReadUInt32BE()
        {
            PC += sizeof(UInt32);
            if (BitConverter.IsLittleEndian)
                return BitConverter.ToUInt32(Reverse(ReadBytesRequired(sizeof(UInt32))), 0);
            else
                return BitConverter.ToUInt32(ReadBytesRequired(sizeof(UInt32)), 0);
        }

        public Int32 ReadInt32BE()
        {
            PC += sizeof(UInt32);
            if (BitConverter.IsLittleEndian)
                return BitConverter.ToInt32(Reverse(ReadBytesRequired(sizeof(Int32))), 0);
            else
                return BitConverter.ToInt32(ReadBytesRequired(sizeof(Int32)), 0);
        }

        public byte[] ReadBytesRequired(int byteCount)
        {
            var result = b.ReadBytes(byteCount);

            if (result.Length != byteCount)
                throw new EndOfStreamException(string.Format("{0} bytes required from stream, but only {1} returned.", byteCount, result.Length));

            return result;
        }
    }
}
