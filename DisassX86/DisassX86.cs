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
            ES = 1,
            CS = 2,
            SS = 4,
            DS = 8,
            REP = 16,
            REPNZ = 32,
            LOCK = 64,
            SEGS = Prefixes.ES | Prefixes.CS | Prefixes.SS | Prefixes.DS,
            B4 = Prefixes.REP | Prefixes.REPNZ | Prefixes.LOCK
        };

        public enum OpClass {
            Prefix,
            Inherent,
            Inherent_AA,
            Mem,
            Mem2R,
            MemW,
            MemImm,
            MemImmOpc_DP,
            MemOpc_S,
            MemOpc_S_Rot1,
            MemOpc_S_RotCL,
            MemImmOpc_Rot,
            AccDisp,
            RegImm,
            Reg_16,
            Seg_16,
            MemSeg,
            AccImm,
            CallNear,
            CallFar,
            String,
            InOut,
            Int,
            Jcc,
            J_short,
            ImmPush,
            RetImm
        }


        public record OpCodeDetails
        {
            public byte And { get; init; }
            public byte Xor { get; init; }

            public Prefixes Pref { get; init; }
            public string Text { get; init; }
            public OpClass OpClass { get; init; }

        }

        /// <summary>
        /// used in the MemImm mode
        /// </summary>
        private readonly static string[] opcode_extensions_dp =
        {
            "add",
            "or",
            "adc",
            "???",
            "and",
            "???",
            "???",
            "cmp"
        };

        private readonly static string[] opcode_extensions_s =
{
            "inc",
            "dec",
            "call",
            "call",
            "jmp",
            "jmp",
            "push",
            "???"
        };

        private readonly static string[] opcode_extensions_s_divmul =
{
            "pop",
            "???",
            "not",
            "neg",
            "mul",
            "imul",
            "div",
            "idiv"
        };

        private readonly static string[] opcode_extensions_s_rot =
        {
            "rol",
            "ror",
            "rcl",
            "rcr",
            "???",
            "???",
            "???",
            "???"
        };


        private readonly static string[] j_conds = {
            "o",
            "no",
            "b",
            "ae",
            "e",
            "ne",
            "be",
            "a",
            "s",
            "ns",
            "p",
            "np",
            "l",
            "ge",
            "le",
            "g"
        };

        public readonly static OpCodeDetails[] OpMap = new[]
        {
            // prefixes
            new OpCodeDetails {And = 0xFF, Xor = 0x26, OpClass = OpClass.Prefix, Text = "es:", Pref = Prefixes.ES},
            new OpCodeDetails {And = 0xFF, Xor = 0x2E, OpClass = OpClass.Prefix, Text = "cs:", Pref = Prefixes.CS},
            new OpCodeDetails {And = 0xFF, Xor = 0x36, OpClass = OpClass.Prefix, Text = "ss:", Pref = Prefixes.SS},
            new OpCodeDetails {And = 0xFF, Xor = 0x3e, OpClass = OpClass.Prefix, Text = "ds:", Pref = Prefixes.DS},

            new OpCodeDetails {And = 0xFF, Xor = 0xf2, OpClass = OpClass.Prefix, Text = "repnz:", Pref = Prefixes.REPNZ},
            new OpCodeDetails {And = 0xFF, Xor = 0xf3, OpClass = OpClass.Prefix, Text = "repz", Pref = Prefixes.REP},
            new OpCodeDetails {And = 0xFF, Xor = 0xf0, OpClass = OpClass.Prefix, Text = "lock", Pref = Prefixes.LOCK},

            // inherents

            new OpCodeDetails {And = 0xFF, Xor = 0x37, OpClass = OpClass.Inherent, Text = "aaa"},
            new OpCodeDetails {And = 0xFF, Xor = 0xD5, OpClass = OpClass.Inherent_AA, Text = "aad"},
            new OpCodeDetails {And = 0xFF, Xor = 0xD4, OpClass = OpClass.Inherent_AA, Text = "aam"},
            new OpCodeDetails {And = 0xFF, Xor = 0x3F, OpClass = OpClass.Inherent, Text = "aas"},
            new OpCodeDetails {And = 0xFF, Xor = 0x27, OpClass = OpClass.Inherent, Text = "daa"},
            new OpCodeDetails {And = 0xFF, Xor = 0x2F, OpClass = OpClass.Inherent, Text = "das"},
            new OpCodeDetails {And = 0xFF, Xor = 0xC3, OpClass = OpClass.Inherent, Text = "ret"},
            new OpCodeDetails {And = 0xFF, Xor = 0xCB, OpClass = OpClass.Inherent, Text = "retf"},
            new OpCodeDetails {And = 0xFF, Xor = 0xCE, OpClass = OpClass.Inherent, Text = "into"},
            new OpCodeDetails {And = 0xFF, Xor = 0xCF, OpClass = OpClass.Inherent, Text = "iret"},

            new OpCodeDetails {And = 0xFF, Xor = 0x90, OpClass = OpClass.Inherent, Text = "nop"},
            new OpCodeDetails {And = 0xFF, Xor = 0x98, OpClass = OpClass.Inherent, Text = "cbw"},
            new OpCodeDetails {And = 0xFF, Xor = 0x99, OpClass = OpClass.Inherent, Text = "cwd"},
            new OpCodeDetails {And = 0xFF, Xor = 0x9C, OpClass = OpClass.Inherent, Text = "pushf"},
            new OpCodeDetails {And = 0xFF, Xor = 0x9D, OpClass = OpClass.Inherent, Text = "popf"},
            new OpCodeDetails {And = 0xFF, Xor = 0x9F, OpClass = OpClass.Inherent, Text = "lahf"},

            new OpCodeDetails {And = 0xFF, Xor = 0xF4, OpClass = OpClass.Inherent, Text = "hlt"},
            new OpCodeDetails {And = 0xFF, Xor = 0xF5, OpClass = OpClass.Inherent, Text = "cmc"},
            new OpCodeDetails {And = 0xFF, Xor = 0xF8, OpClass = OpClass.Inherent, Text = "clc"},
            new OpCodeDetails {And = 0xFF, Xor = 0xFA, OpClass = OpClass.Inherent, Text = "cli"},
            new OpCodeDetails {And = 0xFF, Xor = 0xFC, OpClass = OpClass.Inherent, Text = "cld"},


            //DP instructions
            new OpCodeDetails {And = 0xFC, Xor = 0x80, OpClass = OpClass.MemImmOpc_DP, Text = "!!!"},

            new OpCodeDetails {And = 0xFC, Xor = 0x10, OpClass = OpClass.Mem, Text = "adc"},
            new OpCodeDetails {And = 0xFE, Xor = 0x14, OpClass = OpClass.AccImm, Text = "adc"},

            new OpCodeDetails {And = 0xFC, Xor = 0x00, OpClass = OpClass.Mem, Text = "add"},
            new OpCodeDetails {And = 0xFE, Xor = 0x04, OpClass = OpClass.AccImm, Text = "add"},

            new OpCodeDetails {And = 0xFC, Xor = 0x20, OpClass = OpClass.Mem, Text = "and"},
            new OpCodeDetails {And = 0xFE, Xor = 0x24, OpClass = OpClass.AccImm, Text = "and"},

            new OpCodeDetails {And = 0xFC, Xor = 0x38, OpClass = OpClass.Mem, Text = "cmp"},
            new OpCodeDetails {And = 0xFE, Xor = 0x3C, OpClass = OpClass.AccImm, Text = "cmp"},

            new OpCodeDetails {And = 0xFC, Xor = 0x08, OpClass = OpClass.Mem, Text = "or"},
            new OpCodeDetails {And = 0xFE, Xor = 0x0C, OpClass = OpClass.AccImm, Text = "or"},


            new OpCodeDetails {And = 0xFF, Xor = 0x62, OpClass = OpClass.MemW, Text = "bound"},

            
            // effective address instructions

            new OpCodeDetails {And = 0xFF, Xor = 0xC5, OpClass = OpClass.Mem2R, Text = "lds"},

            new OpCodeDetails {And = 0xFF, Xor = 0x8D, OpClass = OpClass.Mem2R, Text = "lea"},

            new OpCodeDetails {And = 0xFF, Xor = 0xC4, OpClass = OpClass.Mem2R, Text = "les"},


            //Single MemOpc Instructions

            new OpCodeDetails {And = 0xFE, Xor = 0xFE, OpClass = OpClass.MemOpc_S, Text = "!!!"}, // dec/inc/call/jmp etc

            new OpCodeDetails {And = 0xF8, Xor = 0x48, OpClass = OpClass.Reg_16, Text = "dec"},

            new OpCodeDetails {And = 0xFE, Xor = 0xF6, OpClass = OpClass.MemOpc_S, Text = "!!!"},//DIV/IDIV/IMUL/NEG

            new OpCodeDetails {And = 0xF8, Xor = 0x40, OpClass = OpClass.Reg_16, Text = "inc"},



            //CALL

            // also picked up by FF/FE -> above dec/inc/etc

            new OpCodeDetails {And = 0xFF, Xor = 0xE8, OpClass = OpClass.CallNear, Text = "call"},
            new OpCodeDetails {And = 0xFF, Xor = 0x9A, OpClass = OpClass.CallFar, Text = "call"},

            //Jumps

            new OpCodeDetails {And = 0xF0, Xor = 0x70, OpClass = OpClass.Jcc, Text = "j"},

            new OpCodeDetails {And = 0xFF, Xor = 0xE3, OpClass = OpClass.J_short, Text = "jcxz"},

            new OpCodeDetails {And = 0xFF, Xor = 0xEB, OpClass = OpClass.J_short, Text = "jmp"},

            new OpCodeDetails {And = 0xFF, Xor = 0xE2, OpClass = OpClass.J_short, Text = "loop"},
            new OpCodeDetails {And = 0xFF, Xor = 0xE1, OpClass = OpClass.J_short, Text = "loope"},
            new OpCodeDetails {And = 0xFF, Xor = 0xE0, OpClass = OpClass.J_short, Text = "loopne"},


            new OpCodeDetails {And = 0xFF, Xor = 0xE9, OpClass = OpClass.CallNear, Text = "jmp"},
            new OpCodeDetails {And = 0xFF, Xor = 0xEA, OpClass = OpClass.CallFar, Text = "jmp"},

            //MOVs

            new OpCodeDetails {And = 0xFC, Xor = 0x88, OpClass = OpClass.Mem, Text = "mov"},
            new OpCodeDetails {And = 0xFE, Xor = 0xC6, OpClass = OpClass.MemImm, Text = "mov"},
            new OpCodeDetails {And = 0xF0, Xor = 0xB0, OpClass = OpClass.RegImm, Text = "mov"},
            new OpCodeDetails {And = 0xFC, Xor = 0xA0, OpClass = OpClass.AccDisp, Text = "mov"},
            new OpCodeDetails {And = 0xFD, Xor = 0x8C, OpClass = OpClass.MemSeg, Text = "mov"},

            // strings
            new OpCodeDetails {And = 0xFE, Xor = 0xA6, OpClass = OpClass.String, Text = "cmps"},
            new OpCodeDetails {And = 0xFE, Xor = 0xAC, OpClass = OpClass.String, Text = "lods"},
            new OpCodeDetails {And = 0xFE, Xor = 0xA4, OpClass = OpClass.String, Text = "movs"},

            new OpCodeDetails {And = 0xFE, Xor = 0x6C, OpClass = OpClass.String, Text = "ins"},
            new OpCodeDetails {And = 0xFE, Xor = 0x6E, OpClass = OpClass.String, Text = "outs"},


            //In/Out
            new OpCodeDetails {And = 0xF4, Xor = 0xE4, OpClass = OpClass.InOut, Text = "???"},

            //Int
            new OpCodeDetails {And = 0xFE, Xor = 0xCC, OpClass = OpClass.Int, Text = "int"},

            //Push/Pop
            new OpCodeDetails {And = 0xF8, Xor = 0x58, OpClass = OpClass.Reg_16, Text = "pop"},
            new OpCodeDetails {And = 0xC7, Xor = 0x07, OpClass = OpClass.Seg_16, Text = "pop"},
            new OpCodeDetails {And = 0xFF, Xor = 0x8F, OpClass = OpClass.MemOpc_S, Text = "!!!"},

            new OpCodeDetails {And = 0xF8, Xor = 0x50, OpClass = OpClass.Reg_16, Text = "push"},
            new OpCodeDetails {And = 0xC7, Xor = 0x06, OpClass = OpClass.Seg_16, Text = "push"},

            new OpCodeDetails {And = 0xFD, Xor = 0x68, OpClass = OpClass.ImmPush, Text = "push"},

            // rotates

            new OpCodeDetails {And = 0xFE, Xor = 0xD0, OpClass = OpClass.MemOpc_S_Rot1, Text = "!!!"},
            new OpCodeDetails {And = 0xFE, Xor = 0xD2, OpClass = OpClass.MemOpc_S_RotCL, Text = "!!!"},
            new OpCodeDetails {And = 0xFE, Xor = 0xC0, OpClass = OpClass.MemImmOpc_Rot, Text = "!!!"},

            // RetImm

            new OpCodeDetails {And = 0xF7, Xor = 0xC2, OpClass = OpClass.RetImm, Text = "ret"}

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
                    case OpClass.Mem2R:
                        ret = DoClassMem2R(br, pc, l, prefixes, opd, opcode);
                        break;
                    case OpClass.MemW:
                        ret = DoClassMemW(br, pc, l, prefixes, opd, opcode);
                        break;
                    case OpClass.MemImm:
                        ret = DoClassMemImm(br, pc, l, prefixes, opd, opcode);
                        break;
                    case OpClass.MemImmOpc_DP:
                        ret = DoClassMemImmOpc_DP(br, pc, l, prefixes, opd, opcode);
                        break;
                    case OpClass.MemOpc_S:
                        ret = DoClassMemOpc_S(br, pc, l, prefixes, opd, opcode);
                        break;
                    case OpClass.MemOpc_S_Rot1:
                        ret = DoClassMemOpc_S_Rot1(br, pc, l, prefixes, opd, opcode);
                        break;
                    case OpClass.MemOpc_S_RotCL:
                        ret = DoClassMemOpc_S_RotCL(br, pc, l, prefixes, opd, opcode);
                        break;
                    case OpClass.MemImmOpc_Rot:
                        ret = DoClassMemImmOpc_Rot(br, pc, l, prefixes, opd, opcode);
                        break;
                    case OpClass.RegImm:
                        ret = DoClassRegImm(br, pc, l, prefixes, opd, opcode);
                        break;
                    case OpClass.Reg_16:
                        ret = DoClassReg_16(br, pc, l, prefixes, opd, opcode);
                        break;
                    case OpClass.Seg_16:
                        ret = DoClassSeg_16(br, pc, l, prefixes, opd, opcode);
                        break;
                    case OpClass.AccDisp:
                        ret = DoClassAccDisp(br, pc, l, prefixes, opd, opcode);
                        break;
                    case OpClass.MemSeg:
                        ret = DoClassMemSeg(br, pc, l, prefixes, opd, opcode);
                        break;
                    case OpClass.AccImm:
                        ret = DoClassAccImm(br, pc, l, prefixes, opd, opcode);
                        break;
                    case OpClass.CallFar:
                        ret = DoClassCallFar(br, pc, l, prefixes, opd, opcode);
                        break;
                    case OpClass.CallNear:
                        ret = DoClassCallNear(br, pc, l, prefixes, opd, opcode);
                        break;
                    case OpClass.String:
                        ret = DoClassString(br, pc, l, prefixes, opd, opcode);
                        break;
                    case OpClass.InOut:
                        ret = DoClassInOut(br, pc, l, prefixes, opd, opcode);
                        break;
                    case OpClass.Int:
                        ret = DoClassInt(br, pc, l, prefixes, opd, opcode);
                        break;
                    case OpClass.Jcc:
                        ret = DoClassJcc(br, pc, l, prefixes, opd, opcode);
                        break;
                    case OpClass.J_short:
                        ret = DoClassJ_short(br, pc, l, prefixes, opd, opcode);
                        break;
                    case OpClass.ImmPush:
                        ret = DoClassImmPush(br, pc, l, prefixes, opd, opcode);
                        break;
                    case OpClass.RetImm:
                        ret = DoClassRetImm(br, pc, l, prefixes, opd, opcode);
                        break;
                }
            }



            if (ret == null)
                ret = new DisRec2<uint> { Decoded = false, Length = l };
            else
            {
                //check for b4 prefixes
                var b4flags = string.Join(" ", 
                    (prefixes & Prefixes.B4)
                    .GetFlags()
                    .Cast<Prefixes>()
                    .Where(o => o != Prefixes.NONE)
                    .Select(o => o.ToString().ToLower())
                    );
                if (!string.IsNullOrEmpty(b4flags))
                {
                    ret = new DisRec2<UInt32>
                    {
                        Decoded = ret.Decoded,
                        Hints = ret.Hints,
                        Length = ret.Length,
                        Mnemonic = $"{b4flags} {ret.Mnemonic}",
                        Operands = ret.Operands
                    };
                }
            }
            

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

        IEnumerable<DisRec2OperString_Base> GetData32(BinaryReader br, ref ushort l, SymbolType symboltype)
        {
            l += 4;
            return OperNum(br.ReadUInt32(), symboltype);
        }

        IEnumerable<DisRec2OperString_Base> GetRelDisp(BinaryReader br, bool w, ref ushort l, UInt32 pc)
        {
            int offs;
            if (w) {
                offs = br.ReadInt16();
                l += 2;
            } else
            {
                offs = br.ReadSByte();
                l += 1;
            }
            return OperNum((uint)(l + pc + offs) & 0xFFFF, SymbolType.Pointer);
        }

        IEnumerable<DisRec2OperString_Base> GetDisp(BinaryReader br, bool w, ref ushort l, Prefixes prefixes)
        {
            var ps = PointerPrefixStr(prefixes, Prefixes.DS);

            return OperStr("[").Concat(ps).Concat(GetData(br, w, ref l, SymbolType.Pointer)).Concat(OperStr("]"));
        }

        public IEnumerable<DisRec2OperString_Base> GetModRm(BinaryReader br, int mod, int r_m, bool w, bool ptrsz, Prefixes prefixes, ref ushort l, bool call = false)
        {
            int offs = 0;

            IEnumerable<DisRec2OperString_Base> ptr_sz_str;

            if (!ptrsz)
                ptr_sz_str = Enumerable.Empty<DisRec2OperString_Base>();
            else if (call)
                ptr_sz_str = OperStr("far ");
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
                return ptr_sz_str.Concat(OperStr("[")).Concat(PointerPrefixStr(prefixes, Prefixes.DS)).Concat(OperNum((UInt32)offs, SymbolType.Pointer)).Concat(OperStr("]"));
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
        private DisRec2<UInt32> DoClassReg_16(BinaryReader br, UInt32 pc, ushort l, Prefixes prefixes, OpCodeDetails opd, byte opcode)
        {
            int rix = opcode & 0x7;

            var Ops = GetReg(rix, true);

            return new DisRec2<UInt32>
            {
                Decoded = true,
                Length = (ushort)(l),
                Mnemonic = opd.Text,
                Operands = Ops
            };

        }

        private DisRec2<UInt32> DoClassSeg_16(BinaryReader br, UInt32 pc, ushort l, Prefixes prefixes, OpCodeDetails opd, byte opcode)
        {
            int rix = (opcode & 0x38) >> 3;

            var Ops = GetSegReg(rix);

            return new DisRec2<UInt32>
            {
                Decoded = true,
                Length = (ushort)(l),
                Mnemonic = opd.Text,
                Operands = Ops
            };

        }


        private DisRec2<UInt32> DoClassAccImm(BinaryReader br, UInt32 pc, ushort l, Prefixes prefixes, OpCodeDetails opd, byte opcode)
        {
            bool w = (opcode & 0x01) != 0;

            string r = (w) ? "ax" : "al";
            var imm = GetData(br, w, ref l, SymbolType.Immediate);

            return new DisRec2<UInt32>
            {
                Decoded = true,
                Length = (ushort)(l),
                Mnemonic = opd.Text,
                Operands = OperStr(r).Concat(OperStr(",")).Concat(imm)
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
            bool dflag = (opcode & 0x02) != 0;
            bool w = (opcode & 0x01) != 0;

            return DoClassMem_int(br, pc, l, prefixes, opd, opcode, dflag, w);
        }

        private DisRec2<UInt32> DoClassMemW(BinaryReader br, UInt32 pc, ushort l, Prefixes prefixes, OpCodeDetails opd, byte opcode)
        {
            return DoClassMem_int(br, pc, l, prefixes, opd, opcode, true, true);
        }

        private DisRec2<UInt32> DoClassMem2R(BinaryReader br, UInt32 pc, ushort l, Prefixes prefixes, OpCodeDetails opd, byte opcode)
        {
            return DoClassMem_int(br, pc, l, prefixes, opd, opcode, true, true);
        }


        private DisRec2<UInt32> DoClassMem_int(BinaryReader br, UInt32 pc, ushort l, Prefixes prefixes, OpCodeDetails opd, byte opcode, bool dflag, bool w) { 
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
            if (dflag)
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

        public DisRec2<UInt32> DoClassMemImmOpc_DP(BinaryReader br, UInt32 pc, ushort l, Prefixes prefixes, OpCodeDetails opd, byte opcode)
        {
            bool w = (opcode & 0x01) != 0;
            bool s = (opcode & 0x02) != 0;

            byte modrm = br.ReadByte();

            int mod = (modrm & 0xC0) >> 6;
            int rrr = (modrm & 0x38) >> 3;

            string opcode_over = opcode_extensions_dp[rrr];

            int r_m = modrm & 0x7;


            var Op2 = GetModRm(br, mod, r_m, w, true, prefixes, ref l);
            if (Op2 == null)
                return null;

            var Op1 = GetData(br, w ^ s, ref l, SymbolType.Immediate);
            if (Op1 == null)
                return null;

            var Ops = Op2.Concat(OperStr(",")).Concat(Op1);

            return new DisRec2<UInt32>
            {
                Decoded = true,
                Length = (ushort)(l + 1),
                Mnemonic = opcode_over,
                Operands = Ops
            };
        }

        public DisRec2<UInt32> DoClassMemImmOpc_Rot(BinaryReader br, UInt32 pc, ushort l, Prefixes prefixes, OpCodeDetails opd, byte opcode)
        {
            bool w = (opcode & 0x01) != 0;

            byte modrm = br.ReadByte();

            int mod = (modrm & 0xC0) >> 6;
            int rrr = (modrm & 0x38) >> 3;

            string opcode_over = opcode_extensions_s_rot[rrr];

            int r_m = modrm & 0x7;


            var Op2 = GetModRm(br, mod, r_m, w, true, prefixes, ref l);
            if (Op2 == null)
                return null;

            var Op1 = GetData(br, false, ref l, SymbolType.Immediate);
            if (Op1 == null)
                return null;

            var Ops = Op2.Concat(OperStr(",")).Concat(Op1);

            return new DisRec2<UInt32>
            {
                Decoded = true,
                Length = (ushort)(l + 1),
                Mnemonic = opcode_over,
                Operands = Ops
            };
        }


        public DisRec2<UInt32> DoClassMemOpc_S(BinaryReader br, UInt32 pc, ushort l, Prefixes prefixes, OpCodeDetails opd, byte opcode)
        {
            bool w = (opcode & 0x01) != 0;
            bool divmul = (opcode == 0x8F) || (opcode & 0x08) == 0;

            byte modrm = br.ReadByte();

            int mod = (modrm & 0xC0) >> 6;
            int rrr = (modrm & 0x38) >> 3;
            bool call = !divmul && rrr >= 2 && rrr <= 4;
            bool far = (rrr & 0x01) != 0;

            string opcode_over = divmul? opcode_extensions_s_divmul[rrr]:opcode_extensions_s[rrr];

            int r_m = modrm & 0x7;


            var Op2 = GetModRm(br, mod, r_m, w, call ? far : true, prefixes, ref l, call : call );
            if (Op2 == null)
                return null;


            return new DisRec2<UInt32>
            {
                Decoded = true,
                Length = (ushort)(l + 1),
                Mnemonic = opcode_over,
                Operands = Op2
            };
        }

        public DisRec2<UInt32> DoClassMemOpc_S_Rot1(BinaryReader br, UInt32 pc, ushort l, Prefixes prefixes, OpCodeDetails opd, byte opcode)
        {
            bool w = (opcode & 0x01) != 0;

            byte modrm = br.ReadByte();

            int mod = (modrm & 0xC0) >> 6;
            int rrr = (modrm & 0x38) >> 3;

            string opcode_over = opcode_extensions_s_rot[rrr];

            int r_m = modrm & 0x7;


            var Op2 = GetModRm(br, mod, r_m, w, false, prefixes, ref l, false).Concat(OperStr(",1"));
            if (Op2 == null)
                return null;


            return new DisRec2<UInt32>
            {
                Decoded = true,
                Length = (ushort)(l + 1),
                Mnemonic = opcode_over,
                Operands = Op2
            };
        }

        public DisRec2<UInt32> DoClassMemOpc_S_RotCL(BinaryReader br, UInt32 pc, ushort l, Prefixes prefixes, OpCodeDetails opd, byte opcode)
        {
            bool w = (opcode & 0x01) != 0;

            byte modrm = br.ReadByte();

            int mod = (modrm & 0xC0) >> 6;
            int rrr = (modrm & 0x38) >> 3;

            string opcode_over = opcode_extensions_s_rot[rrr];

            int r_m = modrm & 0x7;


            var Op2 = GetModRm(br, mod, r_m, w, false, prefixes, ref l, false).Concat(OperStr(",cl"));
            if (Op2 == null)
                return null;


            return new DisRec2<UInt32>
            {
                Decoded = true,
                Length = (ushort)(l + 1),
                Mnemonic = opcode_over,
                Operands = Op2
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

        public DisRec2<UInt32> DoClassCallNear(BinaryReader br, UInt32 pc, ushort l, Prefixes prefixes, OpCodeDetails opd, byte opcode)
        {
            var disp = OperStr("near ").Concat(GetRelDisp(br, true, ref l, pc));

            return new DisRec2<uint>
            {
                Decoded = true,
                Length = l,
                Mnemonic = opd.Text,
                Operands = disp
            };

        }

        public DisRec2<UInt32> DoClassCallFar(BinaryReader br, UInt32 pc, ushort l, Prefixes prefixes, OpCodeDetails opd, byte opcode)
        {
            var disp = OperStr("far ").Concat(GetData32(br, ref l, SymbolType.Pointer));

            return new DisRec2<uint>
            {
                Decoded = true,
                Length = l,
                Mnemonic = opd.Text,
                Operands = disp
            };

        }


        public DisRec2<UInt32> DoClassString(BinaryReader br, UInt32 pc, ushort l, Prefixes prefixes, OpCodeDetails opd, byte opcode)
        {
            bool w = (opcode & 0x01) != 0;

            var pre = string.Join(" ",
                (prefixes & Prefixes.SEGS)
                .GetFlags()
                .Cast<Prefixes>()
                .Where(o => o != Prefixes.NONE)
                .Select(o => o.ToString().ToLower())
                );

            var mne = string.Join(" ", new[] { pre, $"{opd.Text}{(w ? "w" : "")}" });

            return new DisRec2<UInt32>
            {
                Decoded = true,
                Length = l,
                Mnemonic = mne
            };

        }

        public DisRec2<UInt32> DoClassInOut(BinaryReader br, UInt32 pc, ushort l, Prefixes prefixes, OpCodeDetails opd, byte opcode)
        {
            bool w = (opcode & 0x01) != 0;
            bool d = (opcode & 0x02) != 0;
            bool dx = (opcode & 0x08) != 0;

            var OpP = dx ? OperStr("dx") : GetData(br, false, ref l, SymbolType.Port);

            var OpAcc = OperStr((w) ? "ax" : "al");

            var Ops = d ? OpP.Concat(OperStr(",")).Concat(OpAcc) : OpAcc.Concat(OperStr(",")).Concat(OpP);

            return new DisRec2<UInt32>
            {
                Decoded = true,
                Length = l,
                Mnemonic = d ? "out" : "in",
                Operands = Ops
            };

        }

        public DisRec2<UInt32> DoClassInt(BinaryReader br, UInt32 pc, ushort l, Prefixes prefixes, OpCodeDetails opd, byte opcode)
        {
            IEnumerable<DisRec2OperString_Base> Num;

            if ((opcode & 0x01) == 0)
                Num = OperNum(3, SymbolType.ServiceCall);
            else
                Num = GetData(br, false, ref l, SymbolType.ServiceCall);

            return new DisRec2<UInt32>
            {
                Decoded = true,
                Length = l,
                Mnemonic = opd.Text,
                Operands = Num
            };

        }

        public DisRec2<UInt32> DoClassJcc(BinaryReader br, UInt32 pc, ushort l, Prefixes prefixes, OpCodeDetails opd, byte opcode)
        {
            int ccix = opcode & 0xF;

            var Ops = GetRelDisp(br, false, ref l, pc);

            return new DisRec2<UInt32>
            {
                Decoded = true,
                Length = l,
                Mnemonic = $"{opd.Text}{j_conds[ccix]}",
                Operands = Ops
            };

        }
        public DisRec2<UInt32> DoClassRetImm(BinaryReader br, UInt32 pc, ushort l, Prefixes prefixes, OpCodeDetails opd, byte opcode)
        {
            bool f = (opcode & 0x08) != 0;

            var Ops = OperNum(br.ReadUInt16(), SymbolType.Offset);
            l += 2;

            return new DisRec2<UInt32>
            {
                Decoded = true,
                Length = l,
                Mnemonic = $"{opd.Text}{(f?"f":"")}",
                Operands = Ops
            };

        }

        public DisRec2<UInt32> DoClassJ_short(BinaryReader br, UInt32 pc, ushort l, Prefixes prefixes, OpCodeDetails opd, byte opcode)
        {

            var Ops = OperStr("short ").Concat(GetRelDisp(br, false, ref l, pc));

            return new DisRec2<UInt32>
            {
                Decoded = true,
                Length = l,
                Mnemonic = opd.Text,
                Operands = Ops
            };

        }

        public DisRec2<UInt32> DoClassImmPush(BinaryReader br, UInt32 pc, ushort l, Prefixes prefixes, OpCodeDetails opd, byte opcode)
        {
            bool w = (opcode & 0x02) == 0;

            IEnumerable<DisRec2OperString_Base> Ops;
            if (w)
            {
                Ops = OperStr("word ").Concat(OperNum(br.ReadUInt16(), SymbolType.Immediate));
                l += 2;
            } else
            {
                Ops = OperStr("byte ").Concat(OperNum(br.ReadByte(), SymbolType.Immediate));
                l ++;
            }

            return new DisRec2<UInt32>
            {
                Decoded = true,
                Length = l,
                Mnemonic = opd.Text,
                Operands = Ops
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
