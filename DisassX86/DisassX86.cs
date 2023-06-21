using DisassShared;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;

//TODO: There's a lot of cut and paste code - try and simplify/factor out
//TODO: there's some opc overrides set as "???" - make them null and return unknown opcode

namespace DisassX86
{
    public class DisassX86 : IDisAss
    {

        private API m_API { get; init; }

        public static AddressX86Factory AddressFactory2 => new AddressX86Factory();

        public IDisassAddressFactory AddressFactory => AddressFactory2;

        public DisassX86(API api)
        {
            m_API = api;
        }


        [Flags]
        public enum API
        {
            // instruction "match" flags
            match_x86 = 1,
            match_186 = 2,
            match_286 = 4,
            match_386 = 8,

            // match any processor
            match_all = match_x86 | match_186 | match_286 | match_386,

            // 32 bit defaults
            mode_32bit = 32,

            // API levels
            cpu_x86 = match_x86,
            cpu_186 = match_x86 | match_186,
            cpu_286 = match_x86 | match_186 | match_286,
            cpu_386 = match_x86 | match_186 | match_286 | match_386,
            cpu_386_32 = match_x86 | match_186 | match_286 | match_386 | mode_32bit
        }



        [Flags]
        public enum Prefixes
        {
            ES = 1,
            CS = 2,
            SS = 4,
            DS = 8,
            FS = 16,
            GS = 32,
            REP = 64,
            REPNZ = 128,
            LOCK = 256,
            WIDE_REG = 512,
            WIDE_OPER = 1024,
            NONE = 0,
            SEGS = Prefixes.ES | Prefixes.CS | Prefixes.SS | Prefixes.DS,
            B4 = Prefixes.REP | Prefixes.REPNZ | Prefixes.LOCK
        };

        bool Wide32Reg(Prefixes prefixes) => prefixes.HasFlag(Prefixes.WIDE_REG) ^ m_API.HasFlag(API.mode_32bit);
        bool Wide32Oper(Prefixes prefixes) => prefixes.HasFlag(Prefixes.WIDE_OPER) ^ m_API.HasFlag(API.mode_32bit);


        public enum OpClass
        {
            Prefix,
            Inherent,
            Inherent_AA,
            Mem,
            Mem2R,
            MemW,
            MemImm,
            MemImmOpc_DP,
            MemOpc_S1,
            MemOpc_S2,
            MemOpc_S_Pop,
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
            RetImm,
            ImmImm
        }


        public record OpCodeDetails
        {
            public byte And { get; init; }
            public byte Xor { get; init; }

            public Prefixes Pref { get; init; }
            public string Text { get; init; }
            public OpClass OpClass { get; init; }

            public API MatchAPI { get; init; }
        }

        /// <summary>
        /// used in the MemImm mode
        /// </summary>
        private readonly static string[] opcode_extensions_dp =
        {
            "add",
            "or",
            "adc",
            "sbb",
            "and",
            "sub",
            "xor",
            "cmp"
        };

        private readonly static string[] opcode_extensions_s1 =
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

        private readonly static string[] opcode_extensions_s2 =
{
            "test",
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
            "shl",
            "shr",
            "???",
            "sar"
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
            new OpCodeDetails {And = 0xFF, Xor = 0x26, OpClass = OpClass.Prefix, Text = "es:", Pref = Prefixes.ES, MatchAPI = API.match_all},
            new OpCodeDetails {And = 0xFF, Xor = 0x2E, OpClass = OpClass.Prefix, Text = "cs:", Pref = Prefixes.CS, MatchAPI = API.match_all},
            new OpCodeDetails {And = 0xFF, Xor = 0x36, OpClass = OpClass.Prefix, Text = "ss:", Pref = Prefixes.SS, MatchAPI = API.match_all},
            new OpCodeDetails {And = 0xFF, Xor = 0x3e, OpClass = OpClass.Prefix, Text = "ds:", Pref = Prefixes.DS, MatchAPI = API.match_all},
            new OpCodeDetails {And = 0xFF, Xor = 0x64, OpClass = OpClass.Prefix, Text = "fs:", Pref = Prefixes.FS, MatchAPI = API.match_386},
            new OpCodeDetails {And = 0xFF, Xor = 0x65, OpClass = OpClass.Prefix, Text = "gs:", Pref = Prefixes.GS, MatchAPI = API.match_386},

            new OpCodeDetails {And = 0xFF, Xor = 0x66, OpClass = OpClass.Prefix, Text = null, Pref = Prefixes.WIDE_REG, MatchAPI = API.match_386},
            new OpCodeDetails {And = 0xFF, Xor = 0x67, OpClass = OpClass.Prefix, Text = null, Pref = Prefixes.WIDE_OPER, MatchAPI = API.match_386},


            new OpCodeDetails {And = 0xFF, Xor = 0xf2, OpClass = OpClass.Prefix, Text = "repnz:", Pref = Prefixes.REPNZ, MatchAPI = API.match_all},
            new OpCodeDetails {And = 0xFF, Xor = 0xf3, OpClass = OpClass.Prefix, Text = "repz", Pref = Prefixes.REP, MatchAPI = API.match_all},
            new OpCodeDetails {And = 0xFF, Xor = 0xf0, OpClass = OpClass.Prefix, Text = "lock", Pref = Prefixes.LOCK, MatchAPI = API.match_all},

            // inherents

            new OpCodeDetails {And = 0xFF, Xor = 0x37, OpClass = OpClass.Inherent, Text = "aaa", MatchAPI = API.match_all},
            new OpCodeDetails {And = 0xFF, Xor = 0xD5, OpClass = OpClass.Inherent_AA, Text = "aad", MatchAPI = API.match_all},
            new OpCodeDetails {And = 0xFF, Xor = 0xD4, OpClass = OpClass.Inherent_AA, Text = "aam", MatchAPI = API.match_all},
            new OpCodeDetails {And = 0xFF, Xor = 0x3F, OpClass = OpClass.Inherent, Text = "aas", MatchAPI = API.match_all},
            new OpCodeDetails {And = 0xFF, Xor = 0x27, OpClass = OpClass.Inherent, Text = "daa", MatchAPI = API.match_all},
            new OpCodeDetails {And = 0xFF, Xor = 0x2F, OpClass = OpClass.Inherent, Text = "das", MatchAPI = API.match_all},
            new OpCodeDetails {And = 0xFF, Xor = 0xC3, OpClass = OpClass.Inherent, Text = "ret", MatchAPI = API.match_all},
            new OpCodeDetails {And = 0xFF, Xor = 0xCB, OpClass = OpClass.Inherent, Text = "retf", MatchAPI = API.match_all},
            new OpCodeDetails {And = 0xFF, Xor = 0xCE, OpClass = OpClass.Inherent, Text = "into", MatchAPI = API.match_all},
            new OpCodeDetails {And = 0xFF, Xor = 0xCF, OpClass = OpClass.Inherent, Text = "iret", MatchAPI = API.match_all},

            new OpCodeDetails {And = 0xFF, Xor = 0x90, OpClass = OpClass.Inherent, Text = "nop", MatchAPI = API.match_all},
            new OpCodeDetails {And = 0xFF, Xor = 0x98, OpClass = OpClass.Inherent, Text = "cbw", MatchAPI = API.match_all},
            new OpCodeDetails {And = 0xFF, Xor = 0x99, OpClass = OpClass.Inherent, Text = "cwd", MatchAPI = API.match_all},
            new OpCodeDetails {And = 0xFF, Xor = 0x9B, OpClass = OpClass.Inherent, Text = "wait", MatchAPI = API.match_all},
            new OpCodeDetails {And = 0xFF, Xor = 0x9C, OpClass = OpClass.Inherent, Text = "pushf", MatchAPI = API.match_all},
            new OpCodeDetails {And = 0xFF, Xor = 0x9D, OpClass = OpClass.Inherent, Text = "popf", MatchAPI = API.match_all},
            new OpCodeDetails {And = 0xFF, Xor = 0x9E, OpClass = OpClass.Inherent, Text = "sahf", MatchAPI = API.match_all},
            new OpCodeDetails {And = 0xFF, Xor = 0x9F, OpClass = OpClass.Inherent, Text = "lahf", MatchAPI = API.match_all},

            new OpCodeDetails {And = 0xFF, Xor = 0xF4, OpClass = OpClass.Inherent, Text = "hlt", MatchAPI = API.match_all},
            new OpCodeDetails {And = 0xFF, Xor = 0xF5, OpClass = OpClass.Inherent, Text = "cmc", MatchAPI = API.match_all},
            new OpCodeDetails {And = 0xFF, Xor = 0xF8, OpClass = OpClass.Inherent, Text = "clc", MatchAPI = API.match_all},
            new OpCodeDetails {And = 0xFF, Xor = 0xF9, OpClass = OpClass.Inherent, Text = "stc", MatchAPI = API.match_all},
            new OpCodeDetails {And = 0xFF, Xor = 0xFA, OpClass = OpClass.Inherent, Text = "cli", MatchAPI = API.match_all},
            new OpCodeDetails {And = 0xFF, Xor = 0xFB, OpClass = OpClass.Inherent, Text = "sti", MatchAPI = API.match_all},
            new OpCodeDetails {And = 0xFF, Xor = 0xFC, OpClass = OpClass.Inherent, Text = "cld", MatchAPI = API.match_all},
            new OpCodeDetails {And = 0xFF, Xor = 0xFD, OpClass = OpClass.Inherent, Text = "std", MatchAPI = API.match_all},


            //DP instructions
            new OpCodeDetails {And = 0xFC, Xor = 0x80, OpClass = OpClass.MemImmOpc_DP, Text = "!!!", MatchAPI = API.match_all},

            new OpCodeDetails {And = 0xFC, Xor = 0x10, OpClass = OpClass.Mem, Text = "adc", MatchAPI = API.match_all},
            new OpCodeDetails {And = 0xFE, Xor = 0x14, OpClass = OpClass.AccImm, Text = "adc", MatchAPI = API.match_all},

            new OpCodeDetails {And = 0xFC, Xor = 0x00, OpClass = OpClass.Mem, Text = "add", MatchAPI = API.match_all},
            new OpCodeDetails {And = 0xFE, Xor = 0x04, OpClass = OpClass.AccImm, Text = "add", MatchAPI = API.match_all},

            new OpCodeDetails {And = 0xFC, Xor = 0x20, OpClass = OpClass.Mem, Text = "and", MatchAPI = API.match_all},
            new OpCodeDetails {And = 0xFE, Xor = 0x24, OpClass = OpClass.AccImm, Text = "and", MatchAPI = API.match_all},

            new OpCodeDetails {And = 0xFC, Xor = 0x38, OpClass = OpClass.Mem, Text = "cmp", MatchAPI = API.match_all},
            new OpCodeDetails {And = 0xFE, Xor = 0x3C, OpClass = OpClass.AccImm, Text = "cmp", MatchAPI = API.match_all},

            new OpCodeDetails {And = 0xFC, Xor = 0x08, OpClass = OpClass.Mem, Text = "or", MatchAPI = API.match_all},
            new OpCodeDetails {And = 0xFE, Xor = 0x0C, OpClass = OpClass.AccImm, Text = "or", MatchAPI = API.match_all},

            new OpCodeDetails {And = 0xFC, Xor = 0x18, OpClass = OpClass.Mem, Text = "sbb", MatchAPI = API.match_all},
            new OpCodeDetails {And = 0xFE, Xor = 0x1C, OpClass = OpClass.AccImm, Text = "sbb", MatchAPI = API.match_all},

            new OpCodeDetails {And = 0xFC, Xor = 0x28, OpClass = OpClass.Mem, Text = "sub", MatchAPI = API.match_all},
            new OpCodeDetails {And = 0xFE, Xor = 0x2C, OpClass = OpClass.AccImm, Text = "sub", MatchAPI = API.match_all},

            new OpCodeDetails {And = 0xFE, Xor = 0x84, OpClass = OpClass.Mem, Text = "test", MatchAPI = API.match_all},
            new OpCodeDetails {And = 0xFE, Xor = 0xA8, OpClass = OpClass.AccImm, Text = "test", MatchAPI = API.match_all},

            new OpCodeDetails {And = 0xFE, Xor = 0x86, OpClass = OpClass.Mem, Text = "xchg", MatchAPI = API.match_all},
            new OpCodeDetails {And = 0xF8, Xor = 0x90, OpClass = OpClass.Reg_16, Text = "xchg", MatchAPI = API.match_all},

            new OpCodeDetails {And = 0xFC, Xor = 0x30, OpClass = OpClass.Mem, Text = "xor", MatchAPI = API.match_all},
            new OpCodeDetails {And = 0xFE, Xor = 0x34, OpClass = OpClass.AccImm, Text = "xor", MatchAPI = API.match_all},


            new OpCodeDetails {And = 0xFF, Xor = 0x62, OpClass = OpClass.MemW, Text = "bound", MatchAPI = API.match_all},

            
            // effective address instructions

            new OpCodeDetails {And = 0xFF, Xor = 0xC5, OpClass = OpClass.Mem2R, Text = "lds", MatchAPI = API.match_all},

            new OpCodeDetails {And = 0xFF, Xor = 0x8D, OpClass = OpClass.Mem2R, Text = "lea", MatchAPI = API.match_all},

            new OpCodeDetails {And = 0xFF, Xor = 0xC4, OpClass = OpClass.Mem2R, Text = "les", MatchAPI = API.match_all},


            //Single MemOpc Instructions

            new OpCodeDetails {And = 0xFE, Xor = 0xFE, OpClass = OpClass.MemOpc_S1, Text = "!!!", MatchAPI = API.match_all}, // dec/inc/call/jmp etc

            new OpCodeDetails {And = 0xF8, Xor = 0x48, OpClass = OpClass.Reg_16, Text = "dec", MatchAPI = API.match_all},

            new OpCodeDetails {And = 0xFE, Xor = 0xF6, OpClass = OpClass.MemOpc_S2, Text = "!!!", MatchAPI = API.match_all},//DIV/IDIV/IMUL/NEG

            new OpCodeDetails {And = 0xF8, Xor = 0x40, OpClass = OpClass.Reg_16, Text = "inc", MatchAPI = API.match_all},



            //CALL

            // also picked up by FF/FE -> above dec/inc/etc

            new OpCodeDetails {And = 0xFF, Xor = 0xE8, OpClass = OpClass.CallNear, Text = "call", MatchAPI = API.match_all},
            new OpCodeDetails {And = 0xFF, Xor = 0x9A, OpClass = OpClass.CallFar, Text = "call", MatchAPI = API.match_all},

            //Jumps

            new OpCodeDetails {And = 0xF0, Xor = 0x70, OpClass = OpClass.Jcc, Text = "j", MatchAPI = API.match_all},

            new OpCodeDetails {And = 0xFF, Xor = 0xE3, OpClass = OpClass.J_short, Text = "jcxz", MatchAPI = API.match_all},

            new OpCodeDetails {And = 0xFF, Xor = 0xEB, OpClass = OpClass.J_short, Text = "jmp", MatchAPI = API.match_all},

            new OpCodeDetails {And = 0xFF, Xor = 0xE2, OpClass = OpClass.J_short, Text = "loop", MatchAPI = API.match_all},
            new OpCodeDetails {And = 0xFF, Xor = 0xE1, OpClass = OpClass.J_short, Text = "loope", MatchAPI = API.match_all},
            new OpCodeDetails {And = 0xFF, Xor = 0xE0, OpClass = OpClass.J_short, Text = "loopne", MatchAPI = API.match_all},


            new OpCodeDetails {And = 0xFF, Xor = 0xE9, OpClass = OpClass.CallNear, Text = "jmp", MatchAPI = API.match_all},
            new OpCodeDetails {And = 0xFF, Xor = 0xEA, OpClass = OpClass.CallFar, Text = "jmp", MatchAPI = API.match_all},

            //MOVs

            new OpCodeDetails {And = 0xFC, Xor = 0x88, OpClass = OpClass.Mem, Text = "mov", MatchAPI = API.match_all},
            new OpCodeDetails {And = 0xFE, Xor = 0xC6, OpClass = OpClass.MemImm, Text = "mov", MatchAPI = API.match_all},
            new OpCodeDetails {And = 0xF0, Xor = 0xB0, OpClass = OpClass.RegImm, Text = "mov", MatchAPI = API.match_all},
            new OpCodeDetails {And = 0xFC, Xor = 0xA0, OpClass = OpClass.AccDisp, Text = "mov", MatchAPI = API.match_all},
            new OpCodeDetails {And = 0xFD, Xor = 0x8C, OpClass = OpClass.MemSeg, Text = "mov", MatchAPI = API.match_all},

            // strings
            new OpCodeDetails {And = 0xFE, Xor = 0xA6, OpClass = OpClass.String, Text = "cmps", MatchAPI = API.match_all},
            new OpCodeDetails {And = 0xFE, Xor = 0xAC, OpClass = OpClass.String, Text = "lods", MatchAPI = API.match_all},
            new OpCodeDetails {And = 0xFE, Xor = 0xA4, OpClass = OpClass.String, Text = "movs", MatchAPI = API.match_all},
            new OpCodeDetails {And = 0xFE, Xor = 0xAA, OpClass = OpClass.String, Text = "stos", MatchAPI = API.match_all},
            new OpCodeDetails {And = 0xFE, Xor = 0xAE, OpClass = OpClass.String, Text = "scas", MatchAPI = API.match_all},

            new OpCodeDetails {And = 0xFE, Xor = 0x6C, OpClass = OpClass.String, Text = "ins", MatchAPI = API.match_186},
            new OpCodeDetails {And = 0xFE, Xor = 0x6E, OpClass = OpClass.String, Text = "outs", MatchAPI = API.match_186},


            //In/Out
            new OpCodeDetails {And = 0xF4, Xor = 0xE4, OpClass = OpClass.InOut, Text = "???", MatchAPI = API.match_all},

            //Int
            new OpCodeDetails {And = 0xFE, Xor = 0xCC, OpClass = OpClass.Int, Text = "int", MatchAPI = API.match_all},

            //Push/Pop
            new OpCodeDetails {And = 0xF8, Xor = 0x58, OpClass = OpClass.Reg_16, Text = "pop", MatchAPI = API.match_all},
            new OpCodeDetails {And = 0xC7, Xor = 0x07, OpClass = OpClass.Seg_16, Text = "pop", MatchAPI = API.match_all},
            new OpCodeDetails {And = 0xFF, Xor = 0x8F, OpClass = OpClass.MemOpc_S_Pop, Text = "!!!", MatchAPI = API.match_all},

            new OpCodeDetails {And = 0xF8, Xor = 0x50, OpClass = OpClass.Reg_16, Text = "push", MatchAPI = API.match_all},
            new OpCodeDetails {And = 0xC7, Xor = 0x06, OpClass = OpClass.Seg_16, Text = "push", MatchAPI = API.match_all},

            new OpCodeDetails {And = 0xFD, Xor = 0x68, OpClass = OpClass.ImmPush, Text = "push", MatchAPI = API.match_186},

            new OpCodeDetails {And = 0xFF, Xor = 0x60, OpClass = OpClass.Inherent, Text = "pusha", MatchAPI = API.match_186},
            new OpCodeDetails {And = 0xFF, Xor = 0x61, OpClass = OpClass.Inherent, Text = "popa", MatchAPI = API.match_186},

            new OpCodeDetails {And = 0xFF, Xor = 0xC8, OpClass = OpClass.ImmImm, Text = "enter", MatchAPI = API.match_186},
            new OpCodeDetails {And = 0xFF, Xor = 0xC9, OpClass = OpClass.Inherent, Text = "leave", MatchAPI = API.match_186},

            new OpCodeDetails {And = 0xFF, Xor = 0x64, OpClass = OpClass.Mem, Text = "bound", MatchAPI = API.match_186},


            // rotates

            new OpCodeDetails {And = 0xFE, Xor = 0xD0, OpClass = OpClass.MemOpc_S_Rot1, Text = "!!!", MatchAPI = API.match_all},
            new OpCodeDetails {And = 0xFE, Xor = 0xD2, OpClass = OpClass.MemOpc_S_RotCL, Text = "!!!", MatchAPI = API.match_all},
            new OpCodeDetails {And = 0xFE, Xor = 0xC0, OpClass = OpClass.MemImmOpc_Rot, Text = "!!!", MatchAPI = API.match_186},

            // RetImm

            new OpCodeDetails {And = 0xF7, Xor = 0xC2, OpClass = OpClass.RetImm, Text = "ret", MatchAPI = API.match_all}

        };


        public DisRec2<UInt32> Decode(BinaryReader br, DisassAddressBase pc)
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
                opd = OpMap.Where(o => (o.And & opcode) == o.Xor && ((m_API & o.MatchAPI) != 0)).FirstOrDefault();
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
                    case OpClass.MemOpc_S1:
                        ret = DoClassMemOpc_S1(br, pc, l, prefixes, opd, opcode);
                        break;
                    case OpClass.MemOpc_S2:
                        ret = DoClassMemOpc_S2(br, pc, l, prefixes, opd, opcode);
                        break;
                    case OpClass.MemOpc_S_Pop:
                        ret = DoClassMemOpc_S_Pop(br, pc, l, prefixes, opd, opcode);
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
                    case OpClass.ImmImm:
                        ret = DoClassImmImm(br, pc, l, prefixes, opd, opcode);
                        break;
                }
            }



            if (ret == null)
                ret = new DisRec2<uint> { Decoded = false, Length = l };
            else
            {
                //check for b4 prefixes
                var pb4 = prefixes & Prefixes.B4;
                string b4flags = null;
                if (pb4 != 0)
                {
                    StringBuilder sb = new StringBuilder();
                    if ((pb4 & Prefixes.REP) != 0)
                        sb.Append("rep ");
                    if ((pb4 & Prefixes.REPNZ) != 0)
                        sb.Append("repnz ");
                    if ((pb4 & Prefixes.LOCK) != 0)
                        sb.Append("lock ");

                    b4flags = sb.ToString();
                }


                if (!string.IsNullOrEmpty(b4flags))
                {
                    ret = new DisRec2<UInt32>
                    {
                        Decoded = ret.Decoded,
                        Hints = ret.Hints,
                        Length = ret.Length,
                        Mnemonic = $"{b4flags}{ret.Mnemonic}",
                        Operands = ret.Operands
                    };
                }
            }


            return ret;
        }

        private readonly static string[] regs =
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
            "di",
            "eax",
            "ecx",
            "edx",
            "ebx",
            "esp",
            "ebp",
            "esi",
            "edi"
        };

        (String, Prefixes) GetReg(int rrr, bool width, bool wide_oper)
        {
            int ix = (rrr & 0x7) + (width ? (wide_oper ? 16 : 8) : 0);

            var defseg = (ix == 12 || ix == 13 || ix == 32 || ix == 21) ? Prefixes.SS : Prefixes.DS;

            return ( regs[ix], defseg );
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

        IEnumerable<DisRec2OperString_Base> GetData(BinaryReader br, bool w, bool wide_oper, ref ushort l, SymbolType symboltype)
        {
            if (w)
            {
                if (wide_oper)
                {
                    l += 4;
                    return OperNum(br.ReadUInt32(), symboltype);
                }
                else
                {
                    l += 2;
                    return OperNum(br.ReadUInt16(), symboltype);
                }
            }
            else
            {
                l++;
                return OperNum(br.ReadByte(), symboltype);
            }
        }

        IEnumerable<DisRec2OperString_Base> GetData32(BinaryReader br, ref ushort l, SymbolType symboltype)
        {
            if (symboltype == SymbolType.Pointer)
            {
                l += 4;
                var offs = br.ReadUInt16();
                var seg =  br.ReadUInt16();
                return OperAddr(new AddressX86(seg, offs), symboltype);
            }
            else
            {
                l += 4;
                return OperNum(br.ReadUInt32(), symboltype);
            }
        }

        IEnumerable<DisRec2OperString_Base> GetRelDisp(BinaryReader br, bool w, ref ushort l, DisassAddressBase pc)
        {
            int offs;
            if (w)
            {
                offs = br.ReadInt16();
                l += 2;
            }
            else
            {
                offs = br.ReadSByte();
                l += 1;
            }
            return OperAddr(pc + l + offs, SymbolType.Pointer);
        }

        private IEnumerable<DisRec2OperString_Base> GetAcc(bool wide, Prefixes prefixes)
        {
            return wide ? (
                        Wide32Reg(prefixes) ? OperStr("eax") : OperStr("ax")
                        ) : OperStr("al");
        }


        IEnumerable<DisRec2OperString_Base> GetDisp(BinaryReader br, bool w, ref ushort l, Prefixes prefixes)
        {
            var ps = PointerPrefixStr(prefixes, Prefixes.DS);

            return OperStr("[").Concat(ps).Concat(GetData(br, w, Wide32Reg(prefixes), ref l, SymbolType.Pointer)).Concat(OperStr("]"));
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
                if (Wide32Reg(prefixes))
                    ptr_sz_str = OperStr("dword ");
                else
                    ptr_sz_str = OperStr("word ");
            else
                ptr_sz_str = OperStr("byte ");


            if (mod == 3)
            {
                return OperStr(GetReg(r_m, w, Wide32Reg(prefixes)).Item1);
            }
            else if (mod == 00 && r_m == 6)
            {
                offs = br.ReadUInt16();
                l += 2;
                //special addressing mode
                return ptr_sz_str.Concat(OperStr("[")).Concat(PointerPrefixStr(prefixes, Prefixes.DS)).Concat(OperAddr(new AddressX86((UInt16)offs), SymbolType.Pointer)).Concat(OperStr("]"));
            }
            else
            {

                byte sib = 0;

                if (Wide32Oper(prefixes) && r_m == 4)
                {
                    sib = br.ReadByte();
                    l++;
                }


                switch (mod)
                {
                    case 1:
                        offs = br.ReadSByte();
                        l++;
                        break;
                    case 2:
                        if (Wide32Oper(prefixes))
                        {
                            offs = br.ReadInt32();
                            l += 4;
                        }
                        else {
                            offs = br.ReadInt16();
                            l += 2;

                        }
                        break;

                }


                IEnumerable<DisRec2OperString_Base> offs_str;

                if (offs == 0)
                    offs_str = Enumerable.Empty<DisRec2OperString_Base>();
                else if (offs > 0)
                    offs_str = OperStr("+").Concat(OperNum((uint)offs, SymbolType.Pointer));
                else
                    offs_str = OperStr("-").Concat(OperNum((uint)-offs, SymbolType.Pointer));


                Prefixes defpre = Prefixes.DS;

                IEnumerable<DisRec2OperString_Base> mmm;

                if (Wide32Oper(prefixes)) {

                    if (r_m == 4)
                    {
                        /* special index byte */

                        string sibmul;

                        switch ((sib & 0xC0) >> 6)
                        {
                            case 0:
                                sibmul = "1";
                                break;
                            case 1:
                                sibmul = "2";
                                break;
                            case 2:
                                sibmul = "4";
                                break;
                            default:
                                sibmul = "8";
                                break;
                        }

                        var base_reg_ix = sib & 7;
                        var base_ix_ix = (sib & 0x38) >> 3;
                        IEnumerable<DisRec2OperString_Base> base_reg;
                        if (base_reg_ix == 5 && mod == 0)
                        {
                            base_reg = OperNum(br.ReadUInt32(), SymbolType.Pointer);
                            l += 4;
                        }
                        else
                        {
                            string x;
                            (x, defpre) = GetReg(base_reg_ix, true, true);
                            base_reg = OperStr(x);
                        }


                        //m = $"SIBSIBSIB={sib:X2}{string.Join("", base_reg.Select(o => o.ToString()))}+{sibmul}*{GetReg(base_ix_ix, true, true).Item1},ix={base_ix_ix},ba={sib & 7},mod={mod:X},r_m={r_m:X}";
                        mmm = base_reg
                            .Concat(OperStr("+"))
                            .Concat(OperStr(sibmul))
                            .Concat(OperStr("*"))
                            .Concat(OperStr(GetReg(base_ix_ix, true, true).Item1))
                            ;

                    }
                    else
                    {

                        string m;
                        switch (r_m)
                        {
                            case 0:
                                m = "eax";
                                break;
                            case 1:
                                m = "ecx";
                                break;
                            case 2:
                                m = "edx";
                                break;
                            case 3:
                                m = "ebx";
                                break;
                            case 5:
                                m = "ebp";
                                defpre = Prefixes.SS;
                                break;
                            case 6:
                                m = "esi";
                                break;
                            case 7:
                                m = "edi";
                                break;
                            default:
                                return null;
                        }

                        mmm = OperStr(m);
                    }
                }
                else {
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
                            defpre = Prefixes.SS;
                            break;
                        case 3:
                            m = "bp+di";
                            defpre = Prefixes.SS;
                            break;
                        case 4:
                            m = "si";
                            break;
                        case 5:
                            m = "di";
                            break;
                        case 6:
                            m = "bp";
                            defpre = Prefixes.SS;
                            break;
                        case 7:
                            m = "bx";
                            break;
                        default:
                            return null;
                    }
                    mmm = OperStr(m);
                }

                var pre = PointerPrefixStr(prefixes, defpre);

                return ptr_sz_str.Concat(pre.Concat(OperStr("[").Concat(mmm).Concat(offs_str).Concat(OperStr("]"))));
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

        private DisRec2<UInt32> DoClassRegImm(BinaryReader br, DisassAddressBase pc, ushort l, Prefixes prefixes, OpCodeDetails opd, byte opcode)
        {
            bool w = (opcode & 0x08) != 0;
            int rix = opcode & 0x7;
            bool wide32reg = Wide32Reg(prefixes);

            var Ops = OperStr(GetReg(rix, w, wide32reg).Item1)
                .Concat(OperStr(","))
                .Concat(GetData(br, w, wide32reg, ref l, SymbolType.Immediate));

            return new DisRec2<UInt32>
            {
                Decoded = true,
                Length = (ushort)(l),
                Mnemonic = opd.Text,
                Operands = Ops
            };

        }
        private DisRec2<UInt32> DoClassReg_16(BinaryReader br, DisassAddressBase pc, ushort l, Prefixes prefixes, OpCodeDetails opd, byte opcode)
        {
            int rix = opcode & 0x7;

            var Reg = GetReg(rix, true, Wide32Reg(prefixes));
            var Ops = OperStr(Reg.Item1);
            if ((opcode & 0xF8) == 0x90)
            {
                //xchg acc,reg
                Ops = OperStr("ax,").Concat(Ops);
            }

            return new DisRec2<UInt32>
            {
                Decoded = true,
                Length = (ushort)(l),
                Mnemonic = opd.Text,
                Operands = Ops
            };

        }

        private DisRec2<UInt32> DoClassSeg_16(BinaryReader br, DisassAddressBase pc, ushort l, Prefixes prefixes, OpCodeDetails opd, byte opcode)
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


        private DisRec2<UInt32> DoClassAccImm(BinaryReader br, DisassAddressBase pc, ushort l, Prefixes prefixes, OpCodeDetails opd, byte opcode)
        {
            bool w = (opcode & 0x01) != 0;

            var acc = GetAcc(w, prefixes);
            var imm = GetData(br, w, Wide32Reg(prefixes), ref l, SymbolType.Immediate);

            return new DisRec2<UInt32>
            {
                Decoded = true,
                Length = (ushort)(l),
                Mnemonic = opd.Text,
                Operands = acc.Concat(OperStr(",")).Concat(imm)
            };

        }

        private DisRec2<UInt32> DoClassAccDisp(BinaryReader br, DisassAddressBase pc, ushort l, Prefixes prefixes, OpCodeDetails opd, byte opcode)
        {
            bool w = (opcode & 0x01) != 0;
            bool d = (opcode & 0x02) != 0;

            var Mem = GetDisp(br, true, ref l, prefixes & ~Prefixes.WIDE_REG);
            var Acc = GetAcc(w, prefixes);

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

        private DisRec2<UInt32> DoClassMem(BinaryReader br, DisassAddressBase pc, ushort l, Prefixes prefixes, OpCodeDetails opd, byte opcode)
        {
            bool dflag = (opcode & 0x02) != 0;
            bool w = (opcode & 0x01) != 0;

            return DoClassMem_int(br, pc, l, prefixes, opd, opcode, dflag, w);
        }

        private DisRec2<UInt32> DoClassMemW(BinaryReader br, DisassAddressBase pc, ushort l, Prefixes prefixes, OpCodeDetails opd, byte opcode)
        {
            return DoClassMem_int(br, pc, l, prefixes, opd, opcode, true, true);
        }

        private DisRec2<UInt32> DoClassMem2R(BinaryReader br, DisassAddressBase pc, ushort l, Prefixes prefixes, OpCodeDetails opd, byte opcode)
        {
            return DoClassMem_int(br, pc, l, prefixes, opd, opcode, true, true);
        }


        private DisRec2<UInt32> DoClassMem_int(BinaryReader br, DisassAddressBase pc, ushort l, Prefixes prefixes, OpCodeDetails opd, byte opcode, bool dflag, bool w)
        {
            byte modrm = br.ReadByte();

            int mod = (modrm & 0xC0) >> 6;
            int rrr = (modrm & 0x38) >> 3;
            int r_m = modrm & 0x7;

            var Op1 = OperStr(GetReg(rrr, w, Wide32Reg(prefixes)).Item1);
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

        private DisRec2<UInt32> DoClassMemSeg(BinaryReader br, DisassAddressBase pc, ushort l, Prefixes prefixes, OpCodeDetails opd, byte opcode)
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


        public DisRec2<UInt32> DoClassMemImm(BinaryReader br, DisassAddressBase pc, ushort l, Prefixes prefixes, OpCodeDetails opd, byte opcode)
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

            var Op1 = GetData(br, w, Wide32Reg(prefixes), ref l, SymbolType.Immediate);
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

        public DisRec2<UInt32> DoClassMemImmOpc_DP(BinaryReader br, DisassAddressBase pc, ushort l, Prefixes prefixes, OpCodeDetails opd, byte opcode)
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

            var Op1 = GetData(br, w ^ s, Wide32Reg(prefixes), ref l, SymbolType.Immediate);
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


        public DisRec2<UInt32> DoClassMemImmOpc_Rot(BinaryReader br, DisassAddressBase pc, ushort l, Prefixes prefixes, OpCodeDetails opd, byte opcode)
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

            var Op1 = GetData(br, false, Wide32Reg(prefixes), ref l, SymbolType.Immediate);
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


        public DisRec2<UInt32> DoClassMemOpc_S1(BinaryReader br, DisassAddressBase pc, ushort l, Prefixes prefixes, OpCodeDetails opd, byte opcode)
        {
            bool w = (opcode & 0x01) != 0;

            byte modrm = br.ReadByte();

            int mod = (modrm & 0xC0) >> 6;
            int rrr = (modrm & 0x38) >> 3;
            bool call = rrr >= 2 && rrr <= 4;
            bool far = (rrr & 0x01) != 0;

            string opcode_over = opcode_extensions_s1[rrr];

            int r_m = modrm & 0x7;


            var Op2 = GetModRm(br, mod, r_m, w, call ? far : true, prefixes, ref l, call: call);
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

        public DisRec2<UInt32> DoClassMemOpc_S2(BinaryReader br, DisassAddressBase pc, ushort l, Prefixes prefixes, OpCodeDetails opd, byte opcode)
        {
            bool w = (opcode & 0x01) != 0;

            byte modrm = br.ReadByte();

            int mod = (modrm & 0xC0) >> 6;
            int rrr = (modrm & 0x38) >> 3;
            bool far = (rrr & 0x01) != 0;

            string opcode_over = opcode_extensions_s2[rrr];

            int r_m = modrm & 0x7;


            var Op2 = GetModRm(br, mod, r_m, w, true, prefixes, ref l);
            if (Op2 == null)
                return null;

            IEnumerable<DisRec2OperString_Base> Ops;
            if (rrr == 0)
            {
                UInt32 imm;
                if (w)
                {
                    imm = br.ReadUInt16();
                    l += 2;
                }
                else
                {
                    imm = br.ReadByte();
                    l++;
                }

                // horendous bodge for test!
                Ops = Op2.Concat(OperStr(",")).Concat(OperNum(imm, SymbolType.Immediate));
            }
            else
            {
                Ops = Op2;
            }


            return new DisRec2<UInt32>
            {
                Decoded = true,
                Length = (ushort)(l + 1),
                Mnemonic = opcode_over,
                Operands = Ops
            };
        }

        public DisRec2<UInt32> DoClassMemOpc_S_Pop(BinaryReader br, DisassAddressBase pc, ushort l, Prefixes prefixes, OpCodeDetails opd, byte opcode)
        {
            bool w = (opcode & 0x01) != 0;

            byte modrm = br.ReadByte();

            int mod = (modrm & 0xC0) >> 6;
            int rrr = (modrm & 0x38) >> 3;

            if (rrr != 0)
                return null; //not a pop


            int r_m = modrm & 0x7;


            var Op2 = GetModRm(br, mod, r_m, w, false, prefixes, ref l, false);
            if (Op2 == null)
                return null;


            return new DisRec2<UInt32>
            {
                Decoded = true,
                Length = (ushort)(l + 1),
                Mnemonic = "pop",
                Operands = Op2
            };
        }


        public DisRec2<UInt32> DoClassMemOpc_S_Rot1(BinaryReader br, DisassAddressBase pc, ushort l, Prefixes prefixes, OpCodeDetails opd, byte opcode)
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

        public DisRec2<UInt32> DoClassMemOpc_S_RotCL(BinaryReader br, DisassAddressBase pc, ushort l, Prefixes prefixes, OpCodeDetails opd, byte opcode)
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

        public DisRec2<UInt32> DoClassInherent(BinaryReader br, DisassAddressBase pc, ushort l, Prefixes prefixes, OpCodeDetails opd, byte opcode)
        {
            return new DisRec2<uint>
            {
                Decoded = true,
                Length = l,
                Mnemonic = opd.Text
            };
        }

        public DisRec2<UInt32> DoClassInherent_AA(BinaryReader br, DisassAddressBase pc, ushort l, Prefixes prefixes, OpCodeDetails opd, byte opcode)
        {
            if (br.ReadByte() != 0x0A)
                return null;
            else

                return new DisRec2<uint>
                {
                    Decoded = true,
                    Length = (ushort)(l + 1),
                    Mnemonic = opd.Text
                };
        }

        public DisRec2<UInt32> DoClassCallNear(BinaryReader br, DisassAddressBase pc, ushort l, Prefixes prefixes, OpCodeDetails opd, byte opcode)
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

        public DisRec2<UInt32> DoClassCallFar(BinaryReader br, DisassAddressBase pc, ushort l, Prefixes prefixes, OpCodeDetails opd, byte opcode)
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


        public DisRec2<UInt32> DoClassString(BinaryReader br, DisassAddressBase pc, ushort l, Prefixes prefixes, OpCodeDetails opd, byte opcode)
        {
            bool w = (opcode & 0x01) != 0;

            var mne = $"{opd.Text}{(w ? "w" : "")}";

            var segs = prefixes & Prefixes.SEGS;
            if (segs != 0)
            {
                StringBuilder sb = new StringBuilder();

                if ((segs & Prefixes.ES) != 0)
                    sb.Append("es ");
                if ((segs & Prefixes.CS) != 0)
                    sb.Append("cs ");
                if ((segs & Prefixes.SS) != 0)
                    sb.Append("ss ");
                if ((segs & Prefixes.DS) != 0)
                    sb.Append("ds ");

                if (sb.Length > 0)
                    mne = sb.ToString() + mne;
            }


            return new DisRec2<UInt32>
            {
                Decoded = true,
                Length = l,
                Mnemonic = mne
            };

        }

        public DisRec2<UInt32> DoClassInOut(BinaryReader br, DisassAddressBase pc, ushort l, Prefixes prefixes, OpCodeDetails opd, byte opcode)
        {
            bool w = (opcode & 0x01) != 0;
            bool d = (opcode & 0x02) != 0;
            bool dx = (opcode & 0x08) != 0;

            var OpP = dx ? OperStr("dx") : GetData(br, false, false, ref l, SymbolType.Port);

            var OpAcc = GetAcc(w, prefixes);

            var Ops = d ? OpP.Concat(OperStr(",")).Concat(OpAcc) : OpAcc.Concat(OperStr(",")).Concat(OpP);

            return new DisRec2<UInt32>
            {
                Decoded = true,
                Length = l,
                Mnemonic = d ? "out" : "in",
                Operands = Ops
            };

        }

        public DisRec2<UInt32> DoClassInt(BinaryReader br, DisassAddressBase pc, ushort l, Prefixes prefixes, OpCodeDetails opd, byte opcode)
        {
            IEnumerable<DisRec2OperString_Base> Num;

            if ((opcode & 0x01) == 0)
                return new DisRec2<UInt32>
                {
                    Decoded = true,
                    Length = l,
                    Mnemonic = "int3",
                    Operands = null
                };
            else
                Num = GetData(br, false, false, ref l, SymbolType.ServiceCall);

            return new DisRec2<UInt32>
            {
                Decoded = true,
                Length = l,
                Mnemonic = opd.Text,
                Operands = Num
            };

        }

        public DisRec2<UInt32> DoClassJcc(BinaryReader br, DisassAddressBase pc, ushort l, Prefixes prefixes, OpCodeDetails opd, byte opcode)
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
        public DisRec2<UInt32> DoClassRetImm(BinaryReader br, DisassAddressBase pc, ushort l, Prefixes prefixes, OpCodeDetails opd, byte opcode)
        {
            bool f = (opcode & 0x08) != 0;

            var Ops = OperNum(br.ReadUInt16(), SymbolType.Offset);
            l += 2;

            return new DisRec2<UInt32>
            {
                Decoded = true,
                Length = l,
                Mnemonic = $"{opd.Text}{(f ? "f" : "")}",
                Operands = Ops
            };

        }

        public DisRec2<UInt32> DoClassImmImm(BinaryReader br, DisassAddressBase pc, ushort l, Prefixes prefixes, OpCodeDetails opd, byte opcode)
        {
            var OpA = OperNum(br.ReadUInt16(), SymbolType.Offset);
            l += 2;
            var OpB = OperNum(br.ReadByte(), SymbolType.Offset);
            l += 1;

            return new DisRec2<UInt32>
            {
                Decoded = true,
                Length = l,
                Mnemonic = $"{opd.Text}",
                Operands = OpA.Concat(OperStr(",")).Concat(OpB)
            };

        }

        public DisRec2<UInt32> DoClassJ_short(BinaryReader br, DisassAddressBase pc, ushort l, Prefixes prefixes, OpCodeDetails opd, byte opcode)
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

        public DisRec2<UInt32> DoClassImmPush(BinaryReader br, DisassAddressBase pc, ushort l, Prefixes prefixes, OpCodeDetails opd, byte opcode)
        {
            bool w = (opcode & 0x02) == 0;

            IEnumerable<DisRec2OperString_Base> Ops;
            if (w)
            {
                Ops = OperStr("word ").Concat(OperNum(br.ReadUInt16(), SymbolType.Immediate));
                l += 2;
            }
            else
            {
                Ops = OperStr("byte ").Concat(OperNum(br.ReadByte(), SymbolType.Immediate));
                l++;
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

        private static IEnumerable<DisRec2OperString_Base> OperAddr(DisassAddressBase addr, SymbolType type)
        {
            return new[] { new DisRec2OperString_Address { Address = addr, SymbolType = type } };
        }
    }
}
