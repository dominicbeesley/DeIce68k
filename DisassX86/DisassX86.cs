using DisassShared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DisassX86
{
    public class DisassX86 : IDisAss
    {
        [Flags]
        public enum Prefixes { 
            NONE = 0, 
            ES, CS, SS, DS, 
            SEGS = Prefixes.ES | Prefixes.CS | Prefixes.SS | Prefixes.DS,
            REP, REPNZ };

        public enum OpClass { Prefix, Inherent, Inherent_AA, Mem, MemImm, AccDisp, RegImm, MemSeg }


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
            new OpCodeDetails {And = 0xFF, Xor = 0x26, OpClass = OpClass.Prefix, Text = "es:", Pref = Prefixes.ES},
            new OpCodeDetails {And = 0xFF, Xor = 0x2E, OpClass = OpClass.Prefix, Text = "cs:", Pref = Prefixes.CS},
            new OpCodeDetails {And = 0xFF, Xor = 0x36, OpClass = OpClass.Prefix, Text = "ss:", Pref = Prefixes.SS},
            new OpCodeDetails {And = 0xFF, Xor = 0x3e, OpClass = OpClass.Prefix, Text = "ds:", Pref = Prefixes.DS},

            new OpCodeDetails {And = 0xFF, Xor = 0x37, OpClass = OpClass.Inherent, Text = "aaa"},
            new OpCodeDetails {And = 0xFF, Xor = 0xD5, OpClass = OpClass.Inherent_AA, Text = "aad"},
            new OpCodeDetails {And = 0xFF, Xor = 0xD4, OpClass = OpClass.Inherent_AA, Text = "aam"},
            new OpCodeDetails {And = 0xFF, Xor = 0x3F, OpClass = OpClass.Inherent, Text = "aas"},
            new OpCodeDetails {And = 0xFC, Xor = 0x88, OpClass = OpClass.Mem, Text = "mov"},
            new OpCodeDetails {And = 0xFE, Xor = 0xC6, OpClass = OpClass.MemImm, Text = "mov"},
            new OpCodeDetails {And = 0xF0, Xor = 0xB0, OpClass = OpClass.RegImm, Text = "mov"},
            new OpCodeDetails {And = 0xFC, Xor = 0xA0, OpClass = OpClass.AccDisp, Text = "mov"},
            new OpCodeDetails {And = 0xFD, Xor = 0x8C, OpClass = OpClass.MemSeg, Text = "mov"},
        };


        public DisRec2<UInt32> Decode(BinaryReader br, UInt32 pc)
        {
            DisRec2<UInt32> ret = null;
            ushort l = 0;
            Prefixes prefixes = Prefixes.NONE;
            OpCodeDetails opd;
            byte opcode;
            do
            {
                l++;
                opcode = br.ReadByte();
                opd = OpMap.Where(o => (o.And & opcode) == o.Xor).FirstOrDefault();
                if (opd?.OpClass == OpClass.Prefix)
                    prefixes |= opd.Pref;
            } while (l < 15 && opd?.OpClass == OpClass.Prefix);

            if (l <= 15 && opd != null)
            {
                switch (opd.OpClass)
                {
                    case OpClass.Inherent:
                        ret = DoClassInherent(br, pc, l, prefixes, opd, opcode);
                        break;
                    case OpClass.Inherent_AA:
                        ret = DoClassInherent_AA(br, pc, l, prefixes, opd, opcode);
                        break;
                    case OpClass.Mem:
                        ret = DoClassMem(br, pc, l, prefixes, opd, opcode);
                        break;
                    case OpClass.MemImm:
                        ret = DoClassMemImm(br, pc, l, prefixes, opd, opcode);
                        break;
                    case OpClass.RegImm:
                        ret = DoClassRegImm(br, pc, l, prefixes, opd, opcode);
                        break;
                    case OpClass.AccDisp:
                        ret = DoClassAccDisp(br, pc, l, prefixes, opd, opcode);
                        break;
                    case OpClass.MemSeg:
                        ret = DoClassMemSeg(br, pc, l, prefixes, opd, opcode);
                        break;
                }
            }



            if (ret == null)
                ret = new DisRec2<uint> { Decoded = false, Length = l };

            return ret;
        }

        private readonly static string [] regs =
        {
            "al",
            "cl",
            "dl",
            "bl",
            "ah",
            "ch",
            "dh",
            "bh",
            "ax",
            "cx",
            "dx",
            "bx",
            "sp",
            "bp",
            "si",
            "di"
        };

        IEnumerable<DisRec2OperString_Base> GetReg(int rrr, bool width)
        {
            int ix = (rrr & 0x7) + (width?8:0);

            return new[] { new DisRec2OperString_String { Text = regs[ix] } };
        }

        private readonly static string[] segregs =
        {
            "es",
            "cs",
            "ss",
            "ds"
        };

        IEnumerable<DisRec2OperString_Base> GetSegReg(int ix)
        {
            return new[] { new DisRec2OperString_String { Text = segregs[ix & 3] } };

        }

        IEnumerable<DisRec2OperString_Base> GetData(BinaryReader br, bool w, ref ushort l, SymbolType symboltype)
        {
            if (w)
            {
                l+=2;
                return OperNum(br.ReadUInt16(), symboltype);
            } else
            {
                l++;
                return OperNum(br.ReadByte(), symboltype);
            }
        }

        IEnumerable<DisRec2OperString_Base> GetDisp(BinaryReader br, bool w, ref ushort l, Prefixes prefixes)
        {
            var ps = PointerPrefixStr(prefixes, Prefixes.DS);

            return OperStr("[").Concat(ps).Concat(GetData(br, w, ref l, SymbolType.Pointer)).Concat(OperStr("]"));
        }

        public IEnumerable<DisRec2OperString_Base> GetModRm(BinaryReader br, int mod, int r_m, bool w, bool ptrsz, Prefixes prefixes, ref ushort l)
        {
            int offs = 0;

            IEnumerable<DisRec2OperString_Base> ptr_sz_str;

            if (!ptrsz)
                ptr_sz_str = Enumerable.Empty<DisRec2OperString_Base>();
            else if (w)
                ptr_sz_str = OperStr("word ");
            else
                ptr_sz_str = OperStr("byte ");


            if (mod == 3)
            {
                return GetReg(r_m, w);
            }
            else if (mod == 00 && r_m == 6)
            {
                offs = br.ReadUInt16();
                l += 2;
                //special addressing mode
                return ptr_sz_str.Concat(PointerPrefixStr(prefixes, Prefixes.DS)).Concat(OperStr("[")).Concat(OperNum((UInt32)offs, SymbolType.Pointer)).Concat(OperStr("]"));
            }
            else
            {

                switch (mod)
                {
                    case 1:
                        offs = br.ReadSByte();
                        l++;
                        break;
                    case 2:
                        offs = br.ReadInt16();
                        l += 2;
                        break;

                }

                IEnumerable<DisRec2OperString_Base> offs_str;
                IEnumerable<DisRec2OperString_Base> ret;

                if (offs == 0)
                    offs_str = Enumerable.Empty<DisRec2OperString_Base>();
                else if (offs > 0)
                    offs_str = OperStr("+").Concat(OperNum((uint)offs, SymbolType.Offset)); 
                else
                    offs_str = OperStr("-").Concat(OperNum((uint)-offs, SymbolType.Offset));

                var pre = PointerPrefixStr(prefixes, Prefixes.DS);

                string m;

                switch (r_m)
                {
                    case 0:
                        m = "bx+si";
                        break;
                    case 1:
                        m = "bx+di";
                        break;
                    case 2:
                        m = "bp+si";
                        break;
                    case 3:
                        m = "bp+di";
                        break;
                    case 4:
                        m = "si";
                        break;
                    case 5:
                        m = "di";
                        break;
                    case 6:
                        m = "bp";
                        break;
                    case 7:
                        m = "bx";
                        break;
                    default:
                        return null;
                }

                return ptr_sz_str.Concat(OperStr("[")).Concat(pre).Concat(OperStr(m)).Concat(offs_str).Concat(OperStr("]"));
            }

        }

        private IEnumerable<DisRec2OperString_Base> PointerPrefixStr(Prefixes prefixes, Prefixes def)
        {
            prefixes = prefixes & Prefixes.SEGS;
            if (prefixes == Prefixes.NONE || (prefixes & def) != 0)
               return Enumerable.Empty<DisRec2OperString_Base>();

            switch (prefixes)
            {
                case Prefixes.CS:
                    return OperStr("cs:");
                case Prefixes.DS:
                    return OperStr("ds:");
                case Prefixes.ES:
                    return OperStr("es:");
                case Prefixes.SS:
                    return OperStr("ss:");
                default:
                    return OperStr("??:");
            }
        }

        private DisRec2<UInt32> DoClassRegImm(BinaryReader br, UInt32 pc, ushort l, Prefixes prefixes, OpCodeDetails opd, byte opcode)
        {
            bool w = (opcode & 0x08) != 0;
            int rix = opcode & 0x7;

            var Ops = GetReg(rix, w).Concat(OperStr(",")).Concat(GetData(br, w, ref l, SymbolType.Immediate));

            return new DisRec2<UInt32>
            {
                Decoded = true,
                Length = (ushort)(l),
                Mnemonic = opd.Text,
                Operands = Ops
            };

        }

        private DisRec2<UInt32> DoClassAccDisp(BinaryReader br, UInt32 pc, ushort l, Prefixes prefixes, OpCodeDetails opd, byte opcode)
        {
            bool w = (opcode & 0x01) != 0;
            bool d = (opcode & 0x02) != 0;

            var Mem = GetDisp(br, true, ref l, prefixes);
            var Acc = w ? OperStr("ax") : OperStr("al");

            IEnumerable<DisRec2OperString_Base> Ops;
            
            if (d)
                Ops = Mem.Concat(OperStr(",")).Concat(Acc);
            else
                Ops = Acc.Concat(OperStr(",")).Concat(Mem);

            return new DisRec2<UInt32>
            {
                Decoded = true,
                Length = (ushort)(l),
                Mnemonic = opd.Text,
                Operands = Ops
            };

        }

        private DisRec2<UInt32> DoClassMem(BinaryReader br, UInt32 pc, ushort l, Prefixes prefixes, OpCodeDetails opd, byte opcode)
        {
            bool sdflag = (opcode & 0x02) != 0;
            bool w = (opcode & 0x01) != 0;

            byte modrm = br.ReadByte();

            int mod = (modrm & 0xC0) >> 6;
            int rrr = (modrm & 0x38) >> 3;
            int r_m = modrm & 0x7;

            var Op1 = GetReg(rrr, w);
            if (Op1 == null)
                return null;

            var Op2 = GetModRm(br, mod, r_m, w, false, prefixes, ref l);
            if (Op2 == null)
                return null;

            IEnumerable<DisRec2OperString_Base> Ops;
            if (sdflag)
                Ops = Op1.Concat(OperStr(",")).Concat(Op2);
            else
                Ops = Op2.Concat(OperStr(",")).Concat(Op1);

            return new DisRec2<UInt32>
            {
                Decoded = true,
                Length = (ushort)(l + 1),
                Mnemonic = opd.Text,
                Operands = Ops
            };
        }

        private DisRec2<UInt32> DoClassMemSeg(BinaryReader br, UInt32 pc, ushort l, Prefixes prefixes, OpCodeDetails opd, byte opcode)
        {
            bool sdflag = (opcode & 0x02) != 0;

            byte modrm = br.ReadByte();

            int mod = (modrm & 0xC0) >> 6;
            int rrr = (modrm & 0x38) >> 3;
            int r_m = modrm & 0x7;

            if (rrr >= 4)
                return null;
            var Op1 = GetSegReg(rrr);
            if (Op1 == null)
                return null;

            var Op2 = GetModRm(br, mod, r_m, true, false, prefixes, ref l);
            if (Op2 == null)
                return null;

            IEnumerable<DisRec2OperString_Base> Ops;
            if (sdflag)
                Ops = Op1.Concat(OperStr(",")).Concat(Op2);
            else
                Ops = Op2.Concat(OperStr(",")).Concat(Op1);

            return new DisRec2<UInt32>
            {
                Decoded = true,
                Length = (ushort)(l + 1),
                Mnemonic = opd.Text,
                Operands = Ops
            };
        }


        public DisRec2<UInt32> DoClassMemImm(BinaryReader br, UInt32 pc, ushort l, Prefixes prefixes, OpCodeDetails opd, byte opcode)
        {
            bool w = (opcode & 0x01) != 0;

            byte modrm = br.ReadByte();

            int mod = (modrm & 0xC0) >> 6;
            int rrr = (modrm & 0x38) >> 3;
            if (rrr != 0)
                return null;
            int r_m = modrm & 0x7;


            var Op2 = GetModRm(br, mod, r_m, w, true, prefixes, ref l);
            if (Op2 == null)
                return null;

            var Op1 = GetData(br, w, ref l, SymbolType.Immediate);
            if (Op1 == null)
                return null;

            var Ops = Op2.Concat(OperStr(",")).Concat(Op1);

            return new DisRec2<UInt32>
            {
                Decoded = true,
                Length = (ushort)(l + 1),
                Mnemonic = opd.Text,
                Operands = Ops
            };
        }




        public DisRec2<UInt32> DoClassInherent(BinaryReader br, UInt32 pc, ushort l, Prefixes prefixes, OpCodeDetails opd, byte opcode)
        {
            return new DisRec2<uint>
            {
                Decoded = true,
                Length = l,
                Mnemonic = opd.Text
            };
        }

        public DisRec2<UInt32> DoClassInherent_AA(BinaryReader br, UInt32 pc, ushort l, Prefixes prefixes, OpCodeDetails opd, byte opcode)
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

        private static IEnumerable<DisRec2OperString_Base> OperStr(string str)
        {
            if (str == null)
                return null;
            else
                return new[] { new DisRec2OperString_String { Text = str } };
        }

        private static IEnumerable<DisRec2OperString_Base> OperNum(UInt32 num, SymbolType type)
        {
            return new[] { new DisRec2OperString_Number { Number = num, SymbolType = type } };
        }


    }
}
