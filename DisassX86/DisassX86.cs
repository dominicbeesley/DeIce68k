using DisassShared;
using System;
using System.IO;
using System.Linq;

namespace DisassX86
{
    public class DisassX86 : IDisAss
    {
        [Flags]
        public enum Prefixes { NONE = 0, ES, CS, SS, DS, REP, REPNZ };

        public enum OpClass { Prefix, Inherent, Inherent_AA, Mem, AccImm }


        public record OpCodeDetails
        {
            public byte And { get; init; }
            public byte Xor { get; init; }

            public Prefixes Pref { get; init; }
            public string Text { get; init; }
            public OpClass OpClass { get; init; }

        }

        public readonly static OpCodeDetails[] OpMap = new[]
        {
            new OpCodeDetails {And = 0xFF, Xor = 0x37, OpClass = OpClass.Inherent, Text = "AAA"},
            new OpCodeDetails {And = 0xFF, Xor = 0xD5, OpClass = OpClass.Inherent_AA, Text = "AAD"},
            new OpCodeDetails {And = 0xFF, Xor = 0xD4, OpClass = OpClass.Inherent_AA, Text = "AAM"},
            new OpCodeDetails {And = 0xFF, Xor = 0x3F, OpClass = OpClass.Inherent, Text = "AAS"},
            new OpCodeDetails {And = 0x7C, Xor = 0x10, OpClass = OpClass.Mem, Text = "ADC"},
        };



        public DisRec2<UInt32> Decode(BinaryReader br, UInt32 pc)
        {
            DisRec2<UInt32> ret = null;
            ushort l = 0;
            Prefixes prefixes = Prefixes.NONE;
            OpCodeDetails opd;
            do
            {
                l++;
                byte opcode = br.ReadByte();
                opd = OpMap.Where(o => (o.And & opcode) == o.Xor).FirstOrDefault();
                if (opd?.OpClass == OpClass.Prefix)
                    prefixes |= opd.Pref;
            } while (l < 15 && opd?.OpClass == OpClass.Prefix);

            if (l <= 15 && opd != null)
            {
                switch (opd.OpClass)
                {
                    case OpClass.Inherent:
                        ret = DoClassInherent(br, pc, l, prefixes, opd);
                        break;
                    case OpClass.Inherent_AA:
                        ret = DoClassInherent_AA(br, pc, l, prefixes, opd);
                        break;
                }
            }



            if (ret == null)
                ret = new DisRec2<uint> { Decoded = false, Length = l };

            return ret;
        }

        public DisRec2<UInt32> DoClassInherent(BinaryReader br, UInt32 pc, ushort l, Prefixes prefixes, OpCodeDetails opd)
        {
            return new DisRec2<uint>
            {
                Decoded = true,
                Length = l,
                Mnemonic = opd.Text
            };
        }

        public DisRec2<UInt32> DoClassInherent_AA(BinaryReader br, UInt32 pc, ushort l, Prefixes prefixes, OpCodeDetails opd)
        {
            if (br.ReadByte() != 0x0A)
                return null;
            else

            return new DisRec2<uint>
            {
                Decoded = true,
                Length = (ushort)(l+1),
                Mnemonic = opd.Text
            };
        }

    }
}
