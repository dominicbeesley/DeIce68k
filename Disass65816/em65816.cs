
//Adpated from https://github.com/hoglet67/6502Decoder/blob/master/src/em_65816.c

// This version of the emulator is to take a cycle-by-cycle trace capture and maintain / check CPU state against the trace


using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection.Emit;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using System.Net.Http.Headers;
using System.Diagnostics;


namespace Disass65816
{
    public class em65816
    {

        public class Registers
        {
            public em65816 Emulator { get; init; }

            // 6502 registers: -1 means unknown
            public int A { get; set; }
            public int X { get; set; }
            public int Y { get; set; }

            public int SH { get; set; }
            public int SL { get; set; }

            public int PC { get; set; }

            // 65C816 additional registers: -1 means unknown

            /// <summary>
            /// Accumulator bits 15..8
            /// </summary>
            public int B { get; set; }
            /// <summary>
            /// 16-bit Direct Page Register (default to zero, otherwise AddrMode.ZP addressing is broken)
            /// </summary>
            public int DP { get; set; }
            /// <summary>
            /// 8-bit Data Bank Register
            /// </summary>
            public int DB { get; set; }
            /// <summary>
            /// 8-bit Program Bank Register
            /// </summary>
            public int PB { get; set; }

            // 6502 flags: -1 means unknown
            public Tristate N { get; set; }
            public Tristate V { get; set; }
            public Tristate D { get; set; }
            public Tristate I { get; set; }
            public Tristate Z { get; set; }
            public Tristate C { get; set; }

            // 65C816 additional flags: -1 means unknown
            public Tristate MS { get; set; }  // Accumulator and Memeory Size Flag
            public Tristate XS { get; set; } // Index Register Size Flag
            public Tristate E { get; set; }  // Emulation Mode Flag, updated by XCE

            protected string RF(int x)
            {
                if (x < 0)
                    return "??";
                else
                    return x.ToString("X02");
            }

            protected string RF16(int x)
            {
                if (x < 0)
                    return "????";
                else
                    return x.ToString("X04");
            }

            protected char F(char l, Tristate v)
            {
                return v == Tristate.Unknown ? '?' : v == Tristate.True ? char.ToUpper(l) : char.ToLower(l);

            }


            public override string ToString()
            {
                return $"A={RF(B)}{RF(A)}, X={RF16(X)}, Y={RF16(Y)}, {F('E', E)} {F('N', N)}{F('V', V)}{F('M', MS)}{F('X', XS)}{F('D', D)}{F('I', I)}{F('Z', Z)}{F('C', C)} S={RF(SH)}{RF(SL)}, PC={RF(PB)}{RF16(PC)}, DB={RF(DB)}, DP={RF16(DP)}";
            }

            public Registers(em65816 emulator)
            {
                this.Emulator = emulator;

                A = X = Y = SH = SL = PC = -1;
                B = DP = PB = DB = -1;

                N = V = D = I = Z = C = Tristate.Unknown;

                MS = XS = E = Tristate.Unknown;
            }

            private Registers(Registers other)
            {
                Emulator = other.Emulator;
                A = other.A;
                X = other.X;
                Y = other.Y;
                SH = other.SH;
                SL = other.SL;
                PC = other.PC;
                B = other.B;
                DP = other.DP;
                PB = other.PB;
                DB = other.DB;
                N = other.N;
                V = other.V;
                D = other.D;
                I = other.I;
                Z = other.Z;
                C = other.C;
                MS = other.MS;
                XS = other.XS;
                E = other.E;
            }

            public int memory_read(int ea, mem_access_t acctype) => Emulator.memory_read?.Invoke(ea) ?? -1;
            public void memory_write(int value, int ea, mem_access_t acctype) => Emulator.memory_write?.Invoke(ea, value);

            public Registers Clone()
            {
                return new Registers(this);
            }

            public bool compare_FLAGS(int operand)
            {
                if (N != Tristate.Unknown && (int)N != ((operand >> 7) & 1)) return true;
                if (V != Tristate.Unknown && (int)V != ((operand >> 6) & 1)) return true;
                if (E == Tristate.False && MS >= 0 && (int)MS != ((operand >> 5) & 1)) return true;
                if (E == Tristate.False && XS >= 0 && (int)XS != ((operand >> 4) & 1)) return true;
                if (D != Tristate.Unknown && (int)D != ((operand >> 3) & 1)) return true;
                if (I != Tristate.Unknown && (int)I != ((operand >> 2) & 1)) return true;
                if (Z != Tristate.Unknown && (int)Z != ((operand >> 1) & 1)) return true;
                if (C != Tristate.Unknown && (int)C != ((operand >> 0) & 1)) return true;
                return false;
            }

            public void set_FLAGS(int operand)
            {
                N = (Tristate)((operand >> 7) & 1);
                V = (Tristate)((operand >> 6) & 1);
                if (E == Tristate.False)
                {
                    MS = (Tristate)((operand >> 5) & 1);
                    XS = (Tristate)((operand >> 4) & 1);
                }
                else
                {
                    MS = Tristate.True;
                    XS = Tristate.True;
                }
                D = (Tristate)((operand >> 3) & 1);
                I = (Tristate)((operand >> 2) & 1);
                Z = (Tristate)((operand >> 1) & 1);
                C = (Tristate)((operand >> 0) & 1);
            }

            public int get_FLAGS()
            {
                var ret = 0;
                if (N == Tristate.Unknown) return -1; else ret |= N == Tristate.True ? 0x80 : 0;
                if (V == Tristate.Unknown) return -1; else ret |= V == Tristate.True ? 0x40 : 0;

                if (D == Tristate.Unknown) return -1; else ret |= D == Tristate.True ? 0x8 : 0;
                if (I == Tristate.Unknown) return -1; else ret |= I == Tristate.True ? 0x4 : 0;
                if (Z == Tristate.Unknown) return -1; else ret |= Z == Tristate.True ? 0x2 : 0;
                if (C == Tristate.Unknown) return -1; else ret |= C == Tristate.True ? 0x1 : 0;

                if (E == Tristate.False)
                {
                    if (MS == Tristate.Unknown) return -1; else ret |= MS == Tristate.True ? 0x20 : 0;
                    if (XS == Tristate.Unknown) return -1; else ret |= XS == Tristate.True ? 0x10 : 0;

                }
                else if (E == Tristate.True)
                    ret |= 0x30;
                else
                    ret = -1;

                return ret;
            }

            public void set_NZ_unknown()
            {
                N = Tristate.Unknown;
                Z = Tristate.Unknown;
            }

            public void set_NZC_unknown()
            {
                N = Tristate.Unknown;
                Z = Tristate.Unknown;
                C = Tristate.Unknown;
            }

            public void set_NVZC_unknown()
            {
                N = Tristate.Unknown;
                V = Tristate.Unknown;
                Z = Tristate.Unknown;
                C = Tristate.Unknown;
            }

            public void set_NZ8(int value)
            {
                N = (Tristate)((value >> 7) & 1);
                Z = (Tristate)((value & 0xff) == 0 ? 1 : 0);
            }

            public void set_NZ16(int value)
            {
                N = (Tristate)((value >> 15) & 1);
                Z = (Tristate)((value & 0xffff) == 0 ? 1 : 0);
            }

            public void set_NZ_unknown_width(int value)
            {
                // Don't know which bit is the sign bit
                Tristate s15 = (Tristate)((value >> 15) & 1);
                Tristate s7 = (Tristate)((value >> 7) & 1);
                if (s7 == s15)
                {
                    // both choices of sign bit are the same
                    N = s7;
                }
                else
                {
                    // possible sign bits differ, so N must become undefined
                    N = Tristate.Unknown;
                }
                // Don't know how many bits to check for any ones
                if ((value & 0xff00) == 0)
                {
                    // no high bits set, so base Z on the low bits
                    Z = FromBool((value & 0xff) == 0);
                }
                else
                {
                    // some high bits set, so Z must become undefined
                    Z = Tristate.Unknown;
                }
            }

            public void set_NZ_XS(int value)
            {
                if (XS < 0)
                {
                    set_NZ_unknown_width(value);
                }
                else if (XS == 0)
                {
                    set_NZ16(value);
                }
                else
                {
                    set_NZ8(value);
                }
            }

            public void set_NZ_MS(int value)
            {
                if (MS == Tristate.Unknown)
                {
                    set_NZ_unknown_width(value);
                }
                else if (MS == Tristate.False)
                {
                    set_NZ16(value);
                }
                else
                {
                    set_NZ8(value);
                }
            }

            public void set_NZ_AB(int A, int B)
            {
                if (MS == Tristate.True)
                {
                    // 8-bit
                    if (A >= 0)
                    {
                        set_NZ8(A);
                    }
                    else
                    {
                        set_NZ_unknown();
                    }
                }
                else if (MS == Tristate.False)
                {
                    // 16-bit
                    if (A >= 0 && B >= 0)
                    {
                        set_NZ16((B << 8) + A);
                    }
                    else
                    {
                        // TODO: the behaviour when A is known and B is unknown could be improved
                        set_NZ_unknown();
                    }
                }
                else
                {
                    // width unknown
                    if (A >= 0 && B >= 0)
                    {
                        set_NZ_unknown_width((B << 8) + A);
                    }
                    else
                    {
                        set_NZ_unknown();
                    }
                }
            }

            // ====================================================================
            // Helper Methods
            // ====================================================================

            public int memory_read16(int ea, mem_access_t acctype)
            {
                if (ea < 0) return -1;

                var l = memory_read(ea, acctype);
                if (l < 0) return -1;

                var h = memory_read(ea + 1, acctype);
                if (h < 0) return -1;

                return (l & 0xFF) | ((h & 0xFF) << 8);

            }
            public void memory_write16(int value, int ea, mem_access_t acctype)
            {
                if (ea < 0 || value < 0) return;

                memory_write(value & 0xFF, ea, acctype);
                memory_write((value & 0xFF00) >> 8, ea, acctype);
            }

            public int memory_read24(int ea, mem_access_t acctype)
            {
                if (ea < 0) return -1;

                var l = memory_read(ea, acctype);
                if (l < 0) return -1;

                var h = memory_read(ea, acctype);
                if (h < 0) return -1;

                var b = memory_read(ea, acctype);
                if (b < 0) return -1;

                return (l & 0xFF) | ((h & 0xFF) << 8) | ((b & 0xFF) << 16);

            }

            public int memory_read_MS(int ea, Tristate size, mem_access_t acctype)
            {
                return (size == Tristate.Unknown)?-1:(size == Tristate.True)?memory_read(ea, acctype):memory_read16(ea, acctype);   
            }

            public void memory_write_MS(int value, int ea, Tristate size, mem_access_t acctype)
            {
                if (size == Tristate.False)
                    //16 bit
                    memory_write16(value, ea, acctype);
                else
                    memory_write(value, ea, acctype);
            }


            // TODO: Stack wrapping im emulation mode should only happen with "old" instructions
            // e.g. PLB should not wrap
            // See appendix of 65C816 Opcodes by Bruce Clark

            public int pop8()
            {
                // Increment the low byte of SP
                if (SL >= 0)
                {
                    SL = (SL + 1) & 0xff;
                }
                // Increment the high byte of SP, in certain cases
                if (E == Tristate.True)
                {
                    // In emulation mode, force SH to 1
                    SH = 1;
                }
                else if (E == 0)
                {
                    // In native mode, increment SH if SL has wrapped to 0
                    if (SH >= 0)
                    {
                        if (SL < 0)
                        {
                            SH = -1;
                        }
                        else if (SL == 0)
                        {
                            SH = (SH + 1) & 0xff;
                        }
                    }
                }
                else
                {
                    SH = -1;
                }
                // Handle the memory access
                if (SL >= 0 && SH >= 0)
                {
                    var r = memory_read((SH << 8) + SL, mem_access_t.MEM_STACK);
                    if (r < 0)
                        return -1;
                    else
                        return r & 0xFF;
                }
                else
                    return -1;
            }

            // TODO: Stack wrapping im emulation mode should only happen with "old" instructions
            // e.g. PLB should not wrap
            // See appendix of 65C816 Opcodes by Bruce Clark

            public void push8(int value)
            {
                // Handle the memory access
                if (SL >= 0 && SH >= 0)
                {
                    memory_write(value & 0xFF, (SH << 8) + SL, mem_access_t.MEM_STACK);
                }
                // Decrement the low byte of SP
                if (SL >= 0)
                {
                    SL = (SL - 1) & 0xff;
                }
                // Decrement the high byte of SP, in certain cases
                if (E == Tristate.True)
                {
                    // In emulation mode, force SH to 1
                    SH = 1;
                }
                else if (E == Tristate.False)
                {
                    // In native mode, increment SH if SL has wrapped to 0
                    if (SH >= 0)
                    {
                        if (SL < 0)
                        {
                            SH = -1;
                        }
                        else if (SL == 0xff)
                        {
                            SH = (SH - 1) & 0xff;
                        }
                    }
                }
                else
                {
                    SH = -1;
                }
            }

            public int pop16()
            {
                int l = pop8();
                int h = pop8();

                if (l >= 0 && h >= 0)
                    return l + (h  << 8);
                else
                    return -1;
            }

            public void push16(int value)
            {
                if (value < 0)
                {
                    push8(-1);
                    push8(-1);
                }
                else
                {
                    push8(value >> 8);
                    push8(value);
                }
            }

            public int popXS()
            {
                if (XS < 0)
                {
                    SL = -1;
                    SH = -1;
                    return -1;
                }
                else if (XS == 0)
                {
                    return pop16();
                }
                else
                {
                    return pop8();
                }
            }

            public int popMS()
            {
                if (MS == Tristate.Unknown)
                {
                    SL = -1;
                    SH = -1;
                    return -1;
                }
                else if (MS == Tristate.False)
                {
                    return pop16();
                }
                else
                {
                    return pop8();
                }
            }

            public void pushXS(int value)
            {
                if (XS < 0)
                {
                    SL = -1;
                    SH = -1;
                }
                else if (XS == 0)
                {
                    push16(value);
                }
                else
                {
                    push8(value);
                }
            }

            public void pushMS(int value)
            {
                if (MS == Tristate.Unknown)
                {
                    SL = -1;
                    SH = -1;
                }
                else if (MS == Tristate.False)
                {
                    push16(value);
                }
                else
                {
                    push8(value);
                }
            }



            // A set of actions to take if emulation mode enabled
            public void emulation_mode_on()
            {
                MS = Tristate.True;
                XS = Tristate.True;
                if (X >= 0)
                {
                    X &= 0x00ff;
                }
                if (Y >= 0)
                {
                    Y &= 0x00ff;
                }
                SH = 0x01;
                E = Tristate.True;
            }

            // A set of actions to take if emulation mode enabled
            public void emulation_mode_off()
            {
                E = Tristate.False;
            }

            // Helper to return the variable size accumulator
            public int get_accumulator()
            {
                if (MS == Tristate.True)
                {
                    // 8-bit mode
                    return A;
                }
                else if (MS == Tristate.False && A >= 0 && B >= 0)
                {
                    // 16-bit mode
                    return (B << 8) + A;
                }
                else
                {
                    // unknown width
                    return -1;
                }
            }


        }


        public enum Tristate
        {
            Unknown = -1,
            False = 0,
            True = 1
        }

        static Tristate TriNot(Tristate s) { return s == Tristate.Unknown ? Tristate.Unknown : s == Tristate.True ? Tristate.False : Tristate.True; }
        static Tristate FromBool(bool b) { return b ? Tristate.True : Tristate.False; }
        static bool ToBool(Tristate t, bool def) => t == Tristate.Unknown ? def : t == Tristate.True ? true : false;


        public struct instruction_t
        {
            public bool interrupt { get; set; }
            public int pc { get; set; }
            public int pb { get; set; }
            public byte opcode { get; set; }
            public byte op1 { get; set; }
            public byte op2 { get; set; }
            public byte op3 { get; set; }
            public byte opcount { get; set; }
        }



        public enum mem_access_t
        {
            MEM_INSTR = 0,
            MEM_POINTER = 1,
            MEM_DATA = 2,
            MEM_STACK = 3,
            MEM_FETCH = 4,
        }



        public delegate int memory_reader_fn(int address);
        public delegate void memory_writer_fn(int address, int value);

        // ====================================================================
        // Type Defs
        // ====================================================================

        public enum AddrMode
        {
            INDX,
            INDY,
            IND,
            IDL,
            IDLY,
            ZPX,
            ZPY,
            ZP,
            // All direct page modes are <= AddrMode.ZP
            ABS,
            ABSX,
            ABSY,
            IND16,
            IND1X,
            SR,
            ISY,
            ABL,
            ALX,
            IAL,
            BRL,
            BM,
            IMP,
            IMPA,
            BRA,
            /// <summary>
            /// immediate - always 8 bits
            /// </summary>
            IMM8,
            /// <summary>
            /// immediate - "memory" - size depends on MS
            /// </summary>
            IMMM,
            /// <summary>
            /// immediate - "index" - size depends on MX
            /// </summary>
            IMMX
        }

        public enum OpType
        {
            READOP,
            WRITEOP,
            RMWOP,
            BRANCHOP,
            OTHER
        }

        public struct AddrModeType
        {
            public int len { get; init; }
            public string fmt { get; init; }
        }

        public struct operand_t
        {
            public int Ea { get; init; }
            public bool Immediate { get; init; }

            public int GetValue(Registers r, Tristate size, mem_access_t acctype)
            {
                if (Immediate)
                    return Ea;
                else
                    return r.memory_read_MS(Ea, size, acctype);
            }

            public void SetValue(Registers r, Tristate size, mem_access_t acctype, int value)
            {
                if (Immediate)
                    Debug.Assert(false, "Tried to set an immediate");
                else
                    r.memory_write_MS(value, Ea, size, acctype);
            }
        }

        private delegate IEnumerable<Registers> emulate_method(Registers em, operand_t operVal, instruction_t instruction);


        private struct InstrType
        {
            public string mnemonic { get; private init; }
            public AddrMode mode { get; private init; }
            public int cycles { get; private init; }
            public int newop { get; private init; }
            public OpType optype { get; private init; }
            public emulate_method emulate { get; private init; }
            public int len { get; private init; }
            public int m_extra { get; private init; }
            public int x_extra { get; private init; }
            public string fmt { get; private init; }

            public InstrType(string mnemonic, AddrMode mode, int cycles, int newop, OpType optype, emulate_method emulate)
            {
                this.mnemonic = mnemonic;
                this.mode = mode;
                this.cycles = cycles;
                this.newop = newop;
                this.optype = optype;
                this.emulate = emulate;


                // Compute the extra cycles for the 816 when M=0 and/or X=0
                this.m_extra = 0;
                this.x_extra = 0;
                if (this.mode != AddrMode.IMPA)
                {
                    // add 1 cycle if m=0: ADC, AND, BIT, CMP, EOR, LDA, ORA, PHA, PLA, SBC, STA, STZ
                    if (m1_ops.Contains(this.mnemonic))
                    {
                        this.m_extra++;
                        if (this.optype == OpType.READOP && (this.mode == AddrMode.ABSX || this.mode == AddrMode.ABSY))
                        {
                            // add 1 further cycle if x=0: AddrMode.ABS,X or AddrMode.ABS,Y
                            this.x_extra++;
                        }
                    }
                    // add 2 cycles if m=0 (NOT the implied ones): ASL, DEC, INC, LAddrMode.SR, ROL, ROR, TRB, TSB
                    if (m2_ops.Contains(this.mnemonic))
                    {
                        this.m_extra += 2;
                    }
                    // add 1 cycle if x=0: CPX, CPY, LDX, LDY, STX, STY, PLX, PLY, PHX, PHY
                    if (x1_ops.Contains(this.mnemonic))
                    {
                        this.x_extra++;
                        if (this.mode == AddrMode.ABSX || this.mode == AddrMode.ABSY)
                        {
                            // add 1 further cycle if x=0: LDX AddrMode.ABS,Y or LDY AddrMode.ABS,X
                            this.x_extra++;
                        }

                    }
                }
                // Copy the length and format from the address mode, for efficiency
                this.len = addr_mode_table[(int)this.mode].len;
                this.fmt = addr_mode_table[(int)this.mode].fmt;
            }
        }



        // ====================================================================
        // Static variables
        // ====================================================================
        /*
#define OFFSET_B    2
#define OFFSET_A    4
#define OFFSET_X    9
#define OFFSET_Y   16
#define OFFSET_SH  24
#define OFFSET_SL  26
#define OFFSET_N   31
#define OFFSET_V   35
#define OFFSET_MS  39
#define OFFSET_XS  43
#define OFFSET_D   47
#define OFFSET_I   51
#define OFFSET_Z   55
#define OFFSET_C   59
#define OFFSET_E   63
#define OFFSET_PB  68
#define OFFSET_DB  74
#define OFFSET_DP  80
#define OFFSET_END 84

        static const char default_state[] = "A=???? X=???? Y=???? SP=???? N=? V=? M=? X=? D=? I=? Z=? C=? E=? PB=?? DB=?? DP=????";
        */


        //NOTE: this table needs to be in the same order as the AddrMode enum definition
        private static AddrModeType[] addr_mode_table = {
            new AddrModeType {len = 2,    fmt = "%1$s (%2$02X,X)"},          // AddrMode.INDX
            new AddrModeType {len = 2,    fmt = "%1$s (%2$02X),Y"},          // AddrMode.INDY
            new AddrModeType {len = 2,    fmt = "%1$s (%2$02X)"},            // AddrMode.IND
            new AddrModeType {len = 2,    fmt = "%1$s [%2$02X]"},            // AddrMode.IDL
            new AddrModeType {len = 2,    fmt = "%1$s [%2$02X],Y"},          // AddrMode.IDLY
            new AddrModeType {len = 2,    fmt = "%1$s %2$02X,X"},            // AddrMode.ZPX
            new AddrModeType {len = 2,    fmt = "%1$s %2$02X,Y"},            // AddrMode.ZPY
            new AddrModeType {len = 2,    fmt = "%1$s %2$02X"},              // AddrMode.ZP
            new AddrModeType {len = 3,    fmt = "%1$s %3$02X%2$02X"},        // AddrMode.ABS
            new AddrModeType {len = 3,    fmt = "%1$s %3$02X%2$02X,X"},      // AddrMode.ABSX
            new AddrModeType {len = 3,    fmt = "%1$s %3$02X%2$02X,Y"},      // AddrMode.ABSY
            new AddrModeType {len = 3,    fmt = "%1$s (%3$02X%2$02X)"},      // AddrMode.IND1
            new AddrModeType {len = 3,    fmt = "%1$s (%3$02X%2$02X,X)"},    // AddrMode.IND1X
            new AddrModeType {len = 2,    fmt = "%1$s %2$02X,S"},            // AddrMode.SR
            new AddrModeType {len = 2,    fmt = "%1$s (%2$02X,S),Y"},        // AddrMode.ISY
            new AddrModeType {len = 4,    fmt = "%1$s %4$02X%3$02X%2$02X"},  // AddrMode.ABL
            new AddrModeType {len = 4,    fmt = "%1$s %4$02X%3$02X%2$02X,X"},// AddrMode.ABLX
            new AddrModeType {len = 3,    fmt = "%1$s [%3$02X%2$02X]"},      // AddrMode.IAL
            new AddrModeType {len = 3,    fmt = "%1$s %2$s"},                // AddrMode.BRL
            new AddrModeType {len = 3,    fmt = "%1$s %2$02X,%3$02X"},       // AddrMode.BM
            new AddrModeType {len = 1,    fmt = "%1$s"},                     // AddrMode.IMP
            new AddrModeType {len = 1,    fmt = "%1$s A"},                   // AddrMode.IMPA
            new AddrModeType {len = 2,    fmt = "%1$s %2$s"},                // AddrMode.BRA
            new AddrModeType {len = 2,    fmt = "%1$s #%2$02X"},             // AddrMode.IMM8        
            new AddrModeType {len = 2,    fmt = "%1$s #%2$02X"},             // AddrMode.IMMM        
            new AddrModeType {len = 2,    fmt = "%1$s #%2$02X"}              // AddrMode.IMMX        
        };

        public memory_reader_fn memory_read { get; init; }
        public memory_writer_fn memory_write { get; init; }

        string fmt_imm16 = "%1$s #%3$02X%2$02X";



        static readonly string[] x1_ops = {
           "CPX",
           "CPY",
           "LDX",
           "LDY",
           "PHX",
           "PHY",
           "PLX",
           "PLY",
           "STX",
           "STY"
        };

        static readonly string[] m1_ops = {
           "ADC",
           "AND",
           "BIT",
           "CMP",
           "EOR",
           "LDA",
           "ORA",
           "PHA",
           "PLA",
           "SBC",
           "STA",
           "STZ"
        };

        static readonly string[] m2_ops = {
           "ASL",
           "DEC",
           "INC",
           "LAddrMode.SR",
           "ROL",
           "ROR",
           "TSB",
           "TRB"
        };


        public IEnumerable<Registers> em_65816_emulate(byte[] pdata, Registers regsIn, out instruction_t instruction)
        {

            // Unpack the instruction bytes
            byte opcode = pdata[0];

            // lookup the entry for the instruction
            InstrType instr = instr_table[opcode];

            // Work out opcount, taking account of 8/16 bit immediates
            byte opcount = 0;
            if (instr.mode == AddrMode.IMMM)
            {
                if ((instr.m_extra != 0 && regsIn.MS == Tristate.False) || (instr.x_extra != 0 && regsIn.XS == Tristate.False))
                {
                    opcount = 1;
                }
            }
            opcount += (byte)(instr.len - 1);

            byte op1 = (opcount < 1) ? (byte)0 : pdata[1];
            byte op2 = (opcount < 2) ? (byte)0 : pdata[2];
            byte op3 = (opcount < 3) ? (byte)0 : pdata[3];

            instruction = new instruction_t()
            {

                // Save the instruction state
                opcode = opcode,
                op1 = op1,
                op2 = op2,
                op3 = op3,
                opcount = opcount,
                pc = regsIn.PC,
                pb = regsIn.PB
            };

            // DP page wrapping only happens:
            // - in Emulation Mode (E=1), and
            // - if DPL == 00, and
            // - only for old instructions
            bool wrap = regsIn.E == Tristate.True && (regsIn.DP & 0xff) == 0 && (instr.newop) == 0;


            // For instructions that read or write memory, we need to work out the effective address
            // Note: not needed for stack operations, as S is used directly
            int ea = -1;
            bool immediate = false;
            int index;
            switch (instr.mode)
            {
                case AddrMode.ZP:
                    if (regsIn.DP >= 0)
                    {
                        ea = (regsIn.DP + op1) & 0xffff; // always bank 0
                    }
                    break;
                case AddrMode.ZPX:
                case AddrMode.ZPY:
                    index = instr.mode == AddrMode.ZPX ? regsIn.X : regsIn.Y;
                    if (index >= 0 && regsIn.DP >= 0)
                    {
                        if (wrap)
                            ea = (regsIn.DP & 0xff00) + ((op1 + index) & 0xff);
                        else
                            ea = (regsIn.DP + op1 + index) & 0xffff; // always bank 0
                    }
                    break;
                case AddrMode.INDY:
                    index = regsIn.Y;
                    if (index >= 0 && regsIn.DP >= 0)
                    {
                        if (wrap)
                            ea = (regsIn.DP & 0xff00) + (op1 & 0xff);
                        else
                            ea = (regsIn.DP + op1) & 0xffff; // always bank 0
                        ea = regsIn.memory_read16(ea, mem_access_t.MEM_POINTER);
                        if (ea > 0)
                            ea = (index + ea + (regsIn.DB << 16)) & 0xFFFFFF;
                    }
                    break;
                case AddrMode.INDX:
                    index = regsIn.X;
                    if (index >= 0 && regsIn.DP >= 0)
                    {
                        if (wrap)
                            ea = (regsIn.DP & 0xff00) + ((op1 + index) & 0xff);
                        else
                            ea = (regsIn.DP + op1 + index) & 0xffff; // always bank 0
                        ea = regsIn.memory_read16(ea, mem_access_t.MEM_POINTER);
                        if (ea > 0)
                            ea = (ea + (regsIn.DB << 16)) & 0xFFFFFF;
                    }
                    break;
                case AddrMode.IND:
                    if (regsIn.DP >= 0)
                    {
                        if (wrap)
                            ea = (regsIn.DP & 0xff00) + (op1 & 0xff);
                        else
                            ea = (regsIn.DP + op1) & 0xffff; // always bank 0
                        ea = regsIn.memory_read16(ea, mem_access_t.MEM_POINTER);
                        if (ea > 0)
                            ea = (ea + (regsIn.DB << 16)) & 0xFFFFFF;
                    }
                    break;
                case AddrMode.IND16:
                    // 
                    ea = (op2 << 8) + op1;
                    ea = regsIn.memory_read16(ea, mem_access_t.MEM_POINTER);
                    break;
                case AddrMode.IND1X:
                    if (regsIn.X >= 0 && regsIn.PB >= 0)
                    {
                        ea = ((op2 << 8) + op1 + regsIn.X) & 0xFFFF | (regsIn.PB << 16);
                    }
                    ea = regsIn.memory_read16(ea, mem_access_t.MEM_POINTER);
                    break;
                case AddrMode.ABS:
                    if (regsIn.DB >= 0)
                    {
                        ea = (regsIn.DB << 16) + (op2 << 8) + op1;
                    }
                    break;
                case AddrMode.ABSX:
                case AddrMode.ABSY:
                    index = instr.mode == AddrMode.ABSX ? regsIn.X : regsIn.Y;
                    if (index >= 0 && regsIn.DB >= 0)
                    {
                        ea = ((regsIn.DB << 16) + (op2 << 8) + op1 + index) & 0xffffff;
                    }
                    break;
                case AddrMode.BRA:
                    if (regsIn.PC > 0)
                    {
                        ea = (regsIn.PC + ((sbyte)op1) + 2) & 0xffff;
                    }
                    break;
                case AddrMode.SR:
                    // e.g. LDA 08,S
                    if (regsIn.SL >= 0 && regsIn.SH >= 0)
                    {
                        ea = ((regsIn.SH << 8) + regsIn.SL + op1) & 0xffff;
                    }
                    break;
                case AddrMode.ISY:
                    // e.g. LDA (08, S),Y
                    // <opcode> <op1> <internal> <addrlo> <addrhi> <internal> <operand>
                    index = regsIn.Y;
                    if (index >= 0 && regsIn.DB >= 0 && regsIn.SL >= 0 && regsIn.SH >= 0)
                    {
                        ea = ((regsIn.SH << 8) + regsIn.SL + op1) & 0xffff;
                        ea = regsIn.memory_read16(ea, mem_access_t.MEM_POINTER);
                        if (ea >= 0)
                            ea = ((regsIn.DB << 16) + ea + index) & 0xffffff;
                    }
                    break;
                case AddrMode.IDL:
                    // e.g. LDA [80]
                    if (regsIn.DP >= 0)
                    {
                        if (wrap)
                            ea = (regsIn.DP & 0xff00) + (op1 & 0xff);
                        else
                            ea = (regsIn.DP + op1) & 0xffff; // always bank 0
                        ea = regsIn.memory_read24(ea, mem_access_t.MEM_POINTER);
                    }
                    break;
                case AddrMode.IDLY:
                    // e.g. LDA [80],Y
                    // <opcode> <op1> [ <dpextra> ] <addrlo> <addrhi> <bank> <operand>                    
                    index = regsIn.Y;
                    if (index >= 0 && regsIn.DP >= 0)
                    {
                        if (wrap)
                            ea = (regsIn.DP & 0xff00) + (op1 & 0xff);
                        else
                            ea = (regsIn.DP + op1) & 0xffff; // always bank 0
                        ea = (regsIn.memory_read24(ea, mem_access_t.MEM_POINTER) + index) & 0xFFFFFF;
                    }
                    break;
                case AddrMode.ABL:
                    // e.g. LDA EE0080
                    ea = (op3 << 16) + (op2 << 8) + op1;
                    break;
                case AddrMode.ALX:
                    // e.g. LDA EE0080,X
                    if (regsIn.X >= 0)
                    {
                        ea = ((op3 << 16) + (op2 << 8) + op1 + regsIn.X) & 0xffffff;
                    }
                    break;
                case AddrMode.IAL:
                    // e.g. JMP [$12] (this is the only one)
                    // <opcode> <op1> <op2> <addrlo> <addrhi> <bank>
                    ea = op1 + (op2 << 8);
                    ea = regsIn.memory_read24(ea, mem_access_t.MEM_POINTER);
                    break;
                case AddrMode.BRL:
                    // e.g. PER 1234 or AddrMode.BRL 1234
                    if (regsIn.PC > 0)
                    {
                        ea = (regsIn.PC + ((short)((op2 << 8) + op1)) + 3) & 0xffff;
                    }
                    break;
                case AddrMode.BM:
                    // do nothing special case in emulate method
                    break;
                case AddrMode.IMM8:
                    immediate = true;
                    ea = op1;
                    break;
                case AddrMode.IMMM:
                    immediate = true;
                    if (regsIn.MS == Tristate.False)
                        ea = op1 + (op2 <<8);
                    else if (regsIn.MS == Tristate.True)
                        ea = op1;
                    else
                        ea = -1;
                    break;
                case AddrMode.IMMX:
                    immediate = true;
                    if (regsIn.XS == Tristate.False)
                        ea = op1 + (op2 << 8);
                    else if (regsIn.XS == Tristate.True)
                        ea = op1;
                    else
                        ea = -1;
                    break;
                case AddrMode.IMPA:
                case AddrMode.IMP:
                    // covers AddrMode.IMP, AddrMode.IMPA
                    break;
            }

            var ret = regsIn.Clone();
            if (instruction.opcount < 0)
                ret.PC = -1;
            else
                ret.PC = (ret.PC + 1 + instruction.opcount) & 0xFFFF;

            operand_t operand = new operand_t { Immediate = immediate, Ea = ea };
            // Execute the instruction specific function
            // (This returns -1 if the result is unknown or invalid)
            return instr.emulate(ret, operand, instruction);

        }





        // ====================================================================
        // 65816 specific instructions
        // ====================================================================

        // Push Effective Absolute Address
        static IEnumerable<Registers> op_PEA(Registers ret, operand_t operand, instruction_t instr)
        {
            
            // always pushes a 16-bit value
            ret.push16(operand.Ea);
            return new Registers [] { ret } ;
        }

        // Push Effective Relative Address
        static IEnumerable<Registers> op_PER(Registers ret, operand_t operand, instruction_t instr)
        {
            
            // always pushes a 16-bit value
            ret.push16(operand.Ea);
            return new Registers[] { ret };
        }

        // Push Effective Indirect Address
        static IEnumerable<Registers> op_PEI(Registers ret, operand_t operand, instruction_t instr)
        {
            
            // always pushes a 16-bit value
            ret.push16(operand.Ea);
            return new Registers[] { ret };
        }

        // Push Data Bank Register
        static IEnumerable<Registers> op_PHB(Registers ret, operand_t operand, instruction_t instr)
        {
            
            ret.push8(ret.DB);
            return new Registers[] { ret };
        }

        // Push Program Bank Register
        static IEnumerable<Registers> op_PHK(Registers ret, operand_t operand, instruction_t instr)
        {
            
            ret.push8(ret.PB);
            return new Registers[] { ret };
        }

        // Push Direct Page Register
        static IEnumerable<Registers> op_PHD(Registers ret, operand_t operand, instruction_t instr)
        {
            
            ret.push16(ret.DP);
            return new Registers[] { ret };
        }

        // Pull Data Bank Register
        static IEnumerable<Registers> op_PLB(Registers ret, operand_t operand, instruction_t instr)
        {
            
            ret.DB = ret.pop8();
            ret.set_NZ8(ret.DB);
            return new Registers[] { ret };
        }
        
        // Pull Direct Page Register
        static IEnumerable<Registers> op_PLD(Registers ret, operand_t operand, instruction_t instr)
        {
            
            ret.DP = ret.pop16();
            ret.set_NZ16(ret.DP);
            return new [] { ret };
        }

        static void op_MV(Registers regs, int sba, int dba, int dir)
        {

            int C = -1;
            int data;
            // operand is the data byte (from the bus read)
            // ea = (op2 << 8) + op1 == (srcbank << 8) + dstbank;
            do
            {
                data = -1;
                if (regs.X >= 0)
                {
                    data = regs.memory_read((sba << 16) + regs.X, mem_access_t.MEM_DATA);
                }
                if (regs.Y >= 0)
                {
                    regs.memory_write(data, (dba << 16) + regs.Y, mem_access_t.MEM_DATA);
                }
                if (regs.A >= 0 && regs.B >= 0)
                {
                    C = (((regs.B << 8) | regs.A) - 1) & 0xffff;
                    regs.A = C & 0xff;
                    regs.B = (C >> 8) & 0xff;
                    if (regs.X >= 0)
                    {
                        regs.X = (regs.X + dir) & 0xffff;
                    }
                    if (regs.Y >= 0)
                    {
                        regs.Y = (regs.Y + dir) & 0xffff;
                    }
                }
                else
                {
                    regs.A = -1;
                    regs.B = -1;
                    regs.X = -1;
                    regs.Y = -1;
                }
                // Set the Data Bank to the destination bank
                regs.DB = dba;
            } while (C > 0 && C != 0xFFFF);
        }

        // Block Move (Decrementing)
        static IEnumerable<Registers> op_MVP(Registers ret, operand_t operand, instruction_t instr)
        {
            
            op_MV(ret, instr.op1, instr.op2, -1);
            return new[] { ret };
        }

        // Block Move (Incrementing)
        static IEnumerable<Registers> op_MVN(Registers ret, operand_t operand, instruction_t instr)
        {
            
            op_MV(ret, instr.op1, instr.op2, 1);
            return new[] { ret };
        }

        // Transfer Transfer ret.C accumulator to Direct Page register
        static IEnumerable<Registers> op_TCD(Registers ret, operand_t operand, instruction_t instr)
        {
            
            // Always a 16-bit transfer
            if (ret.B >= 0 && ret.A >= 0)
            {
                ret.DP = (ret.B << 8) + ret.A;
                ret.set_NZ16(ret.DP);
            }
            else
            {
                ret.DP = -1;
                ret.set_NZ_unknown();
            }
            return new [] { ret };
        }

        // Transfer Transfer ret.C accumulator to Stack pointer
        static IEnumerable<Registers> op_TCS(Registers ret, operand_t operand, instruction_t instr)
        {
            
            ret.SH = ret.B;
            ret.SL = ret.A;
            return new [] { ret };
        }

        // Transfer Transfer Direct Page register to ret.C accumulator
        static IEnumerable<Registers> op_TDC(Registers ret, operand_t operand, instruction_t instr)
        {
            
            // Always a 16-bit transfer
            if (ret.DP >= 0)
            {
                ret.A = ret.DP & 0xff;
                ret.B = (ret.DP >> 8) & 0xff;
                ret.set_NZ16(ret.DP);
            }
            else
            {
                ret.A = -1;
                ret.B = -1;
                ret.set_NZ_unknown();
            }
            return new [] { ret };
        }

        // Transfer Transfer Stack pointer to ret.C accumulator
        static IEnumerable<Registers> op_TSC(Registers ret, operand_t operand, instruction_t instr)
        {
            
            // Always a 16-bit transfer
            ret.A = ret.SL;
            ret.B = ret.SH;
            if (ret.B >= 0 && ret.A >= 0)
            {
                ret.set_NZ16((ret.B << 8) + ret.A);
            }
            else
            {
                ret.set_NZ_unknown();
            }
            return new [] { ret };
        }

        static IEnumerable<Registers> op_TXY(Registers ret, operand_t operand, instruction_t instr)
        {
            
            // Variable size transfer controlled by ret.XS
            if (ret.X >= 0)
            {
                ret.Y = ret.X;
                ret.set_NZ_XS(ret.Y);
            }
            else
            {
                ret.Y = -1;
                ret.set_NZ_unknown();
            }
            return new [] { ret };
        }

        static IEnumerable<Registers> op_TYX(Registers ret, operand_t operand, instruction_t instr)
        {
            
            // Variable size transfer controlled by ret.XS
            if (ret.Y >= 0)
            {
                ret.X = ret.Y;
                ret.set_NZ_XS(ret.X);
            }
            else
            {
                ret.X = -1;
                ret.set_NZ_unknown();
            }
            return new [] { ret };
        }

        // Exchange ret.A and ret.B
        static IEnumerable<Registers> op_XBA(Registers ret, operand_t operand, instruction_t instr)
        {
            
            int tmp = ret.A;
            ret.A = ret.B;
            ret.B = tmp;
            if (ret.A >= 0)
            {
                // Always based on the 8-bit result of ret.A
                ret.set_NZ8(ret.A);
            }
            else
            {
                ret.set_NZ_unknown();
            }
            return new [] { ret };
        }

        static IEnumerable<Registers> op_XCE(Registers ret, operand_t operand, instruction_t instr)
        {
            
            Tristate tmp = ret.C;
            ret.C = ret.E;
            ret.E = tmp;
            if (tmp == Tristate.Unknown)
            {
                ret.MS = Tristate.Unknown;
                ret.XS = Tristate.Unknown;
                ret.E = Tristate.Unknown;
            }
            else if (tmp == Tristate.True)
            {
                ret.emulation_mode_on();
            }
            else
            {
                ret.emulation_mode_off();
            }
            return new [] { ret };
        }

        static void repsep(Registers regs, int operand, Tristate val)
        {
            if ((operand & 0x80) != 0)
            {
                regs.N = val;
            }
            if ((operand & 0x40) != 0)
            {
                regs.V = val;
            }
            if (regs.E == Tristate.False)
            {
                if ((operand & 0x20) != 0)
                {
                    regs.MS = val;
                }
                if ((operand & 0x10) != 0)
                {
                    regs.XS = val;
                }
            }
            if ((operand & 0x08) != 0)
            {
                regs.D = val;
            }
            if ((operand & 0x04) != 0)
            {
                regs.I = val;
            }
            if ((operand & 0x02) != 0)
            {
                regs.Z = val;
            }
            if ((operand & 0x01) != 0)
            {
                regs.C = val;
            }
        }

        // Reset/Set Processor Status Bits
        static IEnumerable<Registers> op_REP(Registers ret, operand_t operand, instruction_t instr)
        {
            
            repsep(ret, operand.Ea, Tristate.False);
            return new [] { ret };
        }

        static IEnumerable<Registers> op_SEP(Registers ret, operand_t operand, instruction_t instr)
        {
            
            repsep(ret, operand.Ea, Tristate.False);
            return new [] { ret };
        }

        // Jump to Subroutine Long
        static IEnumerable<Registers> op_JSL(Registers ret, operand_t operand, instruction_t instr)
        {
            
            ret.push8(ret.PB); 
            ret.push16(ret.PC);

            if (operand.Ea >=0 )
            {
                ret.PC = operand.Ea & 0xFFFF;
                ret.PB = (operand.Ea & 0xFF0000) >> 16;
            } else
            {
                ret.PC = -1;
                ret.PB = -1;
            }

            return new [] { ret };
        }

        // Return from Subroutine Long
        static IEnumerable<Registers> op_RTL(Registers ret, operand_t operand, instruction_t instr)
        {
            
            // RTL: the operand is the data pulled from the stack (PCL, PCH, PB)
            ret.PC = ret.pop16();
            ret.PB = ret.pop8();

            if (ret.PC >= 0)
                ret.PC = (ret.PC + 1 ) & 0xFFFF;

            return new [] { ret };
        }

        // ====================================================================
        // 65816/6502 instructions
        // ====================================================================

        static IEnumerable<Registers> op_ADC(Registers ret, operand_t operand, instruction_t instr)
        {

            var val = operand.GetValue(ret, ret.MS, mem_access_t.MEM_DATA);

            int acc = ret.get_accumulator();
            if (acc >= 0 && ret.C != Tristate.Unknown && ret.D != Tristate.Unknown && val >= 0)
            {
                int tmp = 0;
                if (ret.D == Tristate.True)
                {
                    // Decimal mode ADC - works like a 65C02
                    // Working a nibble at a time, correct for both 8 and 18 bits
                    for (int bit = 0; bit < (ret.MS == Tristate.True ? 8 : 16); bit += 4)
                    {
                        int an = (acc >> bit) & 0xF;
                        int bn = (val >> bit) & 0xF;
                        int rn = an + bn + (ret.C == Tristate.True ? 1 : 0);
                        ret.V = FromBool(((rn ^ an) & 8) != 0 && ((bn ^ an) & 8) == 0);
                        ret.C = 0;
                        if (rn >= 10)
                        {
                            rn = (rn - 10) & 0xF;
                            ret.C = Tristate.True;
                        }
                        tmp |= rn << bit;
                    }
                }
                else
                {
                    // Normal mode ADC
                    tmp = acc + val + (ret.C == Tristate.True ? 1 : 0); ;
                    if (ret.MS == Tristate.True)
                    {
                        // 8-bit mode
                        ret.C = FromBool(((tmp >> 8) & 1) != 0);
                        ret.V = FromBool(((acc ^ val) & 0x80) == 0 && ((acc ^ tmp) & 0x80) != 0);
                    }
                    else
                    {
                        // 16-bit mode
                        ret.C = FromBool(((tmp >> 16) & 1) != 0);
                        ret.V = FromBool(((acc ^ val) & 0x8000) == 0 && ((acc ^ tmp) & 0x8000) != 0);
                    }
                }
                if (ret.MS == Tristate.True)
                {
                    // 8-bit mode
                    ret.A = tmp & 0xff;
                }
                else
                {
                    // 16-bit mode
                    ret.A = tmp & 0xff;
                    ret.B = (tmp >> 8) & 0xff;
                }
                ret.set_NZ_AB(ret.A, ret.B);
            }
            else
            {
                ret.A = -1;
                ret.B = -1;
                ret.set_NVZC_unknown();
            }
            return new [] { ret };
        }

        static IEnumerable<Registers> op_AND(Registers ret, operand_t operand, instruction_t instr)
        {

            var val = operand.GetValue(ret, ret.MS, mem_access_t.MEM_DATA);

            // ret.A is always updated, regardless of the size
            if (ret.A >= 0)
            {
                if (val >= 0)
                    ret.A = ret.A & (val & 0xff);
                else
                    ret.A = -1;
            }
            // ret.B is updated only of the size is 16
            if (ret.B >= 0)
            {
                if (ret.MS == Tristate.False)
                {
                    if (val >= 0)
                        ret.B = ret.B & (val >> 8);
                    else
                        ret.B = -1;
                }
                else if (ret.MS == Tristate.Unknown)
                {
                    ret.B = -1;
                }
            }
            // Updating NZ is complex, depending on the whether ret.A and/or ret.B are unknown
            ret.set_NZ_AB(ret.A, ret.B);
            return new [] { ret };
        }

        static IEnumerable<Registers> op_ASLA(Registers ret, operand_t operand, instruction_t instr)
        {

            // Compute the new carry
            if (ret.MS == Tristate.True && ret.A >= 0)
            {
                // 8-bit mode
                ret.C = FromBool(((ret.A >> 7) & 1) != 0);
            }
            else if (ret.MS == Tristate.False && ret.B >= 0)
            {
                // 16-bit mode
                ret.C = FromBool(((ret.B >> 7) & 1) != 0);
            }
            else
            {
                // width unknown
                ret.C = Tristate.Unknown;
            }
            // Compute the new ret.B
            if (ret.MS == Tristate.False && ret.B >= 0)
            {
                if (ret.A >= 0)
                {
                    ret.B = ((ret.B << 1) & 0xfe) | ((ret.A >> 7) & 1);
                }
                else
                {
                    ret.B = -1;
                }
            }
            else if (ret.MS == Tristate.Unknown)
            {
                ret.B = -1;
            }
            // Compute the new ret.A
            if (ret.A >= 0)
            {
                ret.A = (ret.A << 1) & 0xff;
            }
            // Updating NZ is complex, depending on the whether ret.A and/or ret.B are unknown
            ret.set_NZ_AB(ret.A, ret.B);
            return new [] { ret };
        }

        static IEnumerable<Registers> op_ASL(Registers ret, operand_t operand, instruction_t instr)
        {
            var val = operand.GetValue(ret, ret.MS, mem_access_t.MEM_DATA);

            int tmp;
            if (ret.MS == Tristate.True && val >= 0)
            {
                // 8-bit mode
                ret.C = FromBool(((val >> 7) & 1) != 0);
                tmp = (val << 1) & 0xff;
                ret.set_NZ8(tmp);
            }
            else if (ret.MS == Tristate.False && val >= 0)
            {
                // 16-bit mode
                ret.C = FromBool(((val >> 15) & 1) != 0);
                tmp = (val << 1) & 0xffff;
                ret.set_NZ16(tmp);
            }
            else
            {
                // mode unknown
                ret.C = Tristate.Unknown;
                tmp = -1;
                ret.set_NZ_unknown();
            }

            operand.SetValue(ret, ret.MS, mem_access_t.MEM_DATA, tmp);

            return new[] { ret };
        }

        static IEnumerable<Registers> op_BCC(Registers ret, operand_t operand, instruction_t instr)
        {
            if (ret.C == Tristate.Unknown)
                ret.PC = -1;
            else if (ret.C == Tristate.False)
                ret.PC = operand.Ea;

            return new[] { ret };
        }

        static IEnumerable<Registers> op_BCS(Registers ret, operand_t operand, instruction_t instr)
        {
            if (ret.C == Tristate.Unknown)
                ret.PC = -1;
            else if (ret.C == Tristate.True)
                ret.PC = operand.Ea;

            return new[] { ret };
        }

        static IEnumerable<Registers> op_BNE(Registers ret, operand_t operand, instruction_t instr)
        {

            if (ret.Z == Tristate.Unknown)
                ret.PC = -1;
            else if (ret.Z == Tristate.False)
                ret.PC = operand.Ea;

            return new[] { ret };
        }

        static IEnumerable<Registers> op_BEQ(Registers ret, operand_t operand, instruction_t instr)
        {
            if (ret.Z == Tristate.Unknown)
                ret.PC = -1;
            else if (ret.Z == Tristate.True)
                ret.PC = operand.Ea;

            return new[] { ret };
        }

        static IEnumerable<Registers> op_BPL(Registers ret, operand_t operand, instruction_t instr)
        {

            if (ret.N == Tristate.Unknown)
                ret.PC = -1;
            else if (ret.N == Tristate.False)
                ret.PC = operand.Ea;

            return new[] { ret };
        }

        static IEnumerable<Registers> op_BMI(Registers ret, operand_t operand, instruction_t instr)
        {

            if (ret.N == Tristate.Unknown)
                ret.PC = -1;
            else if (ret.N == Tristate.True)
                ret.PC = operand.Ea;

            return new[] { ret };
        }

        static IEnumerable<Registers> op_BVC(Registers ret, operand_t operand, instruction_t instr)
        {

            if (ret.V == Tristate.Unknown)
                ret.PC = -1;
            else if (ret.V == Tristate.False)
                ret.PC = operand.Ea;

            return new[] { ret };
        }

        static IEnumerable<Registers> op_BVS(Registers ret, operand_t operand, instruction_t instr)
        {

            if (ret.V == Tristate.Unknown)
                ret.PC = -1;
            else if (ret.V == Tristate.True)
                ret.PC = operand.Ea;

            return new[] { ret };
        }

        static IEnumerable<Registers> op_BRA(Registers ret, operand_t operand, instruction_t instr)
        {
            ret.PC = operand.Ea;
            return new[] { ret };
        }

        static IEnumerable<Registers> op_BRL(Registers ret, operand_t operand, instruction_t instr)
        {
            ret.PC = operand.Ea;
            return new[] { ret };
        }

        static IEnumerable<Registers> op_BIT_IMM(Registers ret, operand_t operand, instruction_t instr)
        {

            var val = operand.Ea;
            int acc = ret.get_accumulator();
            if (val == 0 || acc == 0)
            {
                // This makes the remainder less pessimistic
                ret.Z = Tristate.True;
            }
            else if (acc >= 0)
            {
                // both acc and operand will be the correct width
                ret.Z = FromBool((acc & val) == 0);
            }
            else
            {
                ret.Z = Tristate.Unknown;
            }
            return new [] { ret };
        }

        static IEnumerable<Registers> op_BIT(Registers ret, operand_t operand, instruction_t instr)
        {

            var val = operand.GetValue(ret, ret.MS, mem_access_t.MEM_DATA);
            int acc = ret.get_accumulator();
            if (val == 0 || acc == 0)
            {
                // This makes the remainder less pessimistic
                ret.Z = Tristate.True;
            }
            else if (acc >= 0)
            {
                // both acc and operand will be the correct width
                ret.Z = FromBool((acc & val) == 0);
            }
            else
            {
                ret.Z = Tristate.Unknown;
            }

            if (ret.MS == Tristate.Unknown || val < 0)
            {
                ret.N = Tristate.Unknown;
                ret.V = Tristate.Unknown;
            }
            else if (ret.MS == Tristate.True)
            {
                ret.N = FromBool((val & 0x80) != 0);
                ret.V = FromBool((val & 0x40) != 0);
            }
            else if (ret.MS == Tristate.False)
            {
                ret.N = FromBool((val & 0x8000) != 0);
                ret.V = FromBool((val & 0x4000) != 0);
            }

            return new[] { ret };

        }

        static IEnumerable<Registers> op_CLC(Registers ret, operand_t operand, instruction_t instr)
        {
            
            ret.C = 0;
            return new [] { ret };
        }

        static IEnumerable<Registers> op_CLD(Registers ret, operand_t operand, instruction_t instr)
        {
            
            ret.D = 0;
            return new [] { ret };
        }

        static IEnumerable<Registers> op_CLI(Registers ret, operand_t operand, instruction_t instr)
        {
            
            ret.I = 0;
            return new [] { ret };
        }

        static IEnumerable<Registers> op_CLV(Registers ret, operand_t operand, instruction_t instr)
        {
            
            ret.V = 0;
            return new [] { ret };
        }

        static IEnumerable<Registers> op_CMP(Registers ret, operand_t operand, instruction_t instr)
        {
            var val = operand.GetValue(ret, ret.MS, mem_access_t.MEM_DATA);
            int acc = ret.get_accumulator();
            if (acc >= 0 && val >= 0)
            {
                int tmp = acc - val;
                ret.C = FromBool(tmp >= 0);
                ret.set_NZ_MS(tmp);
            }
            else
            {
                ret.set_NZC_unknown();
            }
            return new [] { ret };
        }

        static IEnumerable<Registers> op_CPX(Registers ret, operand_t operand, instruction_t instr)
        {

            var val = operand.GetValue(ret, ret.XS, mem_access_t.MEM_DATA);

            if (ret.X >= 0 && val >= 0)
            {
                int tmp = ret.X - val;
                ret.C = FromBool(tmp >= 0);
                ret.set_NZ_XS(tmp);
            }
            else
            {
                ret.set_NZC_unknown();
            }
            return new [] { ret };
        }

        static IEnumerable<Registers> op_CPY(Registers ret, operand_t operand, instruction_t instr)
        {

            var val = operand.GetValue(ret, ret.XS, mem_access_t.MEM_DATA);

            if (ret.Y >= 0 && val >= 0)
            {
                int tmp = ret.Y - val;
                ret.C = FromBool(tmp >= 0);
                ret.set_NZ_XS(tmp);
            }
            else
            {
                ret.set_NZC_unknown();
            }
            return new [] { ret };
        }

        static IEnumerable<Registers> op_DECA(Registers ret, operand_t operand, instruction_t instr)
        {
            
            // Compute the new ret.A
            if (ret.A >= 0)
            {
                ret.A = (ret.A - 1) & 0xff;
            }
            // Compute the new ret.B
            if (ret.MS == Tristate.False && ret.B >= 0)
            {
                if (ret.A == 0xff)
                {
                    ret.B = (ret.B - 1) & 0xff;
                }
                else if (ret.A < 0)
                {
                    ret.B = -1;
                }
            }
            else if (ret.MS == Tristate.Unknown)
            {
                ret.B = -1;
            }
            // Updating NZ is complex, depending on the whether ret.A and/or ret.B are unknown
            ret.set_NZ_AB(ret.A, ret.B);
            return new [] { ret };
        }

        static IEnumerable<Registers> op_DEC(Registers ret, operand_t operand, instruction_t instr)
        {

            var val = operand.GetValue(ret, ret.MS, mem_access_t.MEM_DATA);

            int tmp = -1;
            if (ret.MS == Tristate.True)
            {
                // 8-bit mode
                tmp = (val - 1) & 0xff;
                ret.set_NZ8(tmp);
            }
            else if (ret.MS == Tristate.False)
            {
                // 16-bit mode
                tmp = (val - 1) & 0xffff;
                ret.set_NZ16(tmp);
            }
            else
            {
                ret.set_NZ_unknown();
            }

            operand.SetValue(ret, ret.MS, mem_access_t.MEM_DATA, tmp);

            return new[] { ret };
        }

        static IEnumerable<Registers> op_DEX(Registers ret, operand_t operand, instruction_t instr)
        {
            
            if (ret.X >= 0)
            {
                if (ret.XS == Tristate.True)
                {
                    // 8-bit mode
                    ret.X = (ret.X - 1) & 0xff;
                    ret.set_NZ8(ret.X);
                }
                else if (ret.XS == Tristate.False)
                {
                    // 16-bit mode
                    ret.X = (ret.X - 1) & 0xffff;
                    ret.set_NZ16(ret.X);
                }
                else
                {
                    // mode undefined
                    ret.X = -1;
                    ret.set_NZ_unknown();
                }
            }
            else
            {
                ret.set_NZ_unknown();
            }
            return new [] { ret };
        }

        static IEnumerable<Registers> op_DEY(Registers ret, operand_t operand, instruction_t instr)
        {
            
            if (ret.Y >= 0)
            {
                if (ret.XS == Tristate.True)
                {
                    // 8-bit mode
                    ret.Y = (ret.Y - 1) & 0xff;
                    ret.set_NZ8(ret.Y);
                }
                else if (ret.XS == Tristate.False)
                {
                    // 16-bit mode
                    ret.Y = (ret.Y - 1) & 0xffff;
                    ret.set_NZ16(ret.Y);
                }
                else
                {
                    // mode undefined
                    ret.Y = -1;
                    ret.set_NZ_unknown();
                }
            }
            else
            {
                ret.set_NZ_unknown();
            }
            return new [] { ret };
        }

        static IEnumerable<Registers> op_EOR(Registers ret, operand_t operand, instruction_t instr)
        {
            var val = operand.GetValue(ret, ret.MS, mem_access_t.MEM_DATA);

            // ret.A is always updated, regardless of the size
            if (ret.A >= 0)
            {
                ret.A = ret.A ^ (val & 0xff);
            }
            // ret.B is updated only of the size is 16
            if (ret.B >= 0)
            {
                if (ret.MS == Tristate.False)
                {
                    ret.B = ret.B ^ (val >> 8);
                }
                else if (ret.MS == Tristate.Unknown)
                {
                    ret.B = -1;
                }
            }
            // Updating NZ is complex, depending on the whether ret.A and/or ret.B are unknown
            ret.set_NZ_AB(ret.A, ret.B);
            return new [] { ret };
        }

        static IEnumerable<Registers> op_INCA(Registers ret, operand_t operand, instruction_t instr)
        {
            
            // Compute the new ret.A
            if (ret.A >= 0)
            {
                ret.A = (ret.A + 1) & 0xff;
            }
            // Compute the new ret.B
            if (ret.MS == Tristate.False && ret.B >= 0)
            {
                if (ret.A == 0x00)
                {
                    ret.B = (ret.B + 1) & 0xff;
                }
                else if (ret.A < 0)
                {
                    ret.B = -1;
                }
            }
            else if (ret.MS == Tristate.Unknown)
            {
                ret.B = -1;
            }
            // Updating NZ is complex, depending on the whether ret.A and/or ret.B are unknown
            ret.set_NZ_AB(ret.A, ret.B);
            return new [] { ret };
        }

        static IEnumerable<Registers> op_INC(Registers ret, operand_t operand, instruction_t instr)
        {

            var val = operand.GetValue(ret, ret.MS, mem_access_t.MEM_DATA);

            int tmp = -1;
            if (ret.MS == Tristate.True)
            {
                // 8-bit mode
                tmp = (val + 1) & 0xff;
                ret.set_NZ8(tmp);
            }
            else if (ret.MS == Tristate.False)
            {
                // 16-bit mode
                tmp = (val + 1) & 0xffff;
                ret.set_NZ16(tmp);
            }
            else
            {
                ret.set_NZ_unknown();
            }

            operand.SetValue(ret, ret.MS, mem_access_t.MEM_DATA, tmp);
            return new[] { ret };
        }

        static IEnumerable<Registers> op_INX(Registers ret, operand_t operand, instruction_t instr)
        {
            
            if (ret.X >= 0)
            {
                if (ret.XS == Tristate.True)
                {
                    // 8-bit mode
                    ret.X = (ret.X + 1) & 0xff;
                    ret.set_NZ8(ret.X);
                }
                else if (ret.XS == Tristate.False)
                {
                    // 16-bit mode
                    ret.X = (ret.X + 1) & 0xffff;
                    ret.set_NZ16(ret.X);
                }
                else
                {
                    // mode undefined
                    ret.X = -1;
                    ret.set_NZ_unknown();
                }
            }
            else
            {
                ret.set_NZ_unknown();
            }
            return new [] { ret };
        }

        static IEnumerable<Registers> op_INY(Registers ret, operand_t operand, instruction_t instr)
        {
            
            if (ret.Y >= 0)
            {
                if (ret.XS == Tristate.True)
                {
                    // 8-bit mode
                    ret.Y = (ret.Y + 1) & 0xff;
                    ret.set_NZ8(ret.Y);
                }
                else if (ret.XS == Tristate.False)
                {
                    // 16-bit mode
                    ret.Y = (ret.Y + 1) & 0xffff;
                    ret.set_NZ16(ret.Y);
                }
                else
                {
                    // mode undefined
                    ret.Y = -1;
                    ret.set_NZ_unknown();
                }
            }
            else
            {
                ret.set_NZ_unknown();
            }
            return new [] { ret };
        }

        static IEnumerable<Registers> op_JSR(Registers ret, operand_t operand, instruction_t instr)
        {
               
            // JAddrMode.SR: the operand is the data ret.pushed to the stack (PCH, PCL)
            ret.push16(ret.PC-1);  // ret.PC
            ret.PC = operand.Ea;
            return new [] { ret };
        }

        static IEnumerable<Registers> op_LDA(Registers ret, operand_t operand, instruction_t instr)
        {
            var val = operand.GetValue(ret, ret.MS, mem_access_t.MEM_DATA);
            if (val >= 0)
                ret.A = val & 0xff;
            else
                ret.A = -1;

            if (ret.MS == Tristate.False)
            {
                if (val >= 0)
                    ret.B = (val >> 8) & 0xff;
                else
                    ret.B = -1;
            } else if (ret.MS == Tristate.Unknown)
            {
                ret.B = -1;
            }
            ret.set_NZ_AB(ret.A, ret.B);
            return new [] { ret };
        }

        static IEnumerable<Registers> op_LDX(Registers ret, operand_t operand, instruction_t instr)
        {

            var val = operand.GetValue(ret, ret.XS, mem_access_t.MEM_DATA);
            ret.X = val;
            ret.set_NZ_XS(ret.X);
            return new [] { ret };
        }

        static IEnumerable<Registers> op_LDY(Registers ret, operand_t operand, instruction_t instr)
        {

            var val = operand.GetValue(ret, ret.XS, mem_access_t.MEM_DATA);
            ret.Y = val;
            ret.set_NZ_XS(ret.Y);
            return new [] { ret };
        }

        static IEnumerable<Registers> op_LSRA(Registers ret, operand_t operand, instruction_t instr)
        {
            
            // Compute the new carry
            if (ret.A >= 0)
            {
                ret.C = FromBool((ret.A & 1) != 0);
            }
            else
            {
                ret.C = Tristate.Unknown;
            }
            // Compute the new ret.A
            if (ret.MS == Tristate.True && ret.A >= 0)
            {
                ret.A = ret.A >> 1;
            }
            else if (ret.MS == Tristate.False && ret.A >= 0 && ret.B >= 0)
            {
                ret.A = ((ret.A >> 1) | (ret.B << 7)) & 0xff;
            }
            else
            {
                ret.A = -1;
            }
            // Compute the new ret.B
            if (ret.MS == Tristate.False && ret.B >= 0)
            {
                ret.B = (ret.B >> 1) & 0xff;
            }
            else if (ret.MS == Tristate.Unknown)
            {
                ret.B = -1;
            }
            // Updating NZ is complex, depending on the whether ret.A and/or ret.B are unknown
            ret.set_NZ_AB(ret.A, ret.B);
            return new [] { ret };
        }

        static IEnumerable<Registers> op_LSR(Registers ret, operand_t operand, instruction_t instr)
        {

            var val = operand.GetValue(ret, ret.MS, mem_access_t.MEM_DATA);

            int tmp;
            ret.C = FromBool((val & 1) != 0);
            if (ret.MS == Tristate.True)
            {
                // 8-bit mode
                tmp = (val >> 1) & 0xff;
                ret.set_NZ8(tmp);
            }
            else if (ret.MS == Tristate.False)
            {
                // 16-bit mode
                tmp = (val >> 1) & 0xffff;
                ret.set_NZ16(tmp);
            }
            else
            {
                // mode unknown
                tmp = -1;
                ret.set_NZ_unknown();
            }
            operand.SetValue(ret, ret.MS, mem_access_t.MEM_DATA, tmp);
            return new[] { ret };
        }

        static IEnumerable<Registers> op_ORA(Registers ret, operand_t operand, instruction_t instr)
        {
            var val = operand.GetValue(ret, ret.MS, mem_access_t.MEM_DATA);

            // ret.A is always updated, regardless of the size
            if (ret.A >= 0)
            {
                ret.A = ret.A | (val & 0xff);
            }
            // ret.B is updated only of the size is 16
            if (ret.B >= 0)
            {
                if (ret.MS == Tristate.False)
                {
                    ret.B = ret.B | (val >> 8);
                }
                else if (ret.MS == Tristate.Unknown)
                {
                    ret.B = -1;
                }
            }
            // Updating NZ is complex, depending on the whether ret.A and/or ret.B are unknown
            ret.set_NZ_AB(ret.A, ret.B);
            return new [] { ret };
        }

        static IEnumerable<Registers> op_PHA(Registers ret, operand_t operand, instruction_t instr)
        {
            var acc = ret.get_accumulator();
            ret.pushMS(acc);
            return new [] { ret };
        }

        static IEnumerable<Registers> op_PHP(Registers ret, operand_t operand, instruction_t instr)
        {            
            ret.push8(ret.get_FLAGS());
            return new [] { ret };
        }

        static IEnumerable<Registers> op_PHX(Registers ret, operand_t operand, instruction_t instr)
        {
            
            ret.pushXS(ret.X);
            return new [] { ret };
        }

        static IEnumerable<Registers> op_PHY(Registers ret, operand_t operand, instruction_t instr)
        {
            ret.pushXS(ret.Y);
            return new [] { ret };
        }

        static IEnumerable<Registers> op_PLA(Registers ret, operand_t operand, instruction_t instr)
        {
            ret.A = ret.popMS();
            ret.set_NZ_MS(ret.A);
            return new [] { ret };
        }

        static IEnumerable<Registers> op_PLP(Registers ret, operand_t operand, instruction_t instr)
        {
            var tmp = ret.pop8();
            ret.set_FLAGS(tmp);
            return new [] { ret };
        }

        static IEnumerable<Registers> op_PLX(Registers ret, operand_t operand, instruction_t instr)
        {
            ret.X = ret.popXS();
            ret.set_NZ_XS(ret.X);
            return new [] { ret };
        }

        static IEnumerable<Registers> op_PLY(Registers ret, operand_t operand, instruction_t instr)
        {
            ret.Y = ret.popXS();
            ret.set_NZ_XS(ret.Y);
            return new [] { ret };
        }

        static IEnumerable<Registers> op_ROLA(Registers ret, operand_t operand, instruction_t instr)
        {
            
            // Save the old carry
            Tristate oldC = ret.C;
            // Compute the new carry
            if (ret.MS == Tristate.True && ret.A >= 0)
            {
                // 8-bit mode
                ret.C = FromBool(((ret.A >> 7) & 1) != 0);
            }
            else if (ret.MS == Tristate.False && ret.B >= 0)
            {
                // 16-bit mode
                ret.C = FromBool(((ret.B >> 7) & 1) != 0);
            }
            else
            {
                // width unknown
                ret.C = Tristate.Unknown;
            }
            // Compute the new ret.B
            if (ret.MS == Tristate.False && ret.B >= 0)
            {
                if (ret.A >= 0)
                {
                    ret.B = ((ret.B << 1) & 0xfe) | ((ret.A >> 7) & 1);
                }
                else
                {
                    ret.B = -1;
                }
            }
            else if (ret.MS == Tristate.Unknown)
            {
                ret.B = -1;
            }
            // Compute the new ret.A
            if (ret.A >= 0)
            {
                if (oldC != Tristate.Unknown)
                {
                    ret.A = ((ret.A << 1) | ((oldC == Tristate.True) ? 1 : 0)) & 0xff;
                }
                else
                {
                    ret.A = -1;
                }
            }
            // Updating NZ is complex, depending on the whether ret.A and/or ret.B are unknown
            ret.set_NZ_AB(ret.A, ret.B);
            return new [] { ret };
        }

        static IEnumerable<Registers> op_ROL(Registers ret, operand_t operand, instruction_t instr)
        {

            var val = operand.GetValue(ret, ret.MS, mem_access_t.MEM_DATA);

            Tristate oldC = ret.C;
            int tmp;
            if (ret.MS == Tristate.True && oldC != Tristate.Unknown)
            {
                // 8-bit mode
                ret.C = FromBool(((val >> 7) & 1) != 0);
                tmp = ((val << 1) | (int)oldC) & 0xff;
                ret.set_NZ8(tmp);
            }
            else if (ret.MS == Tristate.False && oldC != Tristate.Unknown)
            {
                // 16-bit mode
                ret.C = FromBool(((val >> 15) & 1) != 0);
                tmp = ((val << 1) | (int)oldC) & 0xffff;
                ret.set_NZ16(tmp);
            }
            else
            {
                ret.C = Tristate.Unknown;
                tmp = -1;
                ret.set_NZ_unknown();
            }

            operand.SetValue(ret, ret.MS, mem_access_t.MEM_DATA, tmp);
            return new[] { ret };
        }

        static IEnumerable<Registers> op_RORA(Registers ret, operand_t operand, instruction_t instr)
        {
            
            // Save the old carry
            Tristate oldC = ret.C;
            // Compute the new carry
            if (ret.A >= 0)
                ret.C = FromBool((ret.A & 1) != 0);
            else
                ret.C = Tristate.Unknown;

            // Compute the new ret.A
            if (ret.MS == Tristate.True && ret.A >= 0 && ret.C != Tristate.Unknown)
            {
                ret.A = ((ret.A >> 1) | ((int)oldC << 7)) & 0xff;
            }
            else if (ret.MS == Tristate.False && ret.A >= 0 && ret.B >= 0)
            {
                ret.A = ((ret.A >> 1) | (ret.B << 7)) & 0xff;
            }
            else
            {
                ret.A = -1;
            }
            // Compute the new ret.B
            if (ret.MS == Tristate.False && ret.B >= 0 && oldC != Tristate.Unknown)
            {
                ret.B = ((ret.B >> 1) | ((int)oldC << 7)) & 0xff;
            }
            else if (ret.MS == Tristate.Unknown)
            {
                ret.B = -1;
            }
            else if (ret.MS == Tristate.False && oldC == Tristate.Unknown)
            {
                ret.B = -1;
            }
            // Updating NZ is complex, depending on the whether ret.A and/or ret.B are unknown
            ret.set_NZ_AB(ret.A, ret.B);
            return new [] { ret };
        }

        static IEnumerable<Registers> op_ROR(Registers ret, operand_t operand, instruction_t instr)
        {
            var val = operand.GetValue(ret, ret.MS, mem_access_t.MEM_DATA);

            Tristate oldC = ret.C;
            int tmp;
            ret.C = FromBool((val & 1) != 0);
            if (ret.MS == Tristate.True)
            {
                // 8-bit mode
                tmp = ((val >> 1) | ((int)oldC << 7)) & 0xff;
                ret.set_NZ8(tmp);
            }
            else if (ret.MS == Tristate.False)
            {
                // 16-bit mode
                tmp = ((val >> 1) | ((int)oldC << 15)) & 0xffff;
                ret.set_NZ16(tmp);
            }
            else
            {
                ret.C = Tristate.Unknown;
                tmp = -1;
                ret.set_NZ_unknown();
            }
            operand.SetValue(ret, ret.MS, mem_access_t.MEM_DATA, tmp);
            return new[] { ret };
        }

        static IEnumerable<Registers> op_RTS(Registers ret, operand_t operand, instruction_t instr)
        {
            
            // RTS: the operand is the data pulled from the stack (PCL, PCH)
            var x = ret.pop16();

            if (x < 0)
                ret.PC = -1;
            else
                ret.PC = (x + 1) & 0xFFFF;
            return new [] { ret };
        }

        static IEnumerable<Registers> op_RTI(Registers ret, operand_t operand, instruction_t instr)
        {
            
            // RTI: the operand is the data pulled from the stack (P, PCL, PCH, PBR)
            ret.set_FLAGS(ret.pop8());
            ret.PC = ret.pop16();
            if (ret.E == Tristate.Unknown)
            {
                ret.SH = -1;
                ret.SL = -1;
            }
            else if (ret.E == Tristate.False)
            {
                ret.PB = ret.pop8();
            }
            return new [] { ret };
        }

        static IEnumerable<Registers> op_SBC(Registers ret, operand_t operand, instruction_t instr)
        {
            var val = operand.GetValue(ret, ret.MS, mem_access_t.MEM_DATA);
            int acc = ret.get_accumulator();
            if (acc >= 0 && ret.C != Tristate.Unknown && ret.MS != Tristate.Unknown)
            {
                int tmp = 0;
                if (ret.D == Tristate.True)
                {
                    // Decimal mode SBC - works like a 65C02
                    // Working a nibble at a time, correct for both 8 and 18 bits
                    for (int bit = 0; bit < (ret.MS == Tristate.True ? 8 : 16); bit += 4)
                    {
                        int an = (acc >> bit) & 0xF;
                        int bn = (val >> bit) & 0xF;
                        int rn = an - bn - (1 - (int)ret.C);
                        ret.V = FromBool(((rn ^ an) & 8) != 0 && ((bn ^ an) & 8) != 0);
                        ret.C = Tristate.True;
                        if (rn < 0)
                        {
                            rn = (rn + 10) & 0xF;
                            ret.C = 0;
                        }
                        tmp |= rn << bit;
                    }
                }
                else
                {
                    // Normal mode SBC
                    tmp = acc - val - (1 - (int)ret.C);
                    if (ret.MS == Tristate.True)
                    {
                        // 8-bit mode
                        ret.C = FromBool(((tmp >> 8) & 1) == 0);
                        ret.V = FromBool(((acc ^ val) & 0x80) != 0 && ((acc ^ tmp) & 0x80) != 0);
                    }
                    else
                    {
                        // 16-bit mode
                        ret.C = FromBool(((tmp >> 16) & 1) == 0);
                        ret.V = FromBool(((acc ^ val) & 0x8000) != 0 && ((acc ^ tmp) & 0x8000) != 0);
                    }
                }
                if (ret.MS == Tristate.True)
                {
                    // 8-bit mode
                    ret.A = tmp & 0xff;
                }
                else
                {
                    // 16-bit mode
                    ret.A = tmp & 0xff;
                    ret.B = (tmp >> 8) & 0xff;
                }
                ret.set_NZ_AB(ret.A, ret.B);
            }
            else
            {
                ret.A = -1;
                ret.B = -1;
                ret.set_NVZC_unknown();
            }
            return new [] { ret };
        }

        static IEnumerable<Registers> op_SEC(Registers ret, operand_t operand, instruction_t instr)
        {
            
            ret.C = Tristate.True;
            return new [] { ret };
        }

        static IEnumerable<Registers> op_SED(Registers ret, operand_t operand, instruction_t instr)
        {
            
            ret.D = Tristate.True;
            return new [] { ret };
        }

        static IEnumerable<Registers> op_SEI(Registers ret, operand_t operand, instruction_t instr)
        {
            
            ret.I = Tristate.True;
            return new [] { ret };
        }

        static IEnumerable<Registers> op_STA(Registers ret, operand_t operand, instruction_t instr)
        {

            var acc = ret.get_accumulator();
            operand.SetValue(ret, ret.MS, mem_access_t.MEM_DATA, acc);

            return new[] { ret };
        }

        static IEnumerable<Registers> op_STX(Registers ret, operand_t operand, instruction_t instr)
        {
            operand.SetValue(ret, ret.XS, mem_access_t.MEM_DATA, ret.X);
            return new[] { ret };
        }

        static IEnumerable<Registers> op_STY(Registers ret, operand_t operand, instruction_t instr)
        {
            operand.SetValue(ret, ret.XS, mem_access_t.MEM_DATA, ret.Y);
            return new[] { ret };
        }

        static IEnumerable<Registers> op_STZ(Registers ret, operand_t operand, instruction_t instr)
        {

            operand.SetValue(ret, ret.MS, mem_access_t.MEM_DATA, 0);
            return new[] { ret };
        }


        static IEnumerable<Registers> op_TSB(Registers ret, operand_t operand, instruction_t instr)
        {
            var val = operand.GetValue(ret, ret.MS, mem_access_t.MEM_DATA);            
            int acc = ret.get_accumulator();
            int tmp;
            if (acc >= 0)
            {
                ret.Z = FromBool((acc & val) == 0);
                tmp = val | acc;
            }
            else
            {
                ret.Z = Tristate.Unknown;
                tmp = -1;
            }
            operand.SetValue(ret, ret.MS, mem_access_t.MEM_DATA, tmp);
            return new[] { ret };
        }

        static IEnumerable<Registers> op_TRB(Registers ret, operand_t operand, instruction_t instr)
        {
            var val = operand.GetValue(ret, ret.MS, mem_access_t.MEM_DATA);
            int acc = ret.get_accumulator();
            int tmp;
            if (acc >= 0)
            {
                ret.Z = FromBool((acc & val) == 0);
                tmp = val & ~acc;
            }
            else
            {
                ret.Z = Tristate.Unknown;
                tmp = -1;
            }
            operand.SetValue(ret, ret.MS, mem_access_t.MEM_DATA, tmp);
            return new[] { ret };
        }

        // This is used to implement: TAX, TAY, TSX
        static int transfer_88_16(Registers ret, int srchi, int srclo)
        {
            int dst;
            if (srclo >= 0 && srchi >= 0 && ret.XS == Tristate.False)
            {
                // 16-bit
                dst = (srchi << 8) + srclo;
                ret.set_NZ16(dst);
            }
            else if (srclo >= 0 && ret.XS == Tristate.True)
            {
                // 8-bit
                dst = srclo;
                ret.set_NZ8(dst);
            }
            else
            {
                dst = -1;
                ret.set_NZ_unknown();
            }
            return dst;
        }

        // This is used to implement: TXret.A, TYret.A
        static (int, int) transfer_16_88(Registers ret, int src, int defhi)
        {
            int dsthi = defhi, dstlo;
            if (ret.MS == Tristate.False)
            {
                // 16-bit
                if (src >= 0)
                {
                    dsthi = (src >> 8) & 0xff;
                    dstlo = src & 0xff;
                    ret.set_NZ16(src);
                }
                else
                {
                    dsthi = -1;
                    dstlo = -1;
                    ret.set_NZ_unknown();
                }
            }
            if (ret.MS == Tristate.True)
            {
                // 8-bit
                if (src >= 0)
                {
                    dstlo = src & 0xff;
                    ret.set_NZ8(src);
                }
                else
                {
                    dstlo = -1;
                    ret.set_NZ_unknown();
                }
            }
            else
            {
                // ret.MS undefined
                if (src >= 0)
                {
                    dstlo = src & 0xff;
                }
                else
                {
                    dstlo = -1;
                }
                dsthi = -1;
                ret.set_NZ_unknown();
            }

            return (dsthi, dstlo);
        }

        static IEnumerable<Registers> op_TAX(Registers ret, operand_t operand, instruction_t instr)
        {

            ret.X = transfer_88_16(ret, ret.B, ret.A);
            return new [] { ret };
        }

        static IEnumerable<Registers> op_TAY(Registers ret, operand_t operand, instruction_t instr)
        {
            
            ret.Y = transfer_88_16(ret, ret.B, ret.A);
            return new [] { ret };
        }

        static IEnumerable<Registers> op_TSX(Registers ret, operand_t operand, instruction_t instr)
        {
            
            ret.X = transfer_88_16(ret, ret.SH, ret.SL);
            return new [] { ret };
        }

        static IEnumerable<Registers> op_TXA(Registers ret, operand_t operand, instruction_t instr)
        {
            
            (ret.B, ret.A) = transfer_16_88(ret, ret.X, ret.B);
            return new [] { ret };
        }

        static IEnumerable<Registers> op_TXS(Registers ret, operand_t operand, instruction_t instr)
        {
            
            if (ret.X >= 0)
            {
                ret.SH = (ret.X >> 8) & 0xff;
                ret.SL = (ret.X) & 0xff;
            }
            else
            {
                ret.SH = -1;
                ret.SL = -1;
            }
            // Force ret.SH to be 01 in emulation mode
            if (ret.E == Tristate.True)
            {
                ret.SH = 0x01;
            }
            return new [] { ret };
        }

        static IEnumerable<Registers> op_TYA(Registers ret, operand_t operand, instruction_t instr)
        {
            
            (ret.B, ret.A) = transfer_16_88(ret, ret.Y, ret.B);
            return new [] { ret };
        }

        static IEnumerable<Registers> op_BRK(Registers ret, operand_t operand, instruction_t instr)
        {
            
            if (ret.E == Tristate.Unknown)
                ret.PC = -1;
            else if (ret.E == Tristate.True)
            {
                ret.push16(ret.PC);
                ret.push8(ret.get_FLAGS() | 0x30);
                ret.PC = ret.memory_read16(0xFFFE, mem_access_t.MEM_INSTR);
            }
            else
            {
                ret.push8(ret.PB);
                ret.push16(ret.PC);
                ret.push8(ret.get_FLAGS() | 0x30);
                ret.PB = 0;
                ret.PC = ret.memory_read16(0xFFEE, mem_access_t.MEM_INSTR);
            }
            ret.I = Tristate.True;
            ret.D = Tristate.False;
            return new [] { ret };
        }

        static IEnumerable<Registers> op_COP(Registers ret, operand_t operand, instruction_t instr)
        {
            
            ret.I = Tristate.True;
            ret.D = Tristate.True;
            if (ret.E == Tristate.Unknown)
                ret.PC = -1;
            else if (ret.E == Tristate.True)
            {
                ret.push16(ret.PC);
                ret.push8(ret.get_FLAGS() | 0x30);
                ret.PC = ret.memory_read16(0xFFFE, mem_access_t.MEM_INSTR);
            }
            else
            {
                ret.push8(ret.PB);
                ret.push16(ret.PC);
                ret.push8(ret.get_FLAGS() | 0x30);
                ret.PB = 0;
                ret.PC = ret.memory_read16(0xFFEE, mem_access_t.MEM_INSTR);
            }
            ret.I = Tristate.True;
            ret.D = Tristate.False;
            return new [] { ret };
        }

        static IEnumerable<Registers> op_WDM(Registers ret, operand_t operand, instruction_t instr)
        {
            return new[] { ret };
        }

        static IEnumerable<Registers> op_NOP(Registers ret, operand_t operand, instruction_t instr)
        {
            return new[] { ret };
        }

        static IEnumerable<Registers> op_JMP(Registers ret, operand_t operand, instruction_t instr)
        {
            if (operand.Ea < 0)
                ret.PC = -1;
            else 
                ret.PC = operand.Ea & 0xFFFF;

            return new[] { ret };
        }

        static IEnumerable<Registers> op_JML(Registers ret, operand_t operand, instruction_t instr)
        {
            if (operand.Ea < 0)
            {  ret.PC = -1;
                ret.PB  = -1;
            } else {
                ret.PB = (operand.Ea >> 16) & 0xFF;
                ret.PC = (operand.Ea & 0xFFFF);
            }

            return new[] { ret };
        }

        static IEnumerable<Registers> op_STP(Registers ret, operand_t operand, instruction_t instr)
        {
            ret.PC = instr.pc;
            return new[] { ret };
        }

        static IEnumerable<Registers> op_WAI(Registers ret, operand_t operand, instruction_t instr)
        {
            ret.PC = instr.pc;
            return new[] { ret };
        }


        // ====================================================================
        // Opcode Tables
        // ====================================================================


        static InstrType[] instr_table = {
   /* 00 */   new InstrType ( "BRK",    AddrMode.IMM8  , 7, 0, OpType.OTHER,    op_BRK),
   /* 01 */   new InstrType ( "ORA",    AddrMode.INDX  , 6, 0, OpType.READOP,   op_ORA),
   /* 02 */   new InstrType ( "COP",    AddrMode.IMM8  , 7, 1, OpType.OTHER,    op_COP),
   /* 03 */   new InstrType ( "ORA",    AddrMode.SR    , 4, 1, OpType.READOP,   op_ORA),
   /* 04 */   new InstrType ( "TSB",    AddrMode.ZP    , 5, 0, OpType.RMWOP,    op_TSB),
   /* 05 */   new InstrType ( "ORA",    AddrMode.ZP    , 3, 0, OpType.READOP,   op_ORA),
   /* 06 */   new InstrType ( "ASL",    AddrMode.ZP    , 5, 0, OpType.RMWOP,    op_ASL),
   /* 07 */   new InstrType ( "ORA",    AddrMode.IDL   , 6, 1, OpType.READOP,   op_ORA),
   /* 08 */   new InstrType ( "PHP",    AddrMode.IMP   , 3, 0, OpType.OTHER,    op_PHP),
   /* 09 */   new InstrType ( "ORA",    AddrMode.IMMM  , 2, 0, OpType.OTHER,    op_ORA),
   /* 0A */   new InstrType ( "ASL",    AddrMode.IMPA  , 2, 0, OpType.OTHER,    op_ASLA),
   /* 0B */   new InstrType ( "PHD",    AddrMode.IMP   , 4, 1, OpType.OTHER,    op_PHD),
   /* 0C */   new InstrType ( "TSB",    AddrMode.ABS   , 6, 0, OpType.RMWOP,    op_TSB),
   /* 0D */   new InstrType ( "ORA",    AddrMode.ABS   , 4, 0, OpType.READOP,   op_ORA),
   /* 0E */   new InstrType ( "ASL",    AddrMode.ABS   , 6, 0, OpType.RMWOP,    op_ASL),
   /* 0F */   new InstrType ( "ORA",    AddrMode.ABL   , 5, 1, OpType.READOP,   op_ORA),
   /* 10 */   new InstrType ( "BPL",    AddrMode.BRA   , 2, 0, OpType.BRANCHOP, op_BPL),
   /* 11 */   new InstrType ( "ORA",    AddrMode.INDY  , 5, 0, OpType.READOP,   op_ORA),
   /* 12 */   new InstrType ( "ORA",    AddrMode.IND   , 5, 0, OpType.READOP,   op_ORA),
   /* 13 */   new InstrType ( "ORA",    AddrMode.ISY   , 7, 1, OpType.READOP,   op_ORA),
   /* 14 */   new InstrType ( "TRB",    AddrMode.ZP    , 5, 0, OpType.RMWOP,    op_TRB),
   /* 15 */   new InstrType ( "ORA",    AddrMode.ZPX   , 4, 0, OpType.READOP,   op_ORA),
   /* 16 */   new InstrType ( "ASL",    AddrMode.ZPX   , 6, 0, OpType.RMWOP,    op_ASL),
   /* 17 */   new InstrType ( "ORA",    AddrMode.IDLY  , 6, 1, OpType.READOP,   op_ORA),
   /* 18 */   new InstrType ( "CLC",    AddrMode.IMP   , 2, 0, OpType.OTHER,    op_CLC),
   /* 19 */   new InstrType ( "ORA",    AddrMode.ABSY  , 4, 0, OpType.READOP,   op_ORA),
   /* 1A */   new InstrType ( "INC",    AddrMode.IMPA  , 2, 0, OpType.OTHER,    op_INCA),
   /* 1B */   new InstrType ( "TCS",    AddrMode.IMP   , 2, 1, OpType.OTHER,    op_TCS),
   /* 1C */   new InstrType ( "TRB",    AddrMode.ABS   , 6, 0, OpType.RMWOP,    op_TRB),
    /* 1D */   new InstrType( "ORA",    AddrMode.ABSX  , 4, 0, OpType.READOP,   op_ORA),
   /* 1E */   new InstrType( "ASL", AddrMode.ABSX  , 7, 0, OpType.RMWOP,    op_ASL),
   /* 1F */   new InstrType( "ORA", AddrMode.ALX   , 5, 1, OpType.READOP,   op_ORA),
   /* 20 */   new InstrType( "JSR", AddrMode.ABS   , 6, 0, OpType.OTHER,    op_JSR),
   /* 21 */   new InstrType( "AND", AddrMode.INDX  , 6, 0, OpType.READOP,   op_AND),
   /* 22 */   new InstrType( "JSL", AddrMode.ABL   , 8, 1, OpType.OTHER,    op_JSL),
   /* 23 */   new InstrType( "AND", AddrMode.SR    , 4, 1, OpType.READOP,   op_AND),
   /* 24 */   new InstrType( "BIT", AddrMode.ZP    , 3, 0, OpType.READOP,   op_BIT),
   /* 25 */   new InstrType( "AND", AddrMode.ZP    , 3, 0, OpType.READOP,   op_AND),
   /* 26 */   new InstrType( "ROL", AddrMode.ZP    , 5, 0, OpType.RMWOP,    op_ROL),
   /* 27 */   new InstrType( "AND", AddrMode.IDL   , 6, 1, OpType.READOP,   op_AND),
   /* 28 */   new InstrType( "PLP", AddrMode.IMP   , 4, 0, OpType.OTHER,    op_PLP),
   /* 29 */   new InstrType( "AND", AddrMode.IMMM  , 2, 0, OpType.OTHER,    op_AND),
   /* 2A */   new InstrType( "ROL", AddrMode.IMPA  , 2, 0, OpType.OTHER,    op_ROLA),
   /* 2B */   new InstrType( "PLD", AddrMode.IMP   , 5, 1, OpType.OTHER,    op_PLD),
   /* 2C */   new InstrType( "BIT", AddrMode.ABS   , 4, 0, OpType.READOP,   op_BIT),
   /* 2D */   new InstrType( "AND", AddrMode.ABS   , 4, 0, OpType.READOP,   op_AND),
   /* 2E */   new InstrType( "ROL", AddrMode.ABS   , 6, 0, OpType.RMWOP,    op_ROL),
   /* 2F */   new InstrType( "AND", AddrMode.ABL   , 5, 1, OpType.READOP,   op_AND),
   /* 30 */   new InstrType( "BMI", AddrMode.BRA   , 2, 0, OpType.BRANCHOP, op_BMI),
   /* 31 */   new InstrType( "AND", AddrMode.INDY  , 5, 0, OpType.READOP,   op_AND),
   /* 32 */   new InstrType( "AND", AddrMode.IND   , 5, 0, OpType.READOP,   op_AND),
   /* 33 */   new InstrType( "AND", AddrMode.ISY   , 7, 1, OpType.READOP,   op_AND),
   /* 34 */   new InstrType( "BIT", AddrMode.ZPX   , 4, 0, OpType.READOP,   op_BIT),
   /* 35 */   new InstrType( "AND", AddrMode.ZPX   , 4, 0, OpType.READOP,   op_AND),
   /* 36 */   new InstrType( "ROL", AddrMode.ZPX   , 6, 0, OpType.RMWOP,    op_ROL),
   /* 37 */   new InstrType( "AND", AddrMode.IDLY  , 6, 1, OpType.READOP,   op_AND),
   /* 38 */   new InstrType( "SEC", AddrMode.IMP   , 2, 0, OpType.OTHER,    op_SEC),
   /* 39 */   new InstrType( "AND", AddrMode.ABSY  , 4, 0, OpType.READOP,   op_AND),
   /* 3A */   new InstrType( "DEC", AddrMode.IMPA  , 2, 0, OpType.OTHER,    op_DECA),
   /* 3B */   new InstrType( "TSC", AddrMode.IMP   , 2, 1, OpType.OTHER,    op_TSC),
   /* 3C */   new InstrType( "BIT", AddrMode.ABSX  , 4, 0, OpType.READOP,   op_BIT),
    /* 3D */  new InstrType( "AND", AddrMode.ABSX  , 4, 0, OpType.READOP,   op_AND),
   /* 3E */   new InstrType( "ROL", AddrMode.ABSX  , 7, 0, OpType.RMWOP,    op_ROL),
   /* 3F */   new InstrType( "AND", AddrMode.ALX   , 5, 1, OpType.READOP,   op_AND),
   /* 40 */   new InstrType( "RTI", AddrMode.IMP   , 6, 0, OpType.OTHER,    op_RTI),
   /* 41 */   new InstrType( "EOR", AddrMode.INDX  , 6, 0, OpType.READOP,   op_EOR),
   /* 42 */   new InstrType( "WDM", AddrMode.IMM8  , 2, 1, OpType.OTHER,    op_WDM),
   /* 43 */   new InstrType( "EOR", AddrMode.SR    , 4, 1, OpType.READOP,   op_EOR),
   /* 44 */   new InstrType( "MVP", AddrMode.BM    , 7, 1, OpType.OTHER,    op_MVP),
   /* 45 */   new InstrType( "EOR", AddrMode.ZP    , 3, 0, OpType.READOP,   op_EOR),
   /* 46 */   new InstrType( "LSR", AddrMode.ZP    , 5, 0, OpType.RMWOP,    op_LSR),
   /* 47 */   new InstrType( "EOR", AddrMode.IDL   , 6, 1, OpType.READOP,   op_EOR),
   /* 48 */   new InstrType( "PHA", AddrMode.IMP   , 3, 0, OpType.OTHER,    op_PHA),
   /* 49 */   new InstrType( "EOR", AddrMode.IMMM  , 2, 0, OpType.OTHER,    op_EOR),
   /* 4A */   new InstrType( "LSR", AddrMode.IMPA  , 2, 0, OpType.OTHER,    op_LSRA),
   /* 4B */   new InstrType( "PHK", AddrMode.IMP   , 3, 1, OpType.OTHER,    op_PHK),
   /* 4C */   new InstrType( "JMP", AddrMode.ABS   , 3, 0, OpType.OTHER,    op_JMP),
   /* 4D */   new InstrType( "EOR", AddrMode.ABS   , 4, 0, OpType.READOP,   op_EOR),
   /* 4E */   new InstrType( "LSR", AddrMode.ABS   , 6, 0, OpType.RMWOP,    op_LSR),
   /* 4F */   new InstrType( "EOR", AddrMode.ABL   , 5, 1, OpType.READOP,   op_EOR),
   /* 50 */   new InstrType( "BVC", AddrMode.BRA   , 2, 0, OpType.BRANCHOP, op_BVC),
   /* 51 */   new InstrType( "EOR", AddrMode.INDY  , 5, 0, OpType.READOP,   op_EOR),
   /* 52 */   new InstrType( "EOR",     AddrMode.IND   , 5, 0, OpType.READOP,   op_EOR),
   /* 53 */   new InstrType( "EOR",     AddrMode.ISY   , 7, 1, OpType.READOP,   op_EOR),
   /* 54 */   new InstrType( "MVN",     AddrMode.BM    , 7, 1, OpType.OTHER,    op_MVN),
   /* 55 */   new InstrType( "EOR",     AddrMode.ZPX   , 4, 0, OpType.READOP,   op_EOR),
   /* 56 */   new InstrType( "LSR",     AddrMode.ZPX   , 6, 0, OpType.RMWOP,    op_LSR),
   /* 57 */   new InstrType(  "EOR",    AddrMode.IDLY  , 6, 1, OpType.READOP,   op_EOR),
   /* 58 */   new InstrType(  "CLI",    AddrMode.IMP   , 2, 0, OpType.OTHER,    op_CLI),
   /* 59 */   new InstrType(  "EOR",    AddrMode.ABSY  , 4, 0, OpType.READOP,   op_EOR),
   /* 5A */   new InstrType(  "PHY",    AddrMode.IMP   , 3, 0, OpType.OTHER,    op_PHY),
   /* 5B */   new InstrType(  "TCD",    AddrMode.IMP   , 2, 1, OpType.OTHER,    op_TCD),
   /* 5C */   new InstrType(  "JML",    AddrMode.ABL   , 4, 1, OpType.OTHER,    op_JML),
    /* 5D */   new InstrType(  "EOR",   AddrMode.ABSX  , 4, 0, OpType.READOP,   op_EOR),
   /* 5E */   new InstrType(  "LSR",    AddrMode.ABSX  , 7, 0, OpType.RMWOP,    op_LSR),
   /* 5F */   new InstrType(  "EOR",    AddrMode.ALX   , 5, 1, OpType.READOP,   op_EOR),
   /* 60 */   new InstrType(  "RTS",    AddrMode.IMP   , 6, 0, OpType.OTHER,    op_RTS),
   /* 61 */   new InstrType(  "ADC",    AddrMode.INDX  , 6, 0, OpType.READOP,   op_ADC),
   /* 62 */   new InstrType(  "PER",    AddrMode.BRL   , 6, 1, OpType.OTHER,    op_PER),
   /* 63 */   new InstrType(  "ADC",    AddrMode.SR    , 4, 1, OpType.READOP,   op_ADC),
   /* 64 */   new InstrType(  "STZ",    AddrMode.ZP    , 3, 0, OpType.WRITEOP,  op_STZ),
   /* 65 */   new InstrType(  "ADC",    AddrMode.ZP    , 3, 0, OpType.READOP,   op_ADC),
   /* 66 */   new InstrType(  "ROR",    AddrMode.ZP    , 5, 0, OpType.RMWOP,    op_ROR),
   /* 67 */   new InstrType(  "ADC",    AddrMode.IDL   , 6, 1, OpType.READOP,   op_ADC),
   /* 68 */   new InstrType(  "PLA",    AddrMode.IMP   , 4, 0, OpType.OTHER,    op_PLA),
   /* 69 */   new InstrType(  "ADC",    AddrMode.IMMM  , 2, 0, OpType.OTHER,    op_ADC),
   /* 6A */   new InstrType(  "ROR",    AddrMode.IMPA  , 2, 0, OpType.OTHER,    op_RORA),
   /* 6B */   new InstrType(  "RTL",    AddrMode.IMP   , 6, 1, OpType.OTHER,    op_RTL),
   /* 6C */   new InstrType(  "JMP",    AddrMode.IND16 , 5, 0, OpType.OTHER,    op_JMP),
   /* 6D */   new InstrType(  "ADC",    AddrMode.ABS   , 4, 0, OpType.READOP,   op_ADC),
   /* 6E */   new InstrType(  "ROR",    AddrMode.ABS   , 6, 0, OpType.RMWOP,    op_ROR),
   /* 6F */   new InstrType(  "ADC",    AddrMode.ABL   , 5, 1, OpType.READOP,   op_ADC),
   /* 70 */   new InstrType(  "BVS",    AddrMode.BRA   , 2, 0, OpType.BRANCHOP, op_BVS),
   /* 71 */   new InstrType(  "ADC",    AddrMode.INDY  , 5, 0, OpType.READOP,   op_ADC),
   /* 72 */   new InstrType(  "ADC",    AddrMode.IND   , 5, 0, OpType.READOP,   op_ADC),
   /* 73 */   new InstrType(  "ADC",    AddrMode.ISY   , 7, 1, OpType.READOP,   op_ADC),
   /* 74 */   new InstrType(  "STZ",    AddrMode.ZPX   , 4, 0, OpType.WRITEOP,  op_STZ),
   /* 75 */   new InstrType(  "ADC",    AddrMode.ZPX   , 4, 0, OpType.READOP,   op_ADC),
   /* 76 */   new InstrType(  "ROR",    AddrMode.ZPX   , 6, 0, OpType.RMWOP,    op_ROR),
   /* 77 */   new InstrType(  "ADC",    AddrMode.IDLY  , 6, 1, OpType.READOP,   op_ADC),
   /* 78 */   new InstrType(  "SEI",    AddrMode.IMP   , 2, 0, OpType.OTHER,    op_SEI),
   /* 79 */   new InstrType(  "ADC",    AddrMode.ABSY  , 4, 0, OpType.READOP,   op_ADC),
   /* 7A */   new InstrType(  "PLY",    AddrMode.IMP   , 4, 0, OpType.OTHER,    op_PLY),
   /* 7B */   new InstrType(  "TDC",    AddrMode.IMP   , 2, 1, OpType.OTHER,    op_TDC),
   /* 7C */   new InstrType(  "JMP",    AddrMode.IND1X , 6, 0, OpType.OTHER,    op_JMP),
    /* 7D */   new InstrType(  "ADC",   AddrMode.ABSX  , 4, 0, OpType.READOP,   op_ADC),
   /* 7E */   new InstrType(  "ROR",    AddrMode.ABSX  , 7, 0, OpType.RMWOP,    op_ROR),
   /* 7F */   new InstrType(  "ADC",    AddrMode.ALX   , 5, 1, OpType.READOP,   op_ADC),
   /* 80 */   new InstrType(  "BRA",    AddrMode.BRA   , 3, 0, OpType.OTHER,    op_BRA),
   /* 81 */   new InstrType(  "STA",    AddrMode.INDX  , 6, 0, OpType.WRITEOP,  op_STA),
   /* 82 */   new InstrType(  "BRL",    AddrMode.BRL   , 4, 1, OpType.OTHER,    op_BRL),
   /* 83 */   new InstrType(  "STA",    AddrMode.SR    , 4, 1, OpType.WRITEOP,  op_STA),
   /* 84 */   new InstrType(  "STY",    AddrMode.ZP    , 3, 0, OpType.WRITEOP,  op_STY),
   /* 85 */   new InstrType(  "STA",    AddrMode.ZP    , 3, 0, OpType.WRITEOP,  op_STA),
   /* 86 */   new InstrType(  "STX",    AddrMode.ZP    , 3, 0, OpType.WRITEOP,  op_STX),
   /* 87 */   new InstrType(  "STA" ,   AddrMode.IDL   , 6, 1, OpType.WRITEOP,  op_STA),
   /* 88 */   new InstrType(  "DEY",    AddrMode.IMP   , 2, 0, OpType.OTHER,    op_DEY),
   /* 89 */   new InstrType(  "BIT",    AddrMode.IMMM  , 2, 0, OpType.OTHER,    op_BIT_IMM),
   /* 8A */   new InstrType(  "TXA", AddrMode.IMP   , 2, 0, OpType.OTHER, op_TXA),
   /* 8B */   new InstrType(  "PHB", AddrMode.IMP   , 3, 1, OpType.OTHER, op_PHB),
   /* 8C */   new InstrType(  "STY", AddrMode.ABS   , 4, 0, OpType.WRITEOP, op_STY),
   /* 8D */   new InstrType(  "STA", AddrMode.ABS   , 4, 0, OpType.WRITEOP, op_STA),
   /* 8E */   new InstrType(  "STX", AddrMode.ABS   , 4, 0, OpType.WRITEOP, op_STX),
   /* 8F */   new InstrType(  "STA", AddrMode.ABL   , 5, 1, OpType.WRITEOP, op_STA),
   /* 90 */   new InstrType(  "BCC", AddrMode.BRA   , 2, 0, OpType.BRANCHOP, op_BCC),
   /* 91 */   new InstrType(  "STA", AddrMode.INDY  , 6, 0, OpType.WRITEOP, op_STA),
   /* 92 */   new InstrType(  "STA", AddrMode.IND   , 5, 0, OpType.WRITEOP, op_STA),
   /* 93 */   new InstrType(  "STA", AddrMode.ISY   , 7, 1, OpType.WRITEOP, op_STA),
   /* 94 */   new InstrType(  "STY", AddrMode.ZPX   , 4, 0, OpType.WRITEOP, op_STY),
   /* 95 */   new InstrType(  "STA", AddrMode.ZPX   , 4, 0, OpType.WRITEOP, op_STA),
   /* 96 */   new InstrType(  "STX", AddrMode.ZPY   , 4, 0, OpType.WRITEOP, op_STX),
   /* 97 */   new InstrType(  "STA", AddrMode.IDLY  , 6, 1, OpType.WRITEOP, op_STA),
   /* 98 */   new InstrType(  "TYA", AddrMode.IMP   , 2, 0, OpType.OTHER, op_TYA),
   /* 99 */   new InstrType(  "STA", AddrMode.ABSY  , 5, 0, OpType.WRITEOP, op_STA),
   /* 9A */   new InstrType(  "TXS", AddrMode.IMP   , 2, 0, OpType.OTHER, op_TXS),
   /* 9B */   new InstrType(  "TXY", AddrMode.IMP   , 2, 1, OpType.OTHER, op_TXY),
   /* 9C */   new InstrType(  "STZ", AddrMode.ABS   , 4, 0, OpType.WRITEOP, op_STZ),
    /* 9D */   new InstrType(  "STA", AddrMode.ABSX  , 5, 0, OpType.WRITEOP, op_STA),
   /* 9E */   new InstrType(  "STZ", AddrMode.ABSX  , 5, 0, OpType.WRITEOP, op_STZ),
   /* 9F */   new InstrType(  "STA", AddrMode.ALX   , 5, 1, OpType.WRITEOP, op_STA),
   /* A0 */   new InstrType(  "LDY", AddrMode.IMMX  , 2, 0, OpType.OTHER, op_LDY),
   /* A1 */   new InstrType(  "LDA", AddrMode.INDX  , 6, 0, OpType.READOP, op_LDA),
   /* A2 */   new InstrType(  "LDX", AddrMode.IMMX  , 2, 0, OpType.OTHER, op_LDX),
   /* A3 */   new InstrType(  "LDA", AddrMode.SR    , 4, 1, OpType.READOP, op_LDA),
   /* A4 */   new InstrType(  "LDY", AddrMode.ZP    , 3, 0, OpType.READOP, op_LDY),
   /* A5 */   new InstrType(  "LDA", AddrMode.ZP    , 3, 0, OpType.READOP, op_LDA),
   /* A6 */   new InstrType(  "LDX", AddrMode.ZP    , 3, 0, OpType.READOP, op_LDX),
   /* A7 */   new InstrType(  "LDA", AddrMode.IDL   , 6, 1, OpType.READOP, op_LDA),
   /* A8 */   new InstrType(  "TAY", AddrMode.IMP   , 2, 0, OpType.OTHER, op_TAY),
   /* A9 */   new InstrType(  "LDA", AddrMode.IMMM  , 2, 0, OpType.OTHER, op_LDA),
   /* AA */   new InstrType(  "TAX", AddrMode.IMP   , 2, 0, OpType.OTHER, op_TAX),
   /* AB */   new InstrType(  "PLB", AddrMode.IMP   , 4, 1, OpType.OTHER, op_PLB),
   /* AC */   new InstrType(  "LDY", AddrMode.ABS   , 4, 0, OpType.READOP, op_LDY),
   /* AD */   new InstrType(  "LDA", AddrMode.ABS   , 4, 0, OpType.READOP, op_LDA),
   /* AE */   new InstrType(  "LDX", AddrMode.ABS   , 4, 0, OpType.READOP, op_LDX),
   /* AF */   new InstrType(  "LDA", AddrMode.ABL   , 5, 1, OpType.READOP, op_LDA),
   /* B0 */   new InstrType(  "BCS", AddrMode.BRA   , 2, 0, OpType.BRANCHOP, op_BCS),
   /* B1 */   new InstrType(  "LDA", AddrMode.INDY  , 5, 0, OpType.READOP, op_LDA),
   /* B2 */   new InstrType(  "LDA", AddrMode.IND   , 5, 0, OpType.READOP, op_LDA),
   /* B3 */   new InstrType(  "LDA", AddrMode.ISY   , 7, 1, OpType.READOP, op_LDA),
   /* B4 */   new InstrType(  "LDY", AddrMode.ZPX   , 4, 0, OpType.READOP, op_LDY),
   /* B5 */   new InstrType(  "LDA", AddrMode.ZPX   , 4, 0, OpType.READOP, op_LDA),
   /* B6 */   new InstrType(  "LDX", AddrMode.ZPY   , 4, 0, OpType.READOP, op_LDX),
   /* B7 */   new InstrType(  "LDA", AddrMode.IDLY  , 6, 1, OpType.READOP, op_LDA),
   /* B8 */   new InstrType(  "CLV", AddrMode.IMP   , 2, 0, OpType.OTHER, op_CLV),
   /* B9 */   new InstrType(  "LDA", AddrMode.ABSY  , 4, 0, OpType.READOP, op_LDA),
   /* BA */   new InstrType(  "TSX", AddrMode.IMP   , 2, 0, OpType.OTHER, op_TSX),
   /* BB */   new InstrType(  "TYX", AddrMode.IMP   , 2, 1, OpType.OTHER, op_TYX),
   /* BC */   new InstrType(  "LDY", AddrMode.ABSX  , 4, 0, OpType.READOP, op_LDY),
    /* BD */   new InstrType(  "LDA", AddrMode.ABSX  , 4, 0, OpType.READOP, op_LDA),
   /* BE */   new InstrType(  "LDX", AddrMode.ABSY  , 4, 0, OpType.READOP, op_LDX),
   /* BF */   new InstrType(  "LDA", AddrMode.ALX   , 5, 1, OpType.READOP, op_LDA),
   /* C0 */   new InstrType(  "CPY", AddrMode.IMMX  , 2, 0, OpType.OTHER, op_CPY),
   /* C1 */   new InstrType(  "CMP", AddrMode.INDX  , 6, 0, OpType.READOP, op_CMP),
   /* C2 */   new InstrType(  "REP", AddrMode.IMM8  , 3, 1, OpType.OTHER, op_REP),
   /* C3 */   new InstrType(  "CMP", AddrMode.SR    , 4, 1, OpType.READOP, op_CMP),
   /* C4 */   new InstrType(  "CPY", AddrMode.ZP    , 3, 0, OpType.READOP, op_CPY),
   /* C5 */   new InstrType(  "CMP", AddrMode.ZP    , 3, 0, OpType.READOP, op_CMP),
   /* C6 */   new InstrType(  "DEC", AddrMode.ZP    , 5, 0, OpType.RMWOP, op_DEC),
   /* C7 */   new InstrType(  "CMP", AddrMode.IDL   , 6, 1, OpType.READOP, op_CMP),
   /* C8 */   new InstrType(  "INY", AddrMode.IMP   , 2, 0, OpType.OTHER, op_INY),
   /* C9 */   new InstrType(  "CMP", AddrMode.IMMM  , 2, 0, OpType.OTHER, op_CMP),
   /* CA */   new InstrType(  "DEX", AddrMode.IMP   , 2, 0, OpType.OTHER, op_DEX),
   /* CB */   new InstrType(  "WAI", AddrMode.IMP   , 1, 1, OpType.OTHER, op_WAI),        // WD65C02=3
   /* CC */   new InstrType(  "CPY", AddrMode.ABS   , 4, 0, OpType.READOP, op_CPY),
   /* CD */   new InstrType(  "CMP", AddrMode.ABS   , 4, 0, OpType.READOP, op_CMP),
   /* CE */   new InstrType(  "DEC", AddrMode.ABS   , 6, 0, OpType.RMWOP, op_DEC),
   /* CF */   new InstrType(  "CMP", AddrMode.ABL   , 5, 1, OpType.READOP, op_CMP),
   /* D0 */   new InstrType(  "BNE", AddrMode.BRA   , 2, 0, OpType.BRANCHOP, op_BNE),
   /* D1 */   new InstrType(  "CMP", AddrMode.INDY  , 5, 0, OpType.READOP, op_CMP),
   /* D2 */   new InstrType(  "CMP", AddrMode.IND   , 5, 0, OpType.READOP, op_CMP),
   /* D3 */   new InstrType(  "CMP", AddrMode.ISY   , 7, 1, OpType.READOP, op_CMP),
   /* D4 */   new InstrType(  "PEI", AddrMode.IND   , 6, 1, OpType.OTHER, op_PEI),
   /* D5 */   new InstrType(  "CMP", AddrMode.ZPX   , 4, 0, OpType.READOP, op_CMP),
   /* D6 */   new InstrType(  "DEC", AddrMode.ZPX   , 6, 0, OpType.RMWOP, op_DEC),
   /* D7 */   new InstrType(  "CMP", AddrMode.IDLY  , 6, 1, OpType.READOP, op_CMP),
   /* D8 */   new InstrType(  "CLD", AddrMode.IMP   , 2, 0, OpType.OTHER, op_CLD),
   /* D9 */   new InstrType(  "CMP", AddrMode.ABSY  , 4, 0, OpType.READOP, op_CMP),
   /* DA */   new InstrType(  "PHX", AddrMode.IMP   , 3, 0, OpType.OTHER, op_PHX),
   /* DB */   new InstrType(  "STP", AddrMode.IMP   , 1, 1, OpType.OTHER,    op_STP),        // WD65C02=3
   /* DC */   new InstrType(  "JML", AddrMode.IAL   , 6, 1, OpType.OTHER,    op_JML),
   /* DD */   new InstrType(  "CMP", AddrMode.ABSX  , 4, 0, OpType.READOP, op_CMP),
   /* DE */   new InstrType(  "DEC", AddrMode.ABSX  , 7, 0, OpType.RMWOP, op_DEC),
   /* DF */   new InstrType(  "CMP", AddrMode.ALX   , 5, 1, OpType.READOP, op_CMP),
   /* E0 */   new InstrType(  "CPX", AddrMode.IMMX  , 2, 0, OpType.OTHER, op_CPX),
   /* E1 */   new InstrType(  "SBC", AddrMode.INDX  , 6, 0, OpType.READOP, op_SBC),
   /* E2 */   new InstrType(  "SEP", AddrMode.IMM8  , 3, 1, OpType.OTHER, op_SEP),
   /* E3 */   new InstrType(  "SBC", AddrMode.SR    , 4, 1, OpType.READOP, op_SBC),
   /* E4 */   new InstrType(  "CPX", AddrMode.ZP    , 3, 0, OpType.READOP, op_CPX),
   /* E5 */   new InstrType(  "SBC", AddrMode.ZP    , 3, 0, OpType.READOP, op_SBC),
   /* E6 */   new InstrType(  "INC", AddrMode.ZP    , 5, 0, OpType.RMWOP, op_INC),
   /* E7 */   new InstrType(  "SBC", AddrMode.IDL   , 6, 1, OpType.READOP, op_SBC),
   /* E8 */   new InstrType(  "INX", AddrMode.IMP   , 2, 0, OpType.OTHER, op_INX),
   /* E9 */   new InstrType(  "SBC", AddrMode.IMMM  , 2, 0, OpType.OTHER, op_SBC),
   /* EA */   new InstrType(  "NOP", AddrMode.IMP   , 2, 0, OpType.OTHER,    op_NOP),
   /* EB */   new InstrType(  "XBA", AddrMode.IMP   , 3, 1, OpType.OTHER, op_XBA),
   /* EC */   new InstrType(  "CPX", AddrMode.ABS   , 4, 0, OpType.READOP, op_CPX),
   /* ED */   new InstrType(  "SBC", AddrMode.ABS   , 4, 0, OpType.READOP, op_SBC),
   /* EE */   new InstrType(  "INC", AddrMode.ABS   , 6, 0, OpType.RMWOP, op_INC),
   /* EF */   new InstrType(  "SBC", AddrMode.ABL   , 5, 1, OpType.READOP, op_SBC),
   /* F0 */   new InstrType(  "BEQ", AddrMode.BRA   , 2, 0, OpType.BRANCHOP, op_BEQ),
   /* F1 */   new InstrType(  "SBC", AddrMode.INDY  , 5, 0, OpType.READOP, op_SBC),
   /* F2 */   new InstrType(  "SBC", AddrMode.IND   , 5, 0, OpType.READOP, op_SBC),
   /* F3 */   new InstrType(  "SBC", AddrMode.ISY   , 7, 1, OpType.READOP, op_SBC),
   /* F4 */   new InstrType(  "PEA", AddrMode.ABS   , 5, 1, OpType.OTHER, op_PEA),
   /* F5 */   new InstrType(  "SBC", AddrMode.ZPX   , 4, 0, OpType.READOP, op_SBC),
   /* F6 */   new InstrType(  "INC", AddrMode.ZPX   , 6, 0, OpType.RMWOP, op_INC),
   /* F7 */   new InstrType(  "SBC", AddrMode.IDLY  , 6, 1, OpType.READOP, op_SBC),
   /* F8 */   new InstrType(  "SED", AddrMode.IMP   , 2, 0, OpType.OTHER, op_SED),
   /* F9 */   new InstrType(  "SBC", AddrMode.ABSY  , 4, 0, OpType.READOP, op_SBC),
   /* FA */   new InstrType(  "PLX", AddrMode.IMP   , 4, 0, OpType.OTHER, op_PLX),
   /* FB */   new InstrType(  "XCE", AddrMode.IMP   , 2, 1, OpType.OTHER, op_XCE),
   /* FC */   new InstrType(  "JSR", AddrMode.IND1X , 8, 1, OpType.OTHER, op_JSR),
    /* FD */  new InstrType(  "SBC", AddrMode.ABSX  , 4, 0, OpType.READOP, op_SBC),
   /* FE */   new InstrType(  "INC", AddrMode.ABSX  , 7, 0, OpType.RMWOP, op_INC),
   /* FF */   new InstrType(  "SBC", AddrMode.ALX   , 5, 1, OpType.READOP, op_SBC)

};
    }
}
