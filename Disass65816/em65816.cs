
//Adpated from https://github.com/hoglet67/6502Decoder/blob/master/src/em_65816.c

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection.Emit;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using operand_t = int;
using ea_t = int;
using System.Numerics;
using static Disass65816.em65816;
using Microsoft.VisualBasic;


namespace Disass65816
{
    public class em65816
    {
        readonly int DEPTH = 10;

        public enum Tristate
        {
            Unknown = -1,
            False = 0,
            True = 1
        }

        Tristate TriNot(Tristate s) { return s == Tristate.Unknown ? Tristate.Unknown : s == Tristate.True ? Tristate.False : Tristate.True; }


        // Sample_type_t is an abstraction of both the 6502 SYNC and the 65816 VDA/VPA

        public enum sample_type_t
        {                   // 6502 Sync    65815 VDA/VPA
            UNKNOWN,        //      ?             ?   ?
            INTERNAL,       //      -             0   0
            PROGRAM,        //      -             0   1
            DATA,           //      0             1   0
            OPCODE,         //      1             1   1
            LAST            // a marker for the end of stream
        }


        public struct sample_t
        {
            public UInt32 sample_count { get; init; }
            public sample_type_t type { get; init; }
            public byte data { get; init; }
            public Tristate rnw { get; init; }
            public Tristate rst { get; init; }
            public Tristate e { get; init; } // (65816 e pin)
        }

        public struct instruction_t
        {
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



        public delegate void memory_reader_fn(int data, int address, mem_access_t access_type);
        public delegate void memory_writer_fn(int data, int address, mem_access_t access_type);
        public delegate int memory_raw_reader_fn(int address);

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
            IMM
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

        private delegate int emulate_method(em65816 em, operand_t operand, ea_t ea);


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
            new AddrModeType {len = 2,    fmt = "%1$s #%2$02X"}              // AddrMode.IMM        };
        };

        public memory_reader_fn memory_read { get; init; }
        public memory_writer_fn memory_write { get; init; }

        public memory_raw_reader_fn memory_read_raw { get; init; }

        string fmt_imm16 = "%1$s #%3$02X%2$02X";

        bool failflag = false;

        // 6502 registers: -1 means unknown
        int A = -1;
        int X = -1;
        int Y = -1;

        int SH = -1;
        int SL = -1;

        int PC = -1;

        // 65C816 additional registers: -1 means unknown
        int B = -1; // Accumulator bits 15..8
        int DP = -1; // 16-bit Direct Page Register (default to zero, otherwise AddrMode.ZP addressing is broken)
        int DB = -1; // 8-bit Data Bank Register
        int PB = -1; // 8-bit Program Bank Register

        // 6502 flags: -1 means unknown
        Tristate N = Tristate.Unknown;
        Tristate V = Tristate.Unknown;
        Tristate D = Tristate.Unknown;
        Tristate I = Tristate.Unknown;
        Tristate Z = Tristate.Unknown;
        Tristate C = Tristate.Unknown;

        // 65C816 additional flags: -1 means unknown
        Tristate MS = Tristate.Unknown; // Accumulator and Memeory Size Flag
        Tristate XS = Tristate.Unknown; // Index Register Size Flag
        Tristate E = Tristate.Unknown; // Emulation Mode Flag, updated by XCE

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

        // ====================================================================
        // Helper Methods
        // ====================================================================

        bool compare_FLAGS(int operand)
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

        void check_FLAGS(int operand)
        {
            failflag |= compare_FLAGS(operand);
        }

        void set_FLAGS(int operand)
        {
            N = (Tristate)((operand >> 7) & 1);
            V = (Tristate)((operand >> 6) & 1);
            if (E == 0)
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

        void set_NZ_unknown()
        {
            N = Tristate.Unknown;
            Z = Tristate.Unknown;
        }

        void set_NZC_unknown()
        {
            N = Tristate.Unknown;
            Z = Tristate.Unknown;
            C = Tristate.Unknown;
        }

        void set_NVZC_unknown()
        {
            N = Tristate.Unknown;
            V = Tristate.Unknown;
            Z = Tristate.Unknown;
            C = Tristate.Unknown;
        }

        void set_NZ8(int value)
        {
            N = (Tristate)((value >> 7) & 1);
            Z = (Tristate)((value & 0xff) == 0 ? 1 : 0);
        }

        void set_NZ16(int value)
        {
            N = (Tristate)((value >> 15) & 1);
            Z = (Tristate)((value & 0xffff) == 0 ? 1 : 0);
        }

        void set_NZ_unknown_width(int value)
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
                Z = (value & 0xff) == 0 ? Tristate.True : Tristate.False;
            }
            else
            {
                // some high bits set, so Z must become undefined
                Z = Tristate.Unknown;
            }
        }

        void set_NZ_XS(int value)
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

        void set_NZ_MS(int value)
        {
            if (MS < 0)
            {
                set_NZ_unknown_width(value);
            }
            else if (MS == 0)
            {
                set_NZ16(value);
            }
            else
            {
                set_NZ8(value);
            }
        }

        void set_NZ_AB(int A, int B)
        {
            if (MS > 0)
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
            else if (MS == 0)
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

        // TODO: Stack wrapping im emulation mode should only happen with "old" instructions
        // e.g. PLB should not wrap
        // See appendix of 65C816 Opcodes by Bruce Clark

        void pop8(int value)
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
                memory_read(value & 0xff, (SH << 8) + SL, mem_access_t.MEM_STACK);
            }
        }

        // TODO: Stack wrapping im emulation mode should only happen with "old" instructions
        // e.g. PLB should not wrap
        // See appendix of 65C816 Opcodes by Bruce Clark

        void push8(int value)
        {
            // Handle the memory access
            if (SL >= 0 && SH >= 0)
            {
                memory_write(value & 0xff, (SH << 8) + SL, mem_access_t.MEM_STACK);
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

        void pop16(int value)
        {
            pop8(value);
            pop8(value >> 8);
        }

        void push16(int value)
        {
            push8(value >> 8);
            push8(value);
        }

        void popXS(int value)
        {
            if (XS < 0)
            {
                SL = -1;
                SH = -1;
            }
            else if (XS == 0)
            {
                pop16(value);
            }
            else
            {
                pop8(value);
            }
        }

        void popMS(int value)
        {
            if (MS < 0)
            {
                SL = -1;
                SH = -1;
            }
            else if (MS == 0)
            {
                pop16(value);
            }
            else
            {
                pop8(value);
            }
        }

        void pushXS(int value)
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

        void pushMS(int value)
        {
            if (MS < 0)
            {
                SL = -1;
                SH = -1;
            }
            else if (MS == 0)
            {
                push16(value);
            }
            else
            {
                push8(value);
            }
        }

        void interrupt(sample_t[] sample_q, int num_cycles, instruction_t instruction, int pc_offset)
        {
            int i;
            int pb;
            if (num_cycles == 7)
            {
                // We must be in emulation mode
                emulation_mode_on();
                i = 2;
                pb = PB;
            }
            else
            {
                // We must be in native mode
                emulation_mode_off();
                i = 3;
                pb = sample_q[2].data;
            }
            // Parse the bus cycles
            // E=0 <opcode> <op1> <write pbr> <write pch> <write pcl> <write p> <read rst> <read rsth>
            // E=1 <opcode> <op1>             <write pch> <write pcl> <write p> <read rst> <read rsth>
            int pc = (sample_q[i].data << 8) + sample_q[i + 1].data;
            int flags = sample_q[i + 2].data;
            int vector = (sample_q[i + 4].data << 8) + sample_q[i + 3].data;
            // Update the address of the interruted instruction
            if (pb >= 0)
            {
                instruction.pb = pb;
            }
            instruction.pc = (pc - pc_offset) & 0xffff;
            // Stack the PB/PC/FLags (for memory modelling)
            if (E == 0)
            {
                push8(pb);
            }
            push16(pc);
            push8(flags);
            // Validate the flags
            check_FLAGS(flags);
            // And make them consistent
            set_FLAGS(flags);
            // Setup expected state for the IAddrMode.SR
            I = Tristate.True;
            D = 0;
            PB = 0x00;
            PC = vector;
        }

        int get_8bit_cycles(sample_t[] sample_q)
        {
            int opcode = sample_q[0].data;
            int op1 = sample_q[1].data;
            int op2 = sample_q[2].data;
            InstrType instr = instr_table[opcode];
            int cycle_count = instr.cycles;

            // One cycle penalty if DP is not page aligned
            int dpextra = (instr.mode <= AddrMode.ZP && DP >= 0 && (DP & 0xff) != 0) ? 1 : 0;

            // Account for extra cycle in a page crossing in (indirect), Y (not stores)
            // <opcode> <op1> [ <dpextra> ] <addrlo> <addrhi> [ <page crossing>] <operand> [ <extra cycle in dec mode> ]
            if ((instr.mode == AddrMode.INDY) && (instr.optype != OpType.WRITEOP) && Y >= 0)
            {
                int bas = (sample_q[3 + dpextra].data << 8) + sample_q[2 + dpextra].data;
                if ((bas & 0x1ff00) != ((bas + Y) & 0x1ff00))
                {
                    cycle_count++;
                }
            }

            // Account for extra cycle in a page crossing in absolute indexed (not stores or rmw) in emulated mode
            if (((instr.mode == AddrMode.ABSX) || (instr.mode == AddrMode.ABSY)) && (instr.optype == OpType.READOP))
            {
                int index = (instr.mode == AddrMode.ABSX) ? X : Y;
                if (index >= 0)
                {
                    int bas = op1 + (op2 << 8);
                    if ((bas & 0x1ff00) != ((bas + index) & 0x1ff00))
                    {
                        cycle_count++;
                    }
                }
            }

            return cycle_count + dpextra;
        }

        int get_num_cycles(sample_t[] sample_q, bool intr_seen)
        {
            int opcode = sample_q[0].data;
            int op1 = sample_q[1].data;
            int op2 = sample_q[2].data;
            InstrType instr = instr_table[opcode];
            int cycle_count = instr.cycles;

            // Interrupt, BRK, COP
            if (intr_seen || opcode == 0x00 || opcode == 0x02)
            {
                return (E == 0) ? 8 : 7;
            }

            // E MS    Correction:
            // ?  ?    ?
            // ?  0    ?
            // 0  ?    ?
            // 0  0    1
            // ?  1    0
            // 0  1    0
            // 1  ?    0
            // 1  0    0
            // 1  1    0

            if (instr.m_extra > 0)
            {
                if (E == 0 && MS == 0)
                {
                    cycle_count += instr.m_extra;
                }
                else if (!(E > 0 || MS > 0))
                {
                    return -1;
                }
            }

            if (instr.x_extra > 0)
            {
                if (E == 0 && XS == 0)
                {
                    cycle_count += instr.x_extra;
                }
                else if (!(E > 0 || XS > 0))
                {
                    return -1;
                }
            }


            // One cycle penalty if DP is not page aligned
            int dpextra = (instr.mode <= AddrMode.ZP && DP >= 0 && (DP & 0xff) != 0) ? 1 : 0;

            // RTI takes one extra cycle in native mode
            if (opcode == 0x40)
            {
                if (E == 0)
                {
                    cycle_count++;
                }
                else if (E < 0)
                {
                    return -1;
                }
            }

            // Account for extra cycle in a page crossing in (indirect), Y (not stores)
            // <opcode> <op1> [ <dpextra> ] <addrlo> <addrhi> [ <page crossing>] <operand> [ <extra cycle in dec mode> ]
            if ((instr.mode == AddrMode.INDY) && (instr.optype != OpType.WRITEOP) && Y >= 0)
            {
                int bas = (sample_q[3 + dpextra].data << 8) + sample_q[2 + dpextra].data;
                // TODO: take account of page crossing with 16-bit Y
                if ((bas & 0x1ff00) != ((bas + Y) & 0x1ff00))
                {
                    cycle_count++;
                }
            }

            // Account for extra cycle in a page crossing in absolute indexed (not stores or rmw) in emulated mode
            if (((instr.mode == AddrMode.ABSX) || (instr.mode == AddrMode.ABSY)) && (instr.optype == OpType.READOP))
            {
                int correction = -1;
                int index = (instr.mode == AddrMode.ABSX) ? X : Y;
                if (index >= 0)
                {
                    int bas = op1 + (op2 << 8);
                    if ((bas & 0x1ff00) != ((bas + index) & 0x1ff00))
                    {
                        correction = 1;
                    }
                    else
                    {
                        correction = 0;
                    }
                }
                // E  C
                // 1  1    1
                // ?  ?    ?
                // ?  1    ?
                // 1  ?    ?
                // ?  0    0
                // 0  ?    0
                // 0  0    0
                // 0  1    0
                // 1  0    0
                if (E > 0 && correction > 0)
                {
                    cycle_count++;
                }
                else if (!(E == 0 || correction == 0))
                {
                    return -1;
                }
            }

            // Account for extra cycles in a branch
            if (((opcode & 0x1f) == 0x10) || (opcode == 0x80))
            {
                // Default to backards branches taken, forward not taken
                // int taken = ((int8_t)op1) < 0;
                Tristate taken = Tristate.False;
                switch (opcode)
                {
                    case 0x10: // BPL
                        taken = TriNot(N);
                        break;
                    case 0x30: // AddrMode.BMI
                        taken = N;
                        break;
                    case 0x50: // BVC
                        taken = TriNot(V);
                        break;
                    case 0x70: // BVS
                        taken = V;
                        break;
                    case 0x80: // AddrMode.BRA
                        taken = Tristate.True;
                        cycle_count--; // instr table contains 3 for cycle count
                        break;
                    case 0x90: // BCC
                        taken = TriNot(C);
                        break;
                    case 0xB0: // BCS
                        taken = C;
                        break;
                    case 0xD0: // BNE
                        taken = TriNot(Z);
                        break;
                    case 0xF0: // BEQ
                        taken = Z;
                        break;
                }
                if (taken  ==  Tristate.Unknown)
                    return -1;
                else if (taken == Tristate.True)
                {
                    // A taken branch is 3 cycles, not 2
                    cycle_count++;
                    // In emulation node, a taken branch that crosses a page boundary is 4 cycle
                    int page_cross = -1;
                    if (E > 0 && PC >= 0)
                    {
                        int target = (PC + 2) + ((sbyte)(op1));
                        if ((target & 0xFF00) != ((PC + 2) & 0xff00))
                        {
                            page_cross = 1;
                        }
                        else
                        {
                            page_cross = 0;
                        }
                    }
                    else if (E == Tristate.False)
                    {
                        page_cross = 0;
                    }
                    if (page_cross < 0)
                    {
                        return -1;
                    }
                    else
                    {
                        cycle_count += page_cross;
                    }
                }
            }

            return cycle_count + dpextra;
        }


        int count_cycles_without_sync(sample_t[] sample_q, bool intr_seen)
        {
            //printf("VPA/VDA must be connected in 65816 mode\n");
            //exit(1);
            int num_cycles = get_num_cycles(sample_q, intr_seen);
            if (num_cycles >= 0)
            {
                return num_cycles;
            }
        //    printf("cycle prediction unknown\n");
            return 1;
        }

        int count_cycles_with_sync(sample_t[] sample_q, bool intr_seen)
        {
            if (sample_q[0].type == sample_type_t.OPCODE)
            {
                for (int i = 1; i < DEPTH; i++)
                {
                    if (sample_q[i].type == sample_type_t.LAST)
                    {
                        return 0;
                    }
                    if (sample_q[i].type == sample_type_t.OPCODE)
                    {
                        // Validate the num_cycles passed in
                        int expected = get_num_cycles(sample_q, intr_seen);
                        if (expected >= 0)
                        {
/*                            if (i != expected)
                            {
                                printf("opcode %02x: cycle prediction fail: expected %d actual %d\n", sample_q[0].data, expected, i);
                            }*/
                        }
                        return i;
                    }
                }
            }
            return 1;
        }

        // A set of actions to take if emulation mode enabled
        void emulation_mode_on()
        {
            if (E == 0)
            {
                failflag = true;
            }
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
        void emulation_mode_off()
        {
            if (E == Tristate.True)
            {
                failflag = true;
            }
            E = 0;
        }

        void check_and_set_ms(Tristate val)
        {
            if (MS != Tristate.Unknown && MS != val)
            {
                failflag = true;
            }
            MS = val;
            // Evidence of MS = 0 implies E = 0
            if (MS == 0)
            {
                emulation_mode_off();
            }
        }

        void check_and_set_xs(Tristate val)
        {
            if (XS != Tristate.True && XS != val)
            {
                failflag = true;
            }
            XS = val;
            // Evidence of XS = 0 implies E = 0
            if (XS == 0)
            {
                emulation_mode_off();
            }
        }

        // Helper to return the variable size accumulator
        int get_accumulator()
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

        // ====================================================================
        // Public Methods
        // ====================================================================
        /*
        void em_65816_init(arguments_t* args)
        {
            switch (args.cpu_type)
            {
                case CPU_65C816:
                    instr_table = instr_table_65c816;
                    break;
                default:
                    printf("em_65816_init called with unsupported cpu_type (%d)\n", args.cpu_type);
                    exit(1);
            }
            if (args.e_flag >= 0)
            {
                E = args.e_flag & 1;
                if (E)
                {
                    emulation_mode_on();
                }
                else
                {
                    emulation_mode_off();
                }
            }
            if (args.sp_reg >= 0)
            {
                SL = args.sp_reg & 0xff;
                SH = (args.sp_reg >> 8) & 0xff;
            }
            if (args.pb_reg >= 0)
            {
                PB = args.pb_reg & 0xff;
            }
            if (args.db_reg >= 0)
            {
                DB = args.db_reg & 0xff;
            }
            if (args.dp_reg >= 0)
            {
                DP = args.dp_reg & 0xffff;
            }
            if (args.ms_flag >= 0)
            {
                MS = args.ms_flag & 1;
            }
            if (args.xs_flag >= 0)
            {
                XS = args.xs_flag & 1;
            }

        }*/

        bool em_65816_match_interrupt(sample_t[] sample_q, int num_samples)
        {
            // Check we have enough valid samples
            if (num_samples < 7)
            {
                return false;
            }
            // Check the cycle has the right structure
            for (int i = 1; i < 7; i++)
            {
                if (sample_q[i].type == sample_type_t.OPCODE)
                {
                    return false;
                }
            }
            // In emulation mode an interupt will write PCH, PCL, PSW in bus cycles 2,3,4
            // In native mode an interupt will write PBR, PCH, PCL, PSW in bus cycles 2,3,4,5
            //
            // TODO: the heuristic only works in emulation mode
            if (sample_q[0].rnw >= 0)
            {
                // If we have the RNW pin connected, then just look for these three writes in succession
                // Currently can't detect a BRK or COP being interrupted
                if (sample_q[0].data == 0x00 || sample_q[0].data == 0x02)
                {
                    return false;
                }
                if (sample_q[2].rnw == 0 && sample_q[3].rnw == 0 && sample_q[4].rnw == 0)
                {
                    return true;
                }
            }
            else
            {
                // If not, then we use a heuristic, based on what we expect to see on the data
                // bus in cycles 2, 3 and 4, i.e. PCH, PCL, PSW
                if (sample_q[2].data == ((PC >> 8) & 0xff) && sample_q[3].data == (PC & 0xff))
                {
                    // Now test unused flag is 1, B is 0
                    if ((sample_q[4].data & 0x30) == 0x20)
                    {
                        // Finally test all other known flags match
                        if (!compare_FLAGS(sample_q[4].data))
                        {
                            // Matched PSW = NV-BDIZC
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        int em_65816_count_cycles(sample_t[] sample_q, bool intr_seen)
        {
            if (sample_q[0].type == sample_type_t.UNKNOWN)
            {
                return count_cycles_without_sync(sample_q, intr_seen);
            }
            else
            {
                return count_cycles_with_sync(sample_q, intr_seen);
            }
        }

        void em_65816_reset(sample_t[] sample_q, int num_cycles, instruction_t instruction)
        {
            instruction.pc = -1;
            A = -1;
            X = -1;
            Y = -1;
            SH = -1;
            SL = -1;
            N = Tristate.Unknown;
            V = Tristate.Unknown;
            D = Tristate.Unknown;
            Z = Tristate.Unknown;
            C = Tristate.Unknown;
            I = Tristate.True;
            D = Tristate.False;
            // Extra 816 regs
            B = -1;
            DP = 0;
            PB = 0;
            // Extra 816 flags
            E = Tristate.True;
            emulation_mode_on();
            // Program Counter
            PC = (sample_q[num_cycles - 1].data << 8) + sample_q[num_cycles - 2].data;
        }

        void em_65816_interrupt(sample_t[] sample_q, int num_cycles, instruction_t instruction)
        {
            interrupt(sample_q, num_cycles, instruction, 0);
        }

        void em_65816_emulate(sample_t[] sample_q, int num_cycles, instruction_t instruction)
        {

            // Unpack the instruction bytes
            byte opcode = sample_q[0].data;

            // Update the E flag if this e pin is being sampled
            Tristate new_E = sample_q[0].e;
            if (new_E != Tristate.Unknown && E != new_E)
            {
                if (E != Tristate.Unknown)
                {
//                    printf("correcting e flag\n");
                    failflag |= true;
                }
                E = new_E;
                if (E == Tristate.True)
                {
                    emulation_mode_on();
                }
                else
                {
                    emulation_mode_off();
                }
            }

            // lookup the entry for the instruction
            InstrType instr = instr_table[opcode];

            // Infer MS from instruction length
            if (MS == Tristate.Unknown && instr.m_extra != 0)
            {
                int cycles = get_8bit_cycles(sample_q);
                check_and_set_ms((num_cycles > cycles) ? Tristate.False : Tristate.True);
            }

            // Infer XS from instruction length
            if (XS == Tristate.Unknown && instr.x_extra != 0)
            {
                int cycles = get_8bit_cycles(sample_q);
                check_and_set_xs((num_cycles > cycles) ? Tristate.False : Tristate.True);
            }

            // Work out outcount, taking account of 8/16 bit immediates
            byte opcount = 0;
            if (instr.mode == AddrMode.IMM)
            {
                if ((instr.m_extra != 0 && MS == Tristate.False) || (instr.x_extra != 0 && XS == Tristate.False))
                {
                    opcount = 1;
                }
            }
            opcount += (byte)(instr.len - 1);

            byte op1 = (opcount < 1) ? (byte)0 : sample_q[1].data;

            // Special case JAddrMode.SR (AddrMode.IND16, X)
            byte op2 = (opcount < 2) ? (byte)0 : (opcode == 0xFC) ? sample_q[4].data : sample_q[2].data;

            byte op3 = (opcount < 3) ? (byte)0 : sample_q[(opcode == 0x22) ? 5 : 3].data;

            // Memory Modelling: Instruction fetches
            if (PB >= 0 && PC >= 0)
            {
                int pc = (PB << 16) + PC;
                memory_read(opcode, pc++, mem_access_t.MEM_FETCH);
                if (opcount >= 1)
                {
                    memory_read(op1, pc++, mem_access_t.MEM_INSTR);
                }
                if (opcount >= 2)
                {
                    memory_read(op2, pc++, mem_access_t.MEM_INSTR);
                }
                if (opcount >= 3)
                {
                    memory_read(op3, pc++, mem_access_t.MEM_INSTR);
                }
            }

            // Save the instruction state
            instruction.opcode = opcode;
            instruction.op1 = op1;
            instruction.op2 = op2;
            instruction.op3 = op3;
            instruction.opcount = opcount;


            // Fill in the current PB/PC value
            if (opcode == 0x00 || opcode == 0x02)
            {
                // BRK or COP - handle in the same way as an interrupt
                // Now just pass BRK onto the interrupt handler
                interrupt(sample_q, num_cycles, instruction, 2);
                // And we are done
                return;
            }
            else if (opcode == 0x20)
            {
                // JAddrMode.SR: <opcode> <op1> <op2> <read dummy> <write pch> <write pcl>
                instruction.pc = (((sample_q[4].data << 8) + sample_q[5].data) - 2) & 0xffff;
                instruction.pb = PB;
            }
            else if (opcode == 0x22)
            {
                // JSL: <opcode> <op1> <op2> <write pbr> <read dummy> <op3> <write pch> <write pcl>
                instruction.pc = (((sample_q[6].data << 8) + sample_q[7].data) - 3) & 0xffff;
                instruction.pb = sample_q[3].data;
            }
            else
            {
                instruction.pc = PC;
                instruction.pb = PB;
            }

            // Take account for optional extra cycle for direct register low (DL) not equal 0.
            int dpextra = (instr.mode <= AddrMode.ZP && DP >= 0 && (DP & 0xff) != 0) ? 1 : 0;

            // DP page wrapping only happens:
            // - in Emulation Mode (E=1), and
            // - if DPL == 00, and
            // - only for old instructions
            bool wrap = E == Tristate.True && (DP & 0xff) == 0 && (instr.newop) == 0;

            // Memory Modelling: Pointer indirection
            switch (instr.mode)
            {
                case AddrMode.INDY:
                    // <opcode> <op1> [ <dpextra> ] <addrlo> <addrhi> [ <page crossing>] <operand>
                    if (DP >= 0)
                    {
                        if (wrap)
                        {
                            memory_read(sample_q[2 + dpextra].data, (DP & 0xFF00) + op1, mem_access_t.MEM_POINTER);
                            memory_read(sample_q[3 + dpextra].data, (DP & 0xFF00) + ((op1 + 1) & 0xff), mem_access_t.MEM_POINTER);
                        }
                        else
                        {
                            memory_read(sample_q[2 + dpextra].data, DP + op1, mem_access_t.MEM_POINTER);
                            memory_read(sample_q[3 + dpextra].data, DP + op1 + 1, mem_access_t.MEM_POINTER);
                        }
                    }
                    break;
                case AddrMode.INDX:
                    // <opcode> <op1> [ <dpextra> ] <dummy> <addrlo> <addrhi> <operand>
                    if (DP >= 0 && X >= 0)
                    {
                        if (wrap)
                        {
                            memory_read(sample_q[3 + dpextra].data, (DP & 0xFF00) + ((op1 + X) & 0xff), mem_access_t.MEM_POINTER);
                            memory_read(sample_q[4 + dpextra].data, (DP & 0xFF00) + ((op1 + X + 1) & 0xff), mem_access_t.MEM_POINTER);
                        }
                        else
                        {
                            memory_read(sample_q[3 + dpextra].data, DP + op1 + X, mem_access_t.MEM_POINTER);
                            memory_read(sample_q[4 + dpextra].data, DP + op1 + X + 1, mem_access_t.MEM_POINTER);
                        }
                    }
                    break;
                case AddrMode.IND:
                    // <opcode> <op1>  [ <dpextra> ] <addrlo> <addrhi> <operand>
                    if (DP >= 0)
                    {
                        if (wrap)
                        {
                            memory_read(sample_q[2 + dpextra].data, (DP & 0xFF00) + op1, mem_access_t.MEM_POINTER);
                            memory_read(sample_q[3 + dpextra].data, (DP & 0xFF00) + ((op1 + 1) & 0xff), mem_access_t.MEM_POINTER);
                        }
                        else
                        {
                            memory_read(sample_q[2 + dpextra].data, DP + op1, mem_access_t.MEM_POINTER);
                            memory_read(sample_q[3 + dpextra].data, DP + op1 + 1, mem_access_t.MEM_POINTER);
                        }
                    }
                    break;
                case AddrMode.ISY:
                    // e.g. LDA (08, S),Y
                    // <opcode> <op1> <internal> <addrlo> <addrhi> <internal> <operand>
                    if (SL >= 0 && SH >= 0)
                    {
                        memory_read(sample_q[3].data, ((SH << 8) + SL + op1) & 0xffff, mem_access_t.MEM_POINTER);
                        memory_read(sample_q[4].data, ((SH << 8) + SL + op1 + 1) & 0xffff, mem_access_t.MEM_POINTER);
                    }
                    break;
                case AddrMode.IDL:
                    // e.g. LDA [80]
                    // <opcode> <op1> [ <dpextra> ] <addrlo> <addrhi> <bank> <operand>
                    if (DP >= 0)
                    {
                        memory_read(sample_q[2 + dpextra].data, DP + op1, mem_access_t.MEM_POINTER);
                        memory_read(sample_q[3 + dpextra].data, DP + op1 + 1, mem_access_t.MEM_POINTER);
                        memory_read(sample_q[4 + dpextra].data, DP + op1 + 2, mem_access_t.MEM_POINTER);
                    }
                    break;
                case AddrMode.IDLY:
                    // e.g. LDA [80],Y
                    // <opcode> <op1> [ <dpextra> ] <addrlo> <addrhi> <bank> <operand>
                    if (DP >= 0)
                    {
                        memory_read(sample_q[2 + dpextra].data, DP + op1, mem_access_t.MEM_POINTER);
                        memory_read(sample_q[3 + dpextra].data, DP + op1 + 1, mem_access_t.MEM_POINTER);
                        memory_read(sample_q[4 + dpextra].data, DP + op1 + 2, mem_access_t.MEM_POINTER);
                    }
                    break;
                case AddrMode.IAL:
                    // e.g. JMP [$1234] (this is the only one)
                    // <opcode> <op1> <op2> <addrlo> <addrhi> <bank>
                    memory_read(sample_q[3].data, (op2 << 8) + op1, mem_access_t.MEM_POINTER);
                    memory_read(sample_q[4].data, ((op2 << 8) + op1 + 1) & 0xffff, mem_access_t.MEM_POINTER);
                    memory_read(sample_q[5].data, ((op2 << 8) + op1 + 2) & 0xffff, mem_access_t.MEM_POINTER);
                    break;
                case AddrMode.IND16:
                    // e.g. JMP (1234)
                    // <opcode> <op1> <op2> <addrlo> <addrhi>
                    memory_read(sample_q[3].data, (op2 << 8) + op1, mem_access_t.MEM_POINTER);
                    memory_read(sample_q[4].data, ((op2 << 8) + op1 + 1) & 0xffff, mem_access_t.MEM_POINTER);
                    break;
                case AddrMode.IND1X:
                    // JMP: <opcode=6C> <op1> <op2> <read new pcl> <read new pch>
                    // JAddrMode.SR: <opcode=FC> <op1> <write pch> <write pcl> <op2> <internal> <read new pcl> <read new pch>
                    if (PB >= 0 && X >= 0)
                    {
                        memory_read(sample_q[num_cycles - 2].data, (PB << 16) + (((op2 << 8) + op1 + X) & 0xffff), mem_access_t.MEM_POINTER);
                        memory_read(sample_q[num_cycles - 1].data, (PB << 16) + (((op2 << 8) + op1 + X + 1) & 0xffff), mem_access_t.MEM_POINTER);
                    }
                    break;
                default:
                    break;
            }

            int operand;
            if (instr.optype == OpType.RMWOP)
            {
                // 2/12/2020: on Beeb816 the <read old> cycle seems hard to sample
                // reliably with the FX2, so lets use the <dummy> instead.
                // E=1 - Dummy is a write of the same data
                // <opcode> <op1> <op2> <read old> <write old> <write new>
                // E=0 - Dummy is an internal cycle (with VPA/VDA=00)
                // MS == 1:       <opcode> <op1> <op2> <read lo> <read hi> <dummy> <write hi> <write lo>
                // MS == 0:       <opcode> <op1> <op2> <read> <dummy> <write>
                if (E == Tristate.True)
                {
                    operand = sample_q[num_cycles - 2].data;
                }
                else if (MS == 0)
                {
                    // 16-bit mode
                    operand = (sample_q[num_cycles - 4].data << 8) + sample_q[num_cycles - 5].data;
                }
                else
                {
                    // 8-bit mode
                    operand = sample_q[num_cycles - 3].data;
                }
            }
            else if (instr.optype == OpType.BRANCHOP)
            {
                // the operand is true if branch taken
                operand = (num_cycles != 2)?1:0;
            }
            else if (opcode == 0x20)
            {
                // JAddrMode.SR abs: the operand is the data pushed to the stack (PCH, PCL)
                // <opcode> <op1> <op2> <read dummy> <write pch> <write pcl>
                operand = (sample_q[4].data << 8) + sample_q[5].data;
            }
            else if (opcode == 0xfc)
            {
                // JAddrMode.SR (AddrMode.IND, X): the operand is the data pushed to the stack (PCH, PCL)
                // <opcode> <op1> <write pch> <write pcl> <op2> <internal> <read new pcl> <read new pch>
                operand = (sample_q[2].data << 8) + sample_q[3].data;
            }
            else if (opcode == 0x22)
            {
                // JSL: the operand is the data pushed to the stack (PCB, PCH, PCL)
                // <opcode> <op1> <op2> <write pbr> <read dummy> <op3> <write pch> <write pcl>
                operand = (sample_q[3].data << 16) + (sample_q[6].data << 8) + sample_q[7].data;
            }
            else if (opcode == 0x40)
            {
                // RTI: the operand is the data pulled from the stack (P, PCL, PCH)
                // E=0: <opcode> <op1> <read dummy> <read p> <read pcl> <read pch> <read pbr>
                // E=1: <opcode> <op1> <read dummy> <read p> <read pcl> <read pch>
                operand = (sample_q[5].data << 16) + (sample_q[4].data << 8) + sample_q[3].data;
                if (num_cycles == 6)
                {
                    emulation_mode_on();
                }
                else
                {
                    emulation_mode_off();
                    operand |= (sample_q[6].data << 24);
                }
            }
            else if (opcode == 0x60)
            {
                // RTS: the operand is the data pulled from the stack (PCL, PCH)
                // <opcode> <op1> <read dummy> <read pcl> <read pch> <read dummy>
                operand = (sample_q[4].data << 8) + sample_q[3].data;
            }
            else if (opcode == 0x6B)
            {
                // RTL: the operand is the data pulled from the stack (PCL, PCH, PBR)
                // <opcode> <op1> <read dummy> <read pcl> <read pch> <read pbr>
                operand = (sample_q[5].data << 16) + (sample_q[4].data << 8) + sample_q[3].data;
            }
            else if (instr.mode == AddrMode.BM)
            {
                // Block Move
                operand = sample_q[3].data;
            }
            else if (instr.mode == AddrMode.IMM)
            {
                // Immediate addressing mode: the operand is the 2nd byte of the instruction
                operand = (op2 << 8) + op1;
            }
            else
            {
                // default to using the last bus cycle(s) as the operand
                // special case PHD (0B) / PLD (2B) / PEI (D4) as these are always 16-bit
                if ((instr.m_extra != 0 && (MS == Tristate.False)) || (instr.x_extra != 0 && (XS == Tristate.False)) || opcode == 0x0B || opcode == 0x2B || opcode == 0xD4)
                {
                    // 16-bit operation
                    if (opcode == 0x48 || opcode == 0x5A || opcode == 0xDA || opcode == 0x0B || opcode == 0xD4)
                    {
                        // PHA/PHX/PHY/PHD push high byte followed by low byte
                        operand = sample_q[num_cycles - 1].data + (sample_q[num_cycles - 2].data << 8);
                    }
                    else
                    {
                        // all other 16-bit ops are low byte then high byer
                        operand = sample_q[num_cycles - 2].data + (sample_q[num_cycles - 1].data << 8);
                    }
                }
                else
                {
                    // 8-bit operation
                    operand = sample_q[num_cycles - 1].data;
                }
            }

            // Operand 2 is the value written back in a store or read-modify-write
            // See RMW comment above for bus cycles
            operand_t operand2 = operand;
            if (instr.optype == OpType.RMWOP)
            {
                if (E == 0 && MS == 0)
                {
                    // 16-bit - byte ordering is high then low
                    operand2 = (sample_q[num_cycles - 2].data << 8) + sample_q[num_cycles - 1].data;
                }
                else
                {
                    // 8-bit
                    operand2 = sample_q[num_cycles - 1].data;
                }
            }
            else if (instr.optype == OpType.WRITEOP)
            {
                if (E == 0 && MS == 0)
                {
                    // 16-bit - byte ordering is low then high
                    operand2 = (sample_q[num_cycles - 1].data << 8) + sample_q[num_cycles - 2].data;
                }
                else
                {
                    operand2 = sample_q[num_cycles - 1].data;
                }
            }

            // For instructions that read or write memory, we need to work out the effective address
            // Note: not needed for stack operations, as S is used directly
            int ea = -1;
            int index;
            switch (instr.mode)
            {
                case AddrMode.ZP:
                    if (DP >= 0)
                    {
                        ea = (DP + op1) & 0xffff; // always bank 0
                    }
                    break;
                case AddrMode.ZPX:
                case AddrMode.ZPY:
                    index = instr.mode == AddrMode.ZPX ? X : Y;
                    if (index >= 0 && DP >= 0)
                    {
                        if (wrap)
                        {
                            ea = (DP & 0xff00) + ((op1 + index) & 0xff);
                        }
                        else
                        {
                            ea = (DP + op1 + index) & 0xffff; // always bank 0
                        }
                    }
                    break;
                case AddrMode.INDY:
                    // <opcode> <op1> [ <dpextra> ] <addrlo> <addrhi> [ <page crossing>] <operand>
                    index = Y;
                    if (index >= 0 && DB >= 0)
                    {
                        ea = (sample_q[3 + dpextra].data << 8) + sample_q[2 + dpextra].data;
                        ea = ((DB << 16) + ea + index) & 0xffffff;
                    }
                    break;
                case AddrMode.INDX:
                    // <opcode> <op1> [ <dpextra> ] <dummy> <addrlo> <addrhi> <operand>
                    if (DB >= 0)
                    {
                        ea = (DB << 16) + (sample_q[4 + dpextra].data << 8) + sample_q[3 + dpextra].data;
                    }
                    break;
                case AddrMode.IND:
                    // <opcode> <op1>  [ <dpextra> ] <addrlo> <addrhi> <operand>
                    if (DB >= 0)
                    {
                        ea = (DB << 16) + (sample_q[3 + dpextra].data << 8) + sample_q[2 + dpextra].data;
                    }
                    break;
                case AddrMode.ABS:
                    if (DB >= 0)
                    {
                        ea = (DB << 16) + (op2 << 8) + op1;
                    }
                    break;
                case AddrMode.ABSX:
                case AddrMode.ABSY:
                    index = instr.mode == AddrMode.ABSX ? X : Y;
                    if (index >= 0 && DB >= 0)
                    {
                        ea = ((DB << 16) + (op2 << 8) + op1 + index) & 0xffffff;
                    }
                    break;
                case AddrMode.BRA:
                    if (PC > 0)
                    {
                        ea = (PC + ((sbyte)op1) + 2) & 0xffff;
                    }
                    break;
                case AddrMode.SR:
                    // e.g. LDA 08,S
                    if (SL >= 0 && SH >= 0)
                    {
                        ea = ((SH << 8) + SL + op1) & 0xffff;
                    }
                    break;
                case AddrMode.ISY:
                    // e.g. LDA (08, S),Y
                    // <opcode> <op1> <internal> <addrlo> <addrhi> <internal> <operand>
                    index = Y;
                    if (index >= 0 && DB >= 0)
                    {
                        ea = (DB << 16) + (sample_q[4].data << 8) + sample_q[3].data;
                        ea = (ea + index) & 0xffffff;
                    }
                    break;
                case AddrMode.IDL:
                    // e.g. LDA [80]
                    // <opcode> <op1> [ <dpextra> ] <addrlo> <addrhi> <bank> <operand>
                    ea = (sample_q[4 + dpextra].data << 16) + (sample_q[3 + dpextra].data << 8) + sample_q[2 + dpextra].data;
                    break;
                case AddrMode.IDLY:
                    // e.g. LDA [80],Y
                    // <opcode> <op1> [ <dpextra> ] <addrlo> <addrhi> <bank> <operand>
                    index = Y;
                    if (index >= 0)
                    {
                        ea = (sample_q[4 + dpextra].data << 16) + (sample_q[3 + dpextra].data << 8) + sample_q[2 + dpextra].data;
                        ea = (ea + index) & 0xffffff;
                    }
                    break;
                case AddrMode.ABL:
                    // e.g. LDA EE0080
                    ea = (op3 << 16) + (op2 << 8) + op1;
                    break;
                case AddrMode.ALX:
                    // e.g. LDA EE0080,X
                    if (X >= 0)
                    {
                        ea = ((op3 << 16) + (op2 << 8) + op1 + X) & 0xffffff;
                    }
                    break;
                case AddrMode.IAL:
                    // e.g. JMP [$1234] (this is the only one)
                    // <opcode> <op1> <op2> <addrlo> <addrhi> <bank>
                    ea = (sample_q[5].data << 16) + (sample_q[4].data << 8) + sample_q[3].data;
                    break;
                case AddrMode.BRL:
                    // e.g. PER 1234 or AddrMode.BRL 1234
                    if (PC > 0)
                    {
                        ea = (PC + ((short)((op2 << 8) + op1)) + 3) & 0xffff;
                    }
                    break;
                case AddrMode.BM:
                    // e.g. MVN 0, 2
                    ea = (op2 << 8) + op1;
                    break;
                default:
                    // covers AddrMode.IMM, AddrMode.IMP, AddrMode.IMPA, AddrMode.IND16, AddrMode.IND1X
                    break;
            }

            if (instr.emulate != null)
            {

                // Is direct page access, as this wraps within bank 0
                bool isDP = instr.mode == AddrMode.ZP || instr.mode == AddrMode.ZPX || instr.mode == AddrMode.ZPY;

                // Determine memory access size
                int size = instr.x_extra != 0 ? (int)XS : instr.m_extra != 0 ? (int)MS : 1;

                // Model memory reads
                if (ea >= 0 && (instr.optype == OpType.READOP || instr.optype == OpType.RMWOP))
                {
                    int oplo = (operand & 0xff);
                    int ophi = ((operand >> 8) & 0xff);
                    if (size == 0)
                    {
                        memory_read(oplo, ea, mem_access_t.MEM_DATA);
                        if (isDP)
                        {
                            memory_read(ophi, (ea + 1) & 0xffff, mem_access_t.MEM_DATA);
                        }
                        else
                        {
                            memory_read(ophi, ea + 1, mem_access_t.MEM_DATA);
                        }
                    }
                    else if (size > 0)
                    {
                        memory_read(oplo, ea, mem_access_t.MEM_DATA);
                    }
                }

                // Execute the instruction specific function
                // (This returns -1 if the result is unknown or invalid)
                int result = instr.emulate(operand, ea);

                if (instr.optype == OpType.WRITEOP || instr.optype == OpType.RMWOP)
                {

                    // STA STX STY STZ
                    // INC DEX ASL LAddrMode.SR ROL ROR
                    // TSB TRB

                    // Check result of instruction against bye
                    if (result >= 0 && result != operand2)
                    {
                        failflag = true;
                    }

                    // Model memory writes based on result seen on bus
                    if (ea >= 0)
                    {
                        memory_write(operand2 & 0xff, ea, mem_access_t.MEM_DATA);
                        if (size == 0)
                        {
                            if (isDP)
                            {
                                memory_write((operand2 >> 8) & 0xff, (ea + 1) & 0xffff, mem_access_t.MEM_DATA);
                            }
                            else
                            {
                                memory_write((operand2 >> 8) & 0xff, ea + 1, mem_access_t.MEM_DATA);
                            }
                        }
                    }
                }
            }

            // Look for control flow changes and update the PC
            if (opcode == 0x40)
            {
                // E=0: <opcode> <op1> <read dummy> <read p> <read pcl> <read pch> <read pbr>
                // E=1: <opcode> <op1> <read dummy> <read p> <read pcl> <read pch>
                PC = sample_q[4].data | (sample_q[5].data << 8);
                if (E == 0)
                {
                    PB = sample_q[6].data;
                }
            }
            else if (opcode == 0x6c || opcode == 0x7c || opcode == 0xfc)
            {
                // JMP (ind), JMP (ind, X), JAddrMode.SR (ind, X)
                PC = (sample_q[num_cycles - 1].data << 8) | sample_q[num_cycles - 2].data;
            }
            else if (opcode == 0x20 || opcode == 0x4c)
            {
                // JAddrMode.SR abs, JMP abs
                // Don't use ea here as it includes PB which may be unknown
                PC = (op2 << 8) + op1;
            }
            else if (opcode == 0x22 || opcode == 0x5c || opcode == 0xdc)
            {
                // JSL long, JML long
                PB = (ea >> 16) & 0xff;
                PC = (ea & 0xffff);
            }
            else if (PC < 0)
            {
                // PC value is not known yet, everything below this point is relative
                PC = -1;
            }
            else if (opcode == 0x80 || opcode == 0x82)
            {
                // AddrMode.BRA / AddrMode.BRL
                PC = ea;
            }
            else if ((opcode & 0x1f) == 0x10 && num_cycles != 2)
            {
                // BXX: if taken
                PC = ea;
            }
            else
            {
                // Otherwise, increment pc by length of instuction
                PC = (PC + opcount + 1) & 0xffff;
            }
        }

        /*
        static int em_65816_disassemble(string buffer, instruction_t instruction)
        {

            int numchars;
            int offset;
            char target[16];

            // Unpack the instruction bytes
            int opcode = instruction.opcode;
            int op1 = instruction.op1;
            int op2 = instruction.op2;
            int op3 = instruction.op3;
            int pc = instruction.pc;
            int opcount = instruction.opcount;
            // lookup the entry for the instruction
            InstrType* instr = &instr_table[opcode];

            const char* mnemonic = instr.mnemonic;
            const char* fmt = instr.fmt;
            switch (instr.mode)
            {
                case AddrMode.IMP:
                case AddrMode.IMPA:
                    numchars = sprintf(buffer, fmt, mnemonic);
                    break;
                case AddrMode.BRA:
                    // Calculate branch target using op1 for normal branches
                    offset = (int8_t)op1;
                    if (pc < 0)
                    {
                        if (offset < 0)
                        {
                            sprintf(target, "pc-%d", -offset);
                        }
                        else
                        {
                            sprintf(target, "pc+%d", offset);
                        }
                    }
                    else
                    {
                        sprintf(target, "%04X", (pc + 2 + offset) & 0xffff);
                    }
                    numchars = sprintf(buffer, fmt, mnemonic, target);
                    break;
                case AddrMode.BRL:
                    // Calculate branch target using op1 for normal branches
                    offset = (int16_t)((op2 << 8) + op1);
                    if (pc < 0)
                    {
                        if (offset < 0)
                        {
                            sprintf(target, "pc-%d", -offset);
                        }
                        else
                        {
                            sprintf(target, "pc+%d", offset);
                        }
                    }
                    else
                    {
                        sprintf(target, "%04X", (pc + 3 + offset) & 0xffff);
                    }
                    numchars = sprintf(buffer, fmt, mnemonic, target);
                    break;
                case AddrMode.IMM:
                    if (opcount == 2)
                    {
                        numchars = sprintf(buffer, fmt_imm16, mnemonic, op1, op2);
                    }
                    else
                    {
                        numchars = sprintf(buffer, fmt, mnemonic, op1);
                    }
                    break;
                case AddrMode.ZP:
                case AddrMode.ZPX:
                case AddrMode.ZPY:
                case AddrMode.INDX:
                case AddrMode.INDY:
                case AddrMode.IND:
                case AddrMode.SR:
                case AddrMode.ISY:
                case AddrMode.IDL:
                case AddrMode.IDLY:
                    numchars = sprintf(buffer, fmt, mnemonic, op1);
                    break;
                case AddrMode.ABS:
                case AddrMode.ABSX:
                case AddrMode.ABSY:
                case AddrMode.IND16:
                case AddrMode.IND1X:
                case AddrMode.IAL:
                case AddrMode.BM:
                    numchars = sprintf(buffer, fmt, mnemonic, op1, op2);
                    break;
                case AddrMode.ABL:
                case AddrMode.ALX:
                    numchars = sprintf(buffer, fmt, mnemonic, op1, op2, op3);
                    break;
                default:
                    numchars = 0;
            }

            return numchars;
        }
        */

        int em_65816_read_memory(int address)
        {
            return memory_read_raw(address);
        }

        /*
        static char* em_65816_get_state(char* buffer)
        {
            strcpy(buffer, default_state);
            if (B >= 0)
            {
                write_hex2(buffer + OFFSET_B, B);
            }
            if (A >= 0)
            {
                write_hex2(buffer + OFFSET_A, A);
            }
            if (X >= 0)
            {
                write_hex4(buffer + OFFSET_X, X);
            }
            if (Y >= 0)
            {
                write_hex4(buffer + OFFSET_Y, Y);
            }
            if (SH >= 0)
            {
                write_hex2(buffer + OFFSET_SH, SH);
            }
            if (SL >= 0)
            {
                write_hex2(buffer + OFFSET_SL, SL);
            }
            if (N >= 0)
            {
                buffer[OFFSET_N] = '0' + N;
            }
            if (V >= 0)
            {
                buffer[OFFSET_V] = '0' + V;
            }
            if (MS >= 0)
            {
                buffer[OFFSET_MS] = '0' + MS;
            }
            if (XS >= 0)
            {
                buffer[OFFSET_XS] = '0' + XS;
            }
            if (D >= 0)
            {
                buffer[OFFSET_D] = '0' + D;
            }
            if (I >= 0)
            {
                buffer[OFFSET_I] = '0' + I;
            }
            if (Z >= 0)
            {
                buffer[OFFSET_Z] = '0' + Z;
            }
            if (C >= 0)
            {
                buffer[OFFSET_C] = '0' + C;
            }
            if (E >= 0)
            {
                buffer[OFFSET_E] = '0' + E;
            }
            if (PB >= 0)
            {
                write_hex2(buffer + OFFSET_PB, PB);
            }
            if (DB >= 0)
            {
                write_hex2(buffer + OFFSET_DB, DB);
            }
            if (DP >= 0)
            {
                write_hex4(buffer + OFFSET_DP, DP);
            }
            return buffer + OFFSET_END;
        }
        */

        bool em_65816_get_and_clear_fail()
        {
            bool ret = failflag;
            failflag = false;
            return ret;
        }

        /*
        cpu_emulator_t em_65816 = {
   .init = em_65816_init,
   .match_interrupt = em_65816_match_interrupt,
   .count_cycles = em_65816_count_cycles,
   .reset = em_65816_reset,
   .interrupt = em_65816_interrupt,
   .emulate = em_65816_emulate,
   .disassemble = em_65816_disassemble,
   .get_PC = em_65816_get_PC,
   .get_PB = em_65816_get_PB,
   .read_memory = em_65816_read_memory,
   .get_state = em_65816_get_state,
   .get_and_clear_fail = em_65816_get_and_clear_fail,
};
        */

        // ====================================================================
        // 65816 specific instructions
        // ====================================================================

        // Push Effective Absolute Address
        static int op_PEA(em65816 em, operand_t operand, ea_t ea)
        {
            // always pushes a 16-bit value
            push16(ea);
            return -1;
        }

        // Push Effective Relative Address
		static int op_PER(em65816 em, operand_t operand, ea_t ea)
        {
            // always pushes a 16-bit value
            push16(ea);
            return -1;
        }

        // Push Effective Indirect Address
		static int op_PEI(em65816 em, operand_t operand, ea_t ea)
        {
            // always pushes a 16-bit value
            push16(operand);
            return -1;
        }

        // Push Data Bank Register
		static int op_PHB(em65816 em, operand_t operand, ea_t ea)
        {
            push8(operand);
            if (DB >= 0)
            {
                if (operand != DB)
                {
                    failflag = 1;
                }
            }
            DB = operand;
            return -1;
        }

        // Push Program Bank Register
		static int op_PHK(em65816 em, operand_t operand, ea_t ea)
        {
            push8(operand);
            if (PB >= 0)
            {
                if (operand != PB)
                {
                    failflag = 1;
                }
            }
            PB = operand;
            return -1;
        }

        // Push Direct Page Register
		static int op_PHD(em65816 em, operand_t operand, ea_t ea)
        {
            push16(operand);
            if (DP >= 0)
            {
                if (operand != DP)
                {
                    failflag = 1;
                }
            }
            DP = operand;
            return -1;
        }

        // Pull Data Bank Register
		static int op_PLB(em65816 em, operand_t operand, ea_t ea)
        {
            DB = operand;
            set_NZ8(DB);
            pop8(operand);
            return -1;
        }

        // Pull Direct Page Register
		static int op_PLD(em65816 em, operand_t operand, ea_t ea)
        {
            DP = operand;
            set_NZ16(DP);
            pop16(operand);
            return -1;
        }

		static int op_MV(em65816 em, int data, int sba, int dba, int dir)
        {
            // operand is the data byte (from the bus read)
            // ea = (op2 << 8) + op1 == (srcbank << 8) + dstbank;
            if (X >= 0)
            {
                memory_read(data, (sba << 16) + X, mem_access_t.MEM_sample_type_t.DATA);
            }
            if (Y >= 0)
            {
                memory_write(data, (dba << 16) + Y, mem_access_t.MEM_sample_type_t.DATA);
            }
            if (A >= 0 && B >= 0)
            {
                int C = (((B << 8) | A) - 1) & 0xffff;
                A = C & 0xff;
                B = (C >> 8) & 0xff;
                if (X >= 0)
                {
                    X = (X + dir) & 0xffff;
                }
                if (Y >= 0)
                {
                    Y = (Y + dir) & 0xffff;
                }
                if (PC >= 0 && C != 0xffff)
                {
                    PC -= 3;
                }
            }
            else
            {
                A = -1;
                B = -1;
                X = -1;
                Y = -1;
                PC = -1;
            }
            // Set the Data Bank to the destination bank
            DB = dba;
            return -1;
        }

        // Block Move (Decrementing)
		static int op_MVP(em65816 em, operand_t operand, ea_t ea)
        {
            return op_MV(operand, (ea >> 8) & 0xff, ea & 0xff, -1);
        }

        // Block Move (Incrementing)
		static int op_MVN(em65816 em, operand_t operand, ea_t ea)
        {
            return op_MV(operand, (ea >> 8) & 0xff, ea & 0xff, 1);
        }

        // Transfer Transfer C accumulator to Direct Page register
		static int op_TCD(em65816 em, operand_t operand, ea_t ea)
        {
            // Always a 16-bit transfer
            if (B >= 0 && A >= 0)
            {
                DP = (B << 8) + A;
                set_NZ16(DP);
            }
            else
            {
                DP = -1;
                set_NZ_unknown();
            }
            return -1;
        }

        // Transfer Transfer C accumulator to Stack pointer
		static int op_TCS(em65816 em, operand_t operand, ea_t ea)
        {
            SH = B;
            SL = A;
            return -1;
        }

        // Transfer Transfer Direct Page register to C accumulator
		static int op_TDC(em65816 em, operand_t operand, ea_t ea)
        {
            // Always a 16-bit transfer
            if (DP >= 0)
            {
                A = DP & 0xff;
                B = (DP >> 8) & 0xff;
                set_NZ16(DP);
            }
            else
            {
                A = -1;
                B = -1;
                set_NZ_unknown();
            }
            return -1;
        }

        // Transfer Transfer Stack pointer to C accumulator
		static int op_TSC(em65816 em, operand_t operand, ea_t ea)
        {
            // Always a 16-bit transfer
            A = SL;
            B = SH;
            if (B >= 0 && A >= 0)
            {
                set_NZ16((B << 8) + A);
            }
            else
            {
                set_NZ_unknown();
            }
            return -1;
        }

		static int op_TXY(em65816 em, operand_t operand, ea_t ea)
        {
            // Variable size transfer controlled by XS
            if (X >= 0)
            {
                Y = X;
                set_NZ_XS(Y);
            }
            else
            {
                Y = -1;
                set_NZ_unknown();
            }
            return -1;
        }

		static int op_TYX(em65816 em, operand_t operand, ea_t ea)
        {
            // Variable size transfer controlled by XS
            if (Y >= 0)
            {
                X = Y;
                set_NZ_XS(X);
            }
            else
            {
                X = -1;
                set_NZ_unknown();
            }
            return -1;
        }

        // Exchange A and B
		static int op_XBA(em65816 em, operand_t operand, ea_t ea)
        {
            int tmp = A;
            A = B;
            B = tmp;
            if (A >= 0)
            {
                // Always based on the 8-bit result of A
                set_NZ8(A);
            }
            else
            {
                set_NZ_unknown();
            }
            return -1;
        }

		static int op_XCE(em65816 em, operand_t operand, ea_t ea)
        {
            int tmp = C;
            C = E;
            E = tmp;
            if (tmp < 0)
            {
                MS = -1;
                XS = -1;
                E = -1;
            }
            else if (tmp > 0)
            {
                emulation_mode_on();
            }
            else
            {
                emulation_mode_off();
            }
            return -1;
        }

        void repsep(int operand, int val)
        {
            if (operand & 0x80)
            {
                N = val;
            }
            if (operand & 0x40)
            {
                V = val;
            }
            if (E == 0)
            {
                if (operand & 0x20)
                {
                    MS = val;
                }
                if (operand & 0x10)
                {
                    XS = val;
                }
            }
            if (operand & 0x08)
            {
                D = val;
            }
            if (operand & 0x04)
            {
                I = val;
            }
            if (operand & 0x02)
            {
                Z = val;
            }
            if (operand & 0x01)
            {
                C = val;
            }
        }

        // Reset/Set Processor Status Bits
		static int op_REP(em65816 em, operand_t operand, ea_t ea)
        {
            repsep(operand, 0);
            return -1;
        }

		static int op_SEP(em65816 em, operand_t operand, ea_t ea)
        {
            repsep(operand, 1);
            return -1;
        }

        // Jump to Subroutine Long
		static int op_JSL(em65816 em, operand_t operand, ea_t ea)
        {
            // JAddrMode.SR: the operand is the data pushed to the stack (PB, PCH, PCL)
            push8(operand >> 16); // PB
            push16(operand);      // PC
            return -1;
        }

        // Return from Subroutine Long
		static int op_RTL(em65816 em, operand_t operand, ea_t ea)
        {
            // RTL: the operand is the data pulled from the stack (PCL, PCH, PB)
            pop16(operand);      // PC
            pop8(operand >> 16); // PB
                                 // The +1 is handled elsewhere
            PC = operand & 0xffff;
            PB = (operand >> 16) & 0xff;
            return -1;
        }

        // ====================================================================
        // 65816/6502 instructions
        // ====================================================================

		static int op_ADC(em65816 em, operand_t operand, ea_t ea)
        {
            int acc = get_accumulator();
            if (acc >= 0 && C >= 0)
            {
                int tmp = 0;
                if (D == 1)
                {
                    // Decimal mode ADC - works like a 65C02
                    // Working a nibble at a time, correct for both 8 and 18 bits
                    for (int bit = 0; bit < (MS ? 8 : 16); bit += 4)
                    {
                        int an = (acc >> bit) & 0xF;
                        int bn = (operand >> bit) & 0xF;
                        int rn = an + bn + C;
                        V = ((rn ^ an) & 8) && !((bn ^ an) & 8);
                        C = 0;
                        if (rn >= 10)
                        {
                            rn = (rn - 10) & 0xF;
                            C = 1;
                        }
                        tmp |= rn << bit;
                    }
                }
                else
                {
                    // Normal mode ADC
                    tmp = acc + operand + C;
                    if (MS > 0)
                    {
                        // 8-bit mode
                        C = (tmp >> 8) & 1;
                        V = (((acc ^ operand) & 0x80) == 0) && (((acc ^ tmp) & 0x80) != 0);
                    }
                    else
                    {
                        // 16-bit mode
                        C = (tmp >> 16) & 1;
                        V = (((acc ^ operand) & 0x8000) == 0) && (((acc ^ tmp) & 0x8000) != 0);
                    }
                }
                if (MS > 0)
                {
                    // 8-bit mode
                    A = tmp & 0xff;
                }
                else
                {
                    // 16-bit mode
                    A = tmp & 0xff;
                    B = (tmp >> 8) & 0xff;
                }
                set_NZ_AB(A, B);
            }
            else
            {
                A = -1;
                B = -1;
                set_NVZC_unknown();
            }
            return -1;
        }

		static int op_AND(em65816 em, operand_t operand, ea_t ea)
        {
            // A is always updated, regardless of the size
            if (A >= 0)
            {
                A = A & (operand & 0xff);
            }
            // B is updated only of the size is 16
            if (B >= 0)
            {
                if (MS == 0)
                {
                    B = B & (operand >> 8);
                }
                else if (MS < 0)
                {
                    B = -1;
                }
            }
            // Updating NZ is complex, depending on the whether A and/or B are unknown
            set_NZ_AB(A, B);
            return -1;
        }

		static int op_ASLA(em65816 em, operand_t operand, ea_t ea)
        {
            // Compute the new carry
            if (MS > 0 && A >= 0)
            {
                // 8-bit mode
                C = (A >> 7) & 1;
            }
            else if (MS == 0 && B >= 0)
            {
                // 16-bit mode
                C = (B >> 7) & 1;
            }
            else
            {
                // width unknown
                C = -1;
            }
            // Compute the new B
            if (MS == 0 && B >= 0)
            {
                if (A >= 0)
                {
                    B = ((B << 1) & 0xfe) | ((A >> 7) & 1);
                }
                else
                {
                    B = -1;
                }
            }
            else if (MS < 0)
            {
                B = -1;
            }
            // Compute the new A
            if (A >= 0)
            {
                A = (A << 1) & 0xff;
            }
            // Updating NZ is complex, depending on the whether A and/or B are unknown
            set_NZ_AB(A, B);
            return -1;
        }

		static int op_ASL(em65816 em, operand_t operand, ea_t ea)
        {
            int tmp;
            if (MS > 0)
            {
                // 8-bit mode
                C = (operand >> 7) & 1;
                tmp = (operand << 1) & 0xff;
                set_NZ8(tmp);
            }
            else if (MS == 0)
            {
                // 16-bit mode
                C = (operand >> 15) & 1;
                tmp = (operand << 1) & 0xffff;
                set_NZ16(tmp);
            }
            else
            {
                // mode unknown
                C = -1;
                tmp = -1;
                set_NZ_unknown();
            }
            return tmp;
        }

		static int op_BCC(em65816 em, operand_t branch_taken, ea_t ea)
        {
            if (C >= 0)
            {
                if (C == branch_taken)
                {
                    failflag = 1;
                }
            }
            else
            {
                C = 1 - branch_taken;
            }
            return -1;
        }

		static int op_BCS(em65816 em, operand_t branch_taken, ea_t ea)
        {
            if (C >= 0)
            {
                if (C != branch_taken)
                {
                    failflag = 1;
                }
            }
            else
            {
                C = branch_taken;
            }
            return -1;
        }

		static int op_BNE(em65816 em, operand_t branch_taken, ea_t ea)
        {
            if (Z >= 0)
            {
                if (Z == branch_taken)
                {
                    failflag = 1;
                }
            }
            else
            {
                Z = 1 - branch_taken;
            }
            return -1;
        }

		static int op_BEQ(em65816 em, operand_t branch_taken, ea_t ea)
        {
            if (Z >= 0)
            {
                if (Z != branch_taken)
                {
                    failflag = 1;
                }
            }
            else
            {
                Z = branch_taken;
            }
            return -1;
        }

		static int op_BPL(em65816 em, operand_t branch_taken, ea_t ea)
        {
            if (N >= 0)
            {
                if (N == branch_taken)
                {
                    failflag = 1;
                }
            }
            else
            {
                N = 1 - branch_taken;
            }
            return -1;
        }

		static int op_BMI(em65816 em, operand_t branch_taken, ea_t ea)
        {
            if (N >= 0)
            {
                if (N != branch_taken)
                {
                    failflag = 1;
                }
            }
            else
            {
                N = branch_taken;
            }
            return -1;
        }

		static int op_BVC(em65816 em, operand_t branch_taken, ea_t ea)
        {
            if (V >= 0)
            {
                if (V == branch_taken)
                {
                    failflag = 1;
                }
            }
            else
            {
                V = 1 - branch_taken;
            }
            return -1;
        }

		static int op_BVS(em65816 em, operand_t branch_taken, ea_t ea)
        {
            if (V >= 0)
            {
                if (V != branch_taken)
                {
                    failflag = 1;
                }
            }
            else
            {
                V = branch_taken;
            }
            return -1;
        }

		static int op_BIT_IMM(em65816 em, operand_t operand, ea_t ea)
        {
            int acc = get_accumulator();
            if (operand == 0)
            {
                // This makes the remainder less pessimistic
                Z = 1;
            }
            else if (acc >= 0)
            {
                // both acc and operand will be the correct width
                Z = (acc & operand) == 0;
            }
            else
            {
                Z = -1;
            }
            return -1;
        }

		static int op_BIT(em65816 em, operand_t operand, ea_t ea)
        {
            if (MS > 0)
            {
                // 8-bit mode
                N = (operand >> 7) & 1;
                V = (operand >> 6) & 1;
            }
            else if (MS == 0)
            {
                // 16-bit mode
                N = (operand >> 15) & 1;
                V = (operand >> 14) & 1;
            }
            else
            {
                // mode undefined
                N = -1; // could be less pessimistic
                V = -1; // could be less pessimistic
            }
            // the rest is the same as BIT immediate (i.e. setting the Z flag)
            return op_BIT_AddrMode.IMM(operand, ea);
        }

		static int op_CLC(em65816 em, operand_t operand, ea_t ea)
        {
            C = 0;
            return -1;
        }

		static int op_CLD(em65816 em, operand_t operand, ea_t ea)
        {
            D = 0;
            return -1;
        }

		static int op_CLI(em65816 em, operand_t operand, ea_t ea)
        {
            I = 0;
            return -1;
        }

		static int op_CLV(em65816 em, operand_t operand, ea_t ea)
        {
            V = 0;
            return -1;
        }

		static int op_CMP(em65816 em, operand_t operand, ea_t ea)
        {
            int acc = get_accumulator();
            if (acc >= 0)
            {
                int tmp = acc - operand;
                C = tmp >= 0;
                set_NZ_MS(tmp);
            }
            else
            {
                set_NZC_unknown();
            }
            return -1;
        }

		static int op_CPX(em65816 em, operand_t operand, ea_t ea)
        {
            if (X >= 0)
            {
                int tmp = X - operand;
                C = tmp >= 0;
                set_NZ_XS(tmp);
            }
            else
            {
                set_NZC_unknown();
            }
            return -1;
        }

		static int op_CPY(em65816 em, operand_t operand, ea_t ea)
        {
            if (Y >= 0)
            {
                int tmp = Y - operand;
                C = tmp >= 0;
                set_NZ_XS(tmp);
            }
            else
            {
                set_NZC_unknown();
            }
            return -1;
        }

		static int op_DECA(em65816 em, operand_t operand, ea_t ea)
        {
            // Compute the new A
            if (A >= 0)
            {
                A = (A - 1) & 0xff;
            }
            // Compute the new B
            if (MS == 0 && B >= 0)
            {
                if (A == 0xff)
                {
                    B = (B - 1) & 0xff;
                }
                else if (A < 0)
                {
                    B = -1;
                }
            }
            else if (MS < 0)
            {
                B = -1;
            }
            // Updating NZ is complex, depending on the whether A and/or B are unknown
            set_NZ_AB(A, B);
            return -1;
        }

		static int op_DEC(em65816 em, operand_t operand, ea_t ea)
        {
            int tmp = -1;
            if (MS > 0)
            {
                // 8-bit mode
                tmp = (operand - 1) & 0xff;
                set_NZ8(tmp);
            }
            else if (MS == 0)
            {
                // 16-bit mode
                tmp = (operand - 1) & 0xffff;
                set_NZ16(tmp);
            }
            else
            {
                set_NZ_unknown();
            }
            return tmp;
        }

		static int op_DEX(em65816 em, operand_t operand, ea_t ea)
        {
            if (X >= 0)
            {
                if (XS > 0)
                {
                    // 8-bit mode
                    X = (X - 1) & 0xff;
                    set_NZ8(X);
                }
                else if (XS == 0)
                {
                    // 16-bit mode
                    X = (X - 1) & 0xffff;
                    set_NZ16(X);
                }
                else
                {
                    // mode undefined
                    X = -1;
                    set_NZ_unknown();
                }
            }
            else
            {
                set_NZ_unknown();
            }
            return -1;
        }

		static int op_DEY(em65816 em, operand_t operand, ea_t ea)
        {
            if (Y >= 0)
            {
                if (XS > 0)
                {
                    // 8-bit mode
                    Y = (Y - 1) & 0xff;
                    set_NZ8(Y);
                }
                else if (XS == 0)
                {
                    // 16-bit mode
                    Y = (Y - 1) & 0xffff;
                    set_NZ16(Y);
                }
                else
                {
                    // mode undefined
                    Y = -1;
                    set_NZ_unknown();
                }
            }
            else
            {
                set_NZ_unknown();
            }
            return -1;
        }

		static int op_EOR(em65816 em, operand_t operand, ea_t ea)
        {
            // A is always updated, regardless of the size
            if (A >= 0)
            {
                A = A ^ (operand & 0xff);
            }
            // B is updated only of the size is 16
            if (B >= 0)
            {
                if (MS == 0)
                {
                    B = B ^ (operand >> 8);
                }
                else if (MS < 0)
                {
                    B = -1;
                }
            }
            // Updating NZ is complex, depending on the whether A and/or B are unknown
            set_NZ_AB(A, B);
            return -1;
        }

		static int op_INCA(em65816 em, operand_t operand, ea_t ea)
        {
            // Compute the new A
            if (A >= 0)
            {
                A = (A + 1) & 0xff;
            }
            // Compute the new B
            if (MS == 0 && B >= 0)
            {
                if (A == 0x00)
                {
                    B = (B + 1) & 0xff;
                }
                else if (A < 0)
                {
                    B = -1;
                }
            }
            else if (MS < 0)
            {
                B = -1;
            }
            // Updating NZ is complex, depending on the whether A and/or B are unknown
            set_NZ_AB(A, B);
            return -1;
        }

		static int op_INC(em65816 em, operand_t operand, ea_t ea)
        {
            int tmp = -1;
            if (MS > 0)
            {
                // 8-bit mode
                tmp = (operand + 1) & 0xff;
                set_NZ8(tmp);
            }
            else if (MS == 0)
            {
                // 16-bit mode
                tmp = (operand + 1) & 0xffff;
                set_NZ16(tmp);
            }
            else
            {
                set_NZ_unknown();
            }
            return tmp;
        }

		static int op_INX(em65816 em, operand_t operand, ea_t ea)
        {
            if (X >= 0)
            {
                if (XS > 0)
                {
                    // 8-bit mode
                    X = (X + 1) & 0xff;
                    set_NZ8(X);
                }
                else if (XS == 0)
                {
                    // 16-bit mode
                    X = (X + 1) & 0xffff;
                    set_NZ16(X);
                }
                else
                {
                    // mode undefined
                    X = -1;
                    set_NZ_unknown();
                }
            }
            else
            {
                set_NZ_unknown();
            }
            return -1;
        }

		static int op_INY(em65816 em, operand_t operand, ea_t ea)
        {
            if (Y >= 0)
            {
                if (XS > 0)
                {
                    // 8-bit mode
                    Y = (Y + 1) & 0xff;
                    set_NZ8(Y);
                }
                else if (XS == 0)
                {
                    // 16-bit mode
                    Y = (Y + 1) & 0xffff;
                    set_NZ16(Y);
                }
                else
                {
                    // mode undefined
                    Y = -1;
                    set_NZ_unknown();
                }
            }
            else
            {
                set_NZ_unknown();
            }
            return -1;
        }

		static int op_JSR(em65816 em, operand_t operand, ea_t ea)
        {
            // JAddrMode.SR: the operand is the data pushed to the stack (PCH, PCL)
            push16(operand);  // PC
            return -1;
        }

		static int op_LDA(em65816 em, operand_t operand, ea_t ea)
        {
            A = operand & 0xff;
            if (MS == 0)
            {
                B = (operand >> 8) & 0xff;
            }
            set_NZ_AB(A, B);
            return -1;
        }

		static int op_LDX(em65816 em, operand_t operand, ea_t ea)
        {
            X = operand;
            set_NZ_XS(X);
            return -1;
        }

		static int op_LDY(em65816 em, operand_t operand, ea_t ea)
        {
            Y = operand;
            set_NZ_XS(Y);
            return -1;
        }

		static int op_LSRA(em65816 em, operand_t operand, ea_t ea)
        {
            // Compute the new carry
            if (A >= 0)
            {
                C = A & 1;
            }
            else
            {
                C = -1;
            }
            // Compute the new A
            if (MS > 0 && A >= 0)
            {
                A = A >> 1;
            }
            else if (MS == 0 && A >= 0 && B >= 0)
            {
                A = ((A >> 1) | (B << 7)) & 0xff;
            }
            else
            {
                A = -1;
            }
            // Compute the new B
            if (MS == 0 && B >= 0)
            {
                B = (B >> 1) & 0xff;
            }
            else if (MS < 0)
            {
                B = -1;
            }
            // Updating NZ is complex, depending on the whether A and/or B are unknown
            set_NZ_AB(A, B);
            return -1;
        }

		static int op_LSR(em65816 em, operand_t operand, ea_t ea)
        {
            int tmp;
            C = operand & 1;
            if (MS > 0)
            {
                // 8-bit mode
                tmp = (operand >> 1) & 0xff;
                set_NZ8(tmp);
            }
            else if (MS == 0)
            {
                // 16-bit mode
                tmp = (operand >> 1) & 0xffff;
                set_NZ16(tmp);
            }
            else
            {
                // mode unknown
                tmp = -1;
                set_NZ_unknown();
            }
            return tmp;
        }

		static int op_ORA(em65816 em, operand_t operand, ea_t ea)
        {
            // A is always updated, regardless of the size
            if (A >= 0)
            {
                A = A | (operand & 0xff);
            }
            // B is updated only of the size is 16
            if (B >= 0)
            {
                if (MS == 0)
                {
                    B = B | (operand >> 8);
                }
                else if (MS < 0)
                {
                    B = -1;
                }
            }
            // Updating NZ is complex, depending on the whether A and/or B are unknown
            set_NZ_AB(A, B);
            return -1;
        }

		static int op_PHA(em65816 em, operand_t operand, ea_t ea)
        {
            pushMS(operand);
            op_STA(operand, -1);
            return -1;
        }

		static int op_PHP(em65816 em, operand_t operand, ea_t ea)
        {
            push8(operand);
            check_FLAGS(operand);
            set_FLAGS(operand);
            return -1;
        }

		static int op_PHX(em65816 em, operand_t operand, ea_t ea)
        {
            pushXS(operand);
            op_STX(operand, -1);
            return -1;
        }

		static int op_PHY(em65816 em, operand_t operand, ea_t ea)
        {
            pushXS(operand);
            op_STY(operand, -1);
            return -1;
        }

		static int op_PLA(em65816 em, operand_t operand, ea_t ea)
        {
            A = operand & 0xff;
            if (MS < 0)
            {
                B = -1;
            }
            else if (MS == 0)
            {
                B = (operand >> 8);
            }
            set_NZ_MS(operand);
            popMS(operand);
            return -1;
        }

		static int op_PLP(em65816 em, operand_t operand, ea_t ea)
        {
            set_FLAGS(operand);
            pop8(operand);
            return -1;
        }

		static int op_PLX(em65816 em, operand_t operand, ea_t ea)
        {
            X = operand;
            set_NZ_XS(X);
            popXS(operand);
            return -1;
        }

		static int op_PLY(em65816 em, operand_t operand, ea_t ea)
        {
            Y = operand;
            set_NZ_XS(Y);
            popXS(operand);
            return -1;
        }

		static int op_ROLA(em65816 em, operand_t operand, ea_t ea)
        {
            // Save the old carry
            int oldC = C;
            // Compute the new carry
            if (MS > 0 && A >= 0)
            {
                // 8-bit mode
                C = (A >> 7) & 1;
            }
            else if (MS == 0 && B >= 0)
            {
                // 16-bit mode
                C = (B >> 7) & 1;
            }
            else
            {
                // width unknown
                C = -1;
            }
            // Compute the new B
            if (MS == 0 && B >= 0)
            {
                if (A >= 0)
                {
                    B = ((B << 1) & 0xfe) | ((A >> 7) & 1);
                }
                else
                {
                    B = -1;
                }
            }
            else if (MS < 0)
            {
                B = -1;
            }
            // Compute the new A
            if (A >= 0)
            {
                if (oldC >= 0)
                {
                    A = ((A << 1) | oldC) & 0xff;
                }
                else
                {
                    A = -1;
                }
            }
            // Updating NZ is complex, depending on the whether A and/or B are unknown
            set_NZ_AB(A, B);
            return -1;
        }

		static int op_ROL(em65816 em, operand_t operand, ea_t ea)
        {
            int oldC = C;
            int tmp;
            if (MS > 0)
            {
                // 8-bit mode
                C = (operand >> 7) & 1;
                tmp = ((operand << 1) | oldC) & 0xff;
                set_NZ8(tmp);
            }
            else if (MS == 0)
            {
                // 16-bit mode
                C = (operand >> 15) & 1;
                tmp = ((operand << 1) | oldC) & 0xffff;
                set_NZ16(tmp);
            }
            else
            {
                C = -1;
                tmp = -1;
                set_NZ_unknown();
            }
            return tmp;
        }

		static int op_RORA(em65816 em, operand_t operand, ea_t ea)
        {
            // Save the old carry
            int oldC = C;
            // Compute the new carry
            if (A >= 0)
            {
                C = A & 1;
            }
            else
            {
                C = -1;
            }
            // Compute the new A
            if (MS > 0 && A >= 0)
            {
                A = ((A >> 1) | (oldC << 7)) & 0xff;
            }
            else if (MS == 0 && A >= 0 && B >= 0)
            {
                A = ((A >> 1) | (B << 7)) & 0xff;
            }
            else
            {
                A = -1;
            }
            // Compute the new B
            if (MS == 0 && B >= 0 && oldC >= 0)
            {
                B = ((B >> 1) | (oldC << 7)) & 0xff;
            }
            else if (MS < 0)
            {
                B = -1;
            }
            // Updating NZ is complex, depending on the whether A and/or B are unknown
            set_NZ_AB(A, B);
            return -1;
        }

		static int op_ROR(em65816 em, operand_t operand, ea_t ea)
        {
            int oldC = C;
            int tmp;
            C = operand & 1;
            if (MS > 0)
            {
                // 8-bit mode
                tmp = ((operand >> 1) | (oldC << 7)) & 0xff;
                set_NZ8(tmp);
            }
            else if (MS == 0)
            {
                // 16-bit mode
                tmp = ((operand >> 1) | (oldC << 15)) & 0xffff;
                set_NZ16(tmp);
            }
            else
            {
                C = -1;
                tmp = -1;
                set_NZ_unknown();
            }
            return tmp;
        }

		static int op_RTS(em65816 em, operand_t operand, ea_t ea)
        {
            // RTS: the operand is the data pulled from the stack (PCL, PCH)
            pop8(operand);
            pop8(operand >> 8);
            // The +1 is handled elsewhere
            PC = operand & 0xffff;
            return -1;
        }

		static int op_RTI(em65816 em, operand_t operand, ea_t ea)
        {
            // RTI: the operand is the data pulled from the stack (P, PCL, PCH, PBR)
            set_FLAGS(operand);
            pop8(operand);
            pop8(operand >> 8);
            pop8(operand >> 16);
            if (E == 0)
            {
                pop8(operand >> 24);
            }
            return -1;
        }

		static int op_SBC(em65816 em, operand_t operand, ea_t ea)
        {
            int acc = get_accumulator();
            if (acc >= 0 && C >= 0)
            {
                int tmp = 0;
                if (D == 1)
                {
                    // Decimal mode SBC - works like a 65C02
                    // Working a nibble at a time, correct for both 8 and 18 bits
                    for (int bit = 0; bit < (MS ? 8 : 16); bit += 4)
                    {
                        int an = (acc >> bit) & 0xF;
                        int bn = (operand >> bit) & 0xF;
                        int rn = an - bn - (1 - C);
                        V = ((rn ^ an) & 8) && ((bn ^ an) & 8);
                        C = 1;
                        if (rn < 0)
                        {
                            rn = (rn + 10) & 0xF;
                            C = 0;
                        }
                        tmp |= rn << bit;
                    }
                }
                else
                {
                    // Normal mode SBC
                    tmp = acc - operand - (1 - C);
                    if (MS > 0)
                    {
                        // 8-bit mode
                        C = 1 - ((tmp >> 8) & 1);
                        V = (((acc ^ operand) & 0x80) != 0) && (((acc ^ tmp) & 0x80) != 0);
                    }
                    else
                    {
                        // 16-bit mode
                        C = 1 - ((tmp >> 16) & 1);
                        V = (((acc ^ operand) & 0x8000) != 0) && (((acc ^ tmp) & 0x8000) != 0);
                    }
                }
                if (MS > 0)
                {
                    // 8-bit mode
                    A = tmp & 0xff;
                }
                else
                {
                    // 16-bit mode
                    A = tmp & 0xff;
                    B = (tmp >> 8) & 0xff;
                }
                set_NZ_AB(A, B);
            }
            else
            {
                A = -1;
                B = -1;
                set_NVZC_unknown();
            }
            return -1;
        }

		static int op_SEC(em65816 em, operand_t operand, ea_t ea)
        {
            C = 1;
            return -1;
        }

		static int op_SED(em65816 em, operand_t operand, ea_t ea)
        {
            D = 1;
            return -1;
        }

		static int op_SEI(em65816 em, operand_t operand, ea_t ea)
        {
            I = 1;
            return -1;
        }

		static int op_STA(em65816 em, operand_t operand, ea_t ea)
        {
            int oplo = operand & 0xff;
            int ophi = (operand >> 8) & 0xff;
            // Always write A
            if (A >= 0)
            {
                if (oplo != A)
                {
                    failflag = 1;
                }
            }
            A = oplo;
            // Optionally write B, depending on the MS flag
            if (MS < 0)
            {
                B = -1;
            }
            else if (MS == 0)
            {
                if (B >= 0)
                {
                    if (ophi != B)
                    {
                        failflag = 1;
                    }
                }
                B = ophi;
            }
            return operand;
        }

		static int op_STX(em65816 em, operand_t operand, ea_t ea)
        {
            if (X >= 0)
            {
                if (operand != X)
                {
                    failflag = 1;
                }
            }
            X = operand;
            return operand;
        }

		static int op_STY(em65816 em, operand_t operand, ea_t ea)
        {
            if (Y >= 0)
            {
                if (operand != Y)
                {
                    failflag = 1;
                }
            }
            Y = operand;
            return operand;
        }

		static int op_STZ(em65816 em, operand_t operand, ea_t ea)
        {
            if (operand != 0)
            {
                failflag = 1;
            }
            return operand;
        }


		static int op_TSB(em65816 em, operand_t operand, ea_t ea)
        {
            int acc = get_accumulator();
            if (acc >= 0)
            {
                Z = ((acc & operand) == 0);
                return operand | acc;
            }
            else
            {
                Z = -1;
                return -1;
            }
        }

		static int op_TRB(em65816 em, operand_t operand, ea_t ea)
        {
            int acc = get_accumulator();
            if (acc >= 0)
            {
                Z = ((acc & operand) == 0);
                return operand & ~acc;
            }
            else
            {
                Z = -1;
                return -1;
            }
        }

        // This is used to implement: TAX, TAY, TSX
        void transfer_88_16(int srchi, int srclo, int* dst)
        {
            if (srclo >= 0 && srchi >= 0 && XS == 0)
            {
                // 16-bit
                *dst = (srchi << 8) + srclo;
                set_NZ16(*dst);
            }
            else if (srclo >= 0 && XS == 1)
            {
                // 8-bit
                *dst = srclo;
                set_NZ8(*dst);
            }
            else
            {
                *dst = -1;
                set_NZ_unknown();
            }
        }

        // This is used to implement: TXA, TYA
        void transfer_16_88(int src, int* dsthi, int* dstlo)
        {
            if (MS == 0)
            {
                // 16-bit
                if (src >= 0)
                {
                    *dsthi = (src >> 8) & 0xff;
                    *dstlo = src & 0xff;
                    set_NZ16(src);
                }
                else
                {
                    *dsthi = -1;
                    *dstlo = -1;
                    set_NZ_unknown();
                }
            }
            if (MS == 1)
            {
                // 8-bit
                if (src >= 0)
                {
                    *dstlo = src & 0xff;
                    set_NZ8(src);
                }
                else
                {
                    *dstlo = -1;
                    set_NZ_unknown();
                }
            }
            else
            {
                // MS undefined
                if (src >= 0)
                {
                    *dstlo = src & 0xff;
                }
                else
                {
                    *dstlo = -1;
                }
                *dsthi = -1;
                set_NZ_unknown();
            }
        }

		static int op_TAX(em65816 em, operand_t operand, ea_t ea)
        {
            transfer_88_16(B, A, &X);
            return -1;
        }

		static int op_TAY(em65816 em, operand_t operand, ea_t ea)
        {
            transfer_88_16(B, A, &Y);
            return -1;
        }

		static int op_TSX(em65816 em, operand_t operand, ea_t ea)
        {
            transfer_88_16(SH, SL, &X);
            return -1;
        }

		static int op_TXA(em65816 em, operand_t operand, ea_t ea)
        {
            transfer_16_88(X, &B, &A);
            return -1;
        }

		static int op_TXS(em65816 em, operand_t operand, ea_t ea)
        {
            if (X >= 0)
            {
                SH = (X >> 8) & 0xff;
                SL = (X) & 0xff;
            }
            else
            {
                SH = -1;
                SL = -1;
            }
            // Force SH to be 01 in emulation mode
            if (E == 1)
            {
                SH = 0x01;
            }
            return -1;
        }

		static int op_TYA(em65816 em, operand_t operand, ea_t ea)
        {
            transfer_16_88(Y, &B, &A);
            return -1;
        }

        // ====================================================================
        // Opcode Tables
        // ====================================================================


        static InstrType[] instr_table = {
      /* 00 */   new InstrType ( "BRK", AddrMode.IMM   , 7, 0, OpType.OTHER,    null),
   /* 01 */   new InstrType ( "ORA",    AddrMode.INDX  , 6, 0, OpType.READOP,   op_ORA),
   /* 02 */   new InstrType ( "COP",    AddrMode.IMM   , 7, 1, OpType.OTHER,    null),
   /* 03 */   new InstrType ( "ORA",    AddrMode.SR    , 4, 1, OpType.READOP,   op_ORA),
   /* 04 */   new InstrType ( "TSB",    AddrMode.ZP    , 5, 0, OpType.RMWOP,    op_TSB),
   /* 05 */   new InstrType ( "ORA",    AddrMode.ZP    , 3, 0, OpType.READOP,   op_ORA),
   /* 06 */   new InstrType ( "ASL",    AddrMode.ZP    , 5, 0, OpType.RMWOP,    op_ASL),
   /* 07 */   new InstrType ( "ORA",    AddrMode.IDL   , 6, 1, OpType.READOP,   op_ORA),
   /* 08 */   new InstrType ( "PHP",    AddrMode.IMP   , 3, 0, OpType.OTHER,    op_PHP),
   /* 09 */   new InstrType ( "ORA",    AddrMode.IMM   , 2, 0, OpType.OTHER,    op_ORA),
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
   /* 29 */   new InstrType( "AND", AddrMode.IMM   , 2, 0, OpType.OTHER,    op_AND),
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
   /* 42 */   new InstrType( "WDM", AddrMode.IMM   , 2, 1, OpType.OTHER,    null),
   /* 43 */   new InstrType( "EOR", AddrMode.SR    , 4, 1, OpType.READOP,   op_EOR),
   /* 44 */   new InstrType( "MVP", AddrMode.BM    , 7, 1, OpType.OTHER,    op_MVP),
   /* 45 */   new InstrType( "EOR", AddrMode.ZP    , 3, 0, OpType.READOP,   op_EOR),
   /* 46 */   new InstrType( "LSR", AddrMode.ZP    , 5, 0, OpType.RMWOP,    op_LSR),
   /* 47 */   new InstrType( "EOR", AddrMode.IDL   , 6, 1, OpType.READOP,   op_EOR),
   /* 48 */   new InstrType( "PHA", AddrMode.IMP   , 3, 0, OpType.OTHER,    op_PHA),
   /* 49 */   new InstrType( "EOR", AddrMode.IMM   , 2, 0, OpType.OTHER,    op_EOR),
   /* 4A */   new InstrType( "LSR", AddrMode.IMPA  , 2, 0, OpType.OTHER,    op_LSRA),
   /* 4B */   new InstrType( "PHK", AddrMode.IMP   , 3, 1, OpType.OTHER,    op_PHK),
   /* 4C */   new InstrType( "JMP", AddrMode.ABS   , 3, 0, OpType.OTHER,    null),
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
   /* 5C */   new InstrType(  "JML",    AddrMode.ABL   , 4, 1, OpType.OTHER,    null),
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
   /* 69 */   new InstrType(  "ADC",    AddrMode.IMM   , 2, 0, OpType.OTHER,    op_ADC),
   /* 6A */   new InstrType(  "ROR",    AddrMode.IMPA  , 2, 0, OpType.OTHER,    op_RORA),
   /* 6B */   new InstrType(  "RTL",    AddrMode.IMP   , 6, 1, OpType.OTHER,    op_RTL),
   /* 6C */   new InstrType(  "JMP",    AddrMode.IND16 , 5, 0, OpType.OTHER,    null),
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
   /* 7C */   new InstrType(  "JMP",    AddrMode.IND1X , 6, 0, OpType.OTHER,    null),
    /* 7D */   new InstrType(  "ADC",   AddrMode.ABSX  , 4, 0, OpType.READOP,   op_ADC),
   /* 7E */   new InstrType(  "ROR",    AddrMode.ABSX  , 7, 0, OpType.RMWOP,    op_ROR),
   /* 7F */   new InstrType(  "ADC",    AddrMode.ALX   , 5, 1, OpType.READOP,   op_ADC),
   /* 80 */   new InstrType(  "BRA",    AddrMode.BRA   , 3, 0, OpType.OTHER,    null),
   /* 81 */   new InstrType(  "STA",    AddrMode.INDX  , 6, 0, OpType.WRITEOP,  op_STA),
   /* 82 */   new InstrType(  "BRL",    AddrMode.BRL   , 4, 1, OpType.OTHER,    null),
   /* 83 */   new InstrType(  "STA",    AddrMode.SR    , 4, 1, OpType.WRITEOP,  op_STA),
   /* 84 */   new InstrType(  "STY",    AddrMode.ZP    , 3, 0, OpType.WRITEOP,  op_STY),
   /* 85 */   new InstrType(  "STA",    AddrMode.ZP    , 3, 0, OpType.WRITEOP,  op_STA),
   /* 86 */   new InstrType(  "STX",    AddrMode.ZP    , 3, 0, OpType.WRITEOP,  op_STX),
   /* 87 */   new InstrType(  "STA" ,   AddrMode.IDL   , 6, 1, OpType.WRITEOP,  op_STA),
   /* 88 */   new InstrType(  "DEY",    AddrMode.IMP   , 2, 0, OpType.OTHER,    op_DEY),
   /* 89 */   new InstrType(  "BIT",    AddrMode.IMM   , 2, 0, OpType.OTHER,    op_BIT_IMM),
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
   /* A0 */   new InstrType(  "LDY", AddrMode.IMM   , 2, 0, OpType.OTHER, op_LDY),
   /* A1 */   new InstrType(  "LDA", AddrMode.INDX  , 6, 0, OpType.READOP, op_LDA),
   /* A2 */   new InstrType(  "LDX", AddrMode.IMM   , 2, 0, OpType.OTHER, op_LDX),
   /* A3 */   new InstrType(  "LDA", AddrMode.SR    , 4, 1, OpType.READOP, op_LDA),
   /* A4 */   new InstrType(  "LDY", AddrMode.ZP    , 3, 0, OpType.READOP, op_LDY),
   /* A5 */   new InstrType(  "LDA", AddrMode.ZP    , 3, 0, OpType.READOP, op_LDA),
   /* A6 */   new InstrType(  "LDX", AddrMode.ZP    , 3, 0, OpType.READOP, op_LDX),
   /* A7 */   new InstrType(  "LDA", AddrMode.IDL   , 6, 1, OpType.READOP, op_LDA),
   /* A8 */   new InstrType(  "TAY", AddrMode.IMP   , 2, 0, OpType.OTHER, op_TAY),
   /* A9 */   new InstrType(  "LDA", AddrMode.IMM   , 2, 0, OpType.OTHER, op_LDA),
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
   /* C0 */   new InstrType(  "CPY", AddrMode.IMM   , 2, 0, OpType.OTHER, op_CPY),
   /* C1 */   new InstrType(  "CMP", AddrMode.INDX  , 6, 0, OpType.READOP, op_CMP),
   /* C2 */   new InstrType(  "REP", AddrMode.IMM   , 3, 1, OpType.OTHER, op_REP),
   /* C3 */   new InstrType(  "CMP", AddrMode.SR    , 4, 1, OpType.READOP, op_CMP),
   /* C4 */   new InstrType(  "CPY", AddrMode.ZP    , 3, 0, OpType.READOP, op_CPY),
   /* C5 */   new InstrType(  "CMP", AddrMode.ZP    , 3, 0, OpType.READOP, op_CMP),
   /* C6 */   new InstrType(  "DEC", AddrMode.ZP    , 5, 0, OpType.RMWOP, op_DEC),
   /* C7 */   new InstrType(  "CMP", AddrMode.IDL   , 6, 1, OpType.READOP, op_CMP),
   /* C8 */   new InstrType(  "INY", AddrMode.IMP   , 2, 0, OpType.OTHER, op_INY),
   /* C9 */   new InstrType(  "CMP", AddrMode.IMM   , 2, 0, OpType.OTHER, op_CMP),
   /* CA */   new InstrType(  "DEX", AddrMode.IMP   , 2, 0, OpType.OTHER, op_DEX),
   /* CB */   new InstrType(  "WAI", AddrMode.IMP   , 1, 1, OpType.OTHER,    null),        // WD65C02=3
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
   /* DB */   new InstrType(  "STP", AddrMode.IMP   , 1, 1, OpType.OTHER,    null),        // WD65C02=3
   /* DC */   new InstrType(  "JML", AddrMode.IAL   , 6, 1, OpType.OTHER,    null),
   /* DD */   new InstrType(  "CMP", AddrMode.ABSX  , 4, 0, OpType.READOP, op_CMP),
   /* DE */   new InstrType(  "DEC", AddrMode.ABSX  , 7, 0, OpType.RMWOP, op_DEC),
   /* DF */   new InstrType(  "CMP", AddrMode.ALX   , 5, 1, OpType.READOP, op_CMP),
   /* E0 */   new InstrType(  "CPX", AddrMode.IMM   , 2, 0, OpType.OTHER, op_CPX),
   /* E1 */   new InstrType(  "SBC", AddrMode.INDX  , 6, 0, OpType.READOP, op_SBC),
   /* E2 */   new InstrType(  "SEP", AddrMode.IMM   , 3, 1, OpType.OTHER, op_SEP),
   /* E3 */   new InstrType(  "SBC", AddrMode.SR    , 4, 1, OpType.READOP, op_SBC),
   /* E4 */   new InstrType(  "CPX", AddrMode.ZP    , 3, 0, OpType.READOP, op_CPX),
   /* E5 */   new InstrType(  "SBC", AddrMode.ZP    , 3, 0, OpType.READOP, op_SBC),
   /* E6 */   new InstrType(  "INC", AddrMode.ZP    , 5, 0, OpType.RMWOP, op_INC),
   /* E7 */   new InstrType(  "SBC", AddrMode.IDL   , 6, 1, OpType.READOP, op_SBC),
   /* E8 */   new InstrType(  "INX", AddrMode.IMP   , 2, 0, OpType.OTHER, op_INX),
   /* E9 */   new InstrType(  "SBC", AddrMode.IMM   , 2, 0, OpType.OTHER, op_SBC),
   /* EA */   new InstrType(  "NOP", AddrMode.IMP   , 2, 0, OpType.OTHER,    null),
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
