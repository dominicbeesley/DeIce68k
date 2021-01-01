using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeIce68k.ViewModel
{
    public class DisassItemOpModel : DisassItemModelBase
    {
        public string Mnemonic { get; }
        public string Operands { get; }

        public ushort Length { get; }

        public bool Decoded { get; }

        public byte [] InstrBytes { get; }

        public string InstrBytesString {
            get
            {
                return string.Join(" ", InstrBytes.Select(x => $"{x:X2}"));
            }
        }


        public DisassItemOpModel(uint addr, string hints, byte[] instrBytes, string mnemonic, string operands, ushort length, bool decoded, bool pc)
            : base(addr, hints, pc)
        {
            Mnemonic = mnemonic;
            Operands = operands;
            Length = length;
            Decoded = decoded;
            InstrBytes = instrBytes;

        }
    }
}
