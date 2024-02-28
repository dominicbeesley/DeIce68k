using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Disass65816.Emulate
{
    public static class RegsEmuExtensions
    {
        static public bool compare_FLAGS(this IRegsEmu65816 regs, int operand)
        {
            if (regs.N != (((operand >> 7) & 1) != 0)) return true;
            if (regs.V != (((operand >> 6) & 1) != 0)) return true;
            if (regs.E == Tristate.False)
            {
                if (regs.MS != (((operand >> 5) & 1) != 0)) return true;
                if (regs.XS != (((operand >> 4) & 1) != 0)) return true;
            }
            if (regs.D != (((operand >> 3) & 1) != 0)) return true;
            if (regs.I != (((operand >> 2) & 1) != 0)) return true;
            if (regs.Z != (((operand >> 1) & 1) != 0)) return true;
            if (regs.C != (((operand >> 0) & 1) != 0)) return true;
            return false;
        }

        public static void set_FLAGS(this IRegsEmu65816 regs, int operand)
        {
            regs.N = ((operand >> 7) & 1) != 0;
            regs.V = ((operand >> 6) & 1) != 0;
            if (regs.E == Tristate.False)
            {
                regs.MS = ((operand >> 5) & 1) != 0;
                regs.XS = ((operand >> 4) & 1) != 0;
            }
            else
            {
                regs.MS = Tristate.True;
                regs.XS = Tristate.True;
            }
            regs.D = ((operand >> 3) & 1) != 0;
            regs.I = ((operand >> 2) & 1) != 0;
            regs.Z = ((operand >> 1) & 1) != 0;
            regs.C = ((operand >> 0) & 1) != 0;
        }

        public static int get_FLAGS(this IRegsEmu65816 regs)
        {
            var ret = 0;
            if (regs.N.IsUnknown) return -1; else ret |= regs.N == Tristate.True ? 0x80 : 0;
            if (regs.V.IsUnknown) return -1; else ret |= regs.V == Tristate.True ? 0x40 : 0;

            if (regs.D.IsUnknown) return -1; else ret |= regs.D == Tristate.True ? 0x8 : 0;
            if (regs.I.IsUnknown) return -1; else ret |= regs.I == Tristate.True ? 0x4 : 0;
            if (regs.Z.IsUnknown) return -1; else ret |= regs.Z == Tristate.True ? 0x2 : 0;
            if (regs.C.IsUnknown) return -1; else ret |= regs.C == Tristate.True ? 0x1 : 0;

            if (regs.E == Tristate.False)
            {
                if (regs.MS.IsUnknown) return -1; else ret |= regs.MS == Tristate.True ? 0x20 : 0;
                if (regs.XS.IsUnknown) return -1; else ret |= regs.XS == Tristate.True ? 0x10 : 0;

            }
            else if (regs.E == Tristate.True)
                ret |= 0x30;
            else
                ret = -1;

            return ret;
        }

        public static void set_NZ_unknown(this IRegsEmu65816 regs)
        {
            regs.N = Tristate.Unknown;
            regs.Z = Tristate.Unknown;
        }

        public static void set_NZC_unknown(this IRegsEmu65816 regs)
        {
            regs.N = Tristate.Unknown;
            regs.Z = Tristate.Unknown;
            regs.C = Tristate.Unknown;
        }

        public static void set_NVZC_unknown(this IRegsEmu65816 regs)
        {
            regs.N = Tristate.Unknown;
            regs.V = Tristate.Unknown;
            regs.Z = Tristate.Unknown;
            regs.C = Tristate.Unknown;
        }

        public static void set_NZ8(this IRegsEmu65816 regs, int value)
        {
            regs.N = ((value >> 7) & 1) != 0;
            regs.Z = ((value & 0xff) == 0 ? 1 : 0) != 0;
        }

        public static void set_NZ16(this IRegsEmu65816 regs, int value)
        {
            regs.N = ((value >> 15) & 1) != 0;
            regs.Z = ((value & 0xffff) == 0 ? 1 : 0) != 0;
        }

        public static void set_NZ_unknown_width(this IRegsEmu65816 regs, int value)
        {
            // Don't know which bit is the sign bit
            Tristate s15 = ((value >> 15) & 1) != 0;
            Tristate s7 = ((value >> 7) & 1) != 0;
            if (s7 == s15)
            {
                // both choices of sign bit are the same
                regs.N = s7;
            }
            else
            {
                // possible sign bits differ, so regs.N must become undefined
                regs.N = Tristate.Unknown;
            }
            // Don't know how many bits to check for any ones
            if ((value & 0xff00) == 0)
            {
                // no high bits set, so base regs.Z on the low bits
                regs.Z = ((value & 0xff) == 0);
            }
            else
            {
                // some high bits set, so regs.Z must become undefined
                regs.Z = Tristate.Unknown;
            }
        }

        public static void set_NZ_XS(this IRegsEmu65816 regs, int value)
        {
            if (regs.XS.IsUnknown)
            {
                regs.set_NZ_unknown_width(value);
            }
            else if (regs.XS == Tristate.False)
            {
                regs.set_NZ16(value);
            }
            else
            {
                regs.set_NZ8(value);
            }
        }

        public static void set_NZ_MS(this IRegsEmu65816 regs, int value)
        {
            if (regs.MS.IsUnknown)
            {
                regs.set_NZ_unknown_width(value);
            }
            else if (regs.MS == Tristate.False)
            {
                regs.set_NZ16(value);
            }
            else
            {
                regs.set_NZ8(value);
            }
        }

        public static void set_NZ_AB(this IRegsEmu65816 regs)
        {
            if (regs.MS == Tristate.True)
            {
                // 8-bit
                if (regs.A >= 0)
                {
                    regs.set_NZ8(regs.A);
                }
                else
                {
                    regs.set_NZ_unknown();
                }
            }
            else if (regs.MS == Tristate.False)
            {
                // 16-bit
                if (regs.A >= 0 && regs.B >= 0)
                {
                    regs.set_NZ16((regs.B << 8) + regs.A);
                }
                else
                {
                    // TODO: the behaviour when regs.A is known and regs.B is unknown could be improved
                    regs.set_NZ_unknown();
                }
            }
            else
            {
                // width unknown
                if (regs.A >= 0 && regs.B >= 0)
                {
                    regs.set_NZ_unknown_width((regs.B << 8) + regs.A);
                }
                else
                {
                    regs.set_NZ_unknown();
                }
            }
        }

        // ====================================================================
        // Helper Methods
        // ====================================================================

        public static int memory_read16(this IRegsEmu65816 regs, int ea)
        {
            if (ea < 0) return -1;

            var l = regs.memory_read(ea);
            if (l < 0) return -1;

            var h = regs.memory_read(ea + 1);
            if (h < 0) return -1;

            return (l & 0xFF) | ((h & 0xFF) << 8);

        }
        public static void memory_write16(this IRegsEmu65816 regs, int value, int ea)
        {
            if (ea < 0 || value < 0) return;

            regs.memory_write(value & 0xFF, ea);
            regs.memory_write((value & 0xFF00) >> 8, ea);
        }

        public static int memory_read24(this IRegsEmu65816 regs, int ea)
        {
            if (ea < 0) return -1;

            var l = regs.memory_read(ea);
            if (l < 0) return -1;

            var h = regs.memory_read(ea);
            if (h < 0) return -1;

            var b = regs.memory_read(ea);
            if (b < 0) return -1;

            return (l & 0xFF) | ((h & 0xFF) << 8) | ((b & 0xFF) << 16);

        }

        public static int memory_read_MS(this IRegsEmu65816 regs, int ea, Tristate size)
        {
            return (size.IsUnknown) ? -1 : (size == Tristate.True) ? regs.memory_read(ea) : regs.memory_read16(ea);
        }

        public static void memory_write_MS(this IRegsEmu65816 regs, int value, int ea, Tristate size)
        {
            if (size == Tristate.False)
                //16 bit
                regs.memory_write16(value, ea);
            else
                regs.memory_write(value, ea);
        }


        // TODO: Stack wrapping im emulation mode should only happen with "old" instructions
        // e.g. PLB should not wrap
        // See appendix of 65C816 Opcodes by Bruce Clark

        public static int pop8(this IRegsEmu65816 regs)
        {
            // Increment the low byte of SP
            if (regs.SL >= 0)
            {
                regs.SL = (regs.SL + 1) & 0xff;
            }
            // Increment the high byte of SP, in certain cases
            if (regs.E == Tristate.True)
            {
                // In emulation mode, force regs.SH to 1
                regs.SH = 1;
            }
            else if (regs.E == Tristate.False)
            {
                // In native mode, increment regs.SH if regs.SL has wrapped to 0
                if (regs.SH >= 0)
                {
                    if (regs.SL < 0)
                    {
                        regs.SH = -1;
                    }
                    else if (regs.SL == 0)
                    {
                        regs.SH = (regs.SH + 1) & 0xff;
                    }
                }
            }
            else
            {
                regs.SH = -1;
            }
            // Handle the memory access
            if (regs.SL >= 0 && regs.SH >= 0)
            {
                var r = regs.memory_read((regs.SH << 8) + regs.SL);
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

        public static void push8(this IRegsEmu65816 regs, int value)
        {
            // Handle the memory access
            if (regs.SL >= 0 && regs.SH >= 0)
            {
                regs.memory_write(value & 0xFF, (regs.SH << 8) + regs.SL);
            }
            // Decrement the low byte of SP
            if (regs.SL >= 0)
            {
                regs.SL = (regs.SL - 1) & 0xff;
            }
            // Decrement the high byte of SP, in certain cases
            if (regs.E == Tristate.True)
            {
                // In emulation mode, force regs.SH to 1
                regs.SH = 1;
            }
            else if (regs.E == Tristate.False)
            {
                // In native mode, increment regs.SH if regs.SL has wrapped to 0
                if (regs.SH >= 0)
                {
                    if (regs.SL < 0)
                    {
                        regs.SH = -1;
                    }
                    else if (regs.SL == 0xff)
                    {
                        regs.SH = (regs.SH - 1) & 0xff;
                    }
                }
            }
            else
            {
                regs.SH = -1;
            }
        }

        public static int pop16(this IRegsEmu65816 regs)
        {
            int l = regs.pop8();
            int h = regs.pop8();

            if (l >= 0 && h >= 0)
                return l + (h << 8);
            else
                return -1;
        }

        public static void push16(this IRegsEmu65816 regs, int value)
        {
            if (value < 0)
            {
                regs.push8(-1);
                regs.push8(-1);
            }
            else
            {
                regs.push8(value >> 8);
                regs.push8(value);
            }
        }

        public static int popXS(this IRegsEmu65816 regs)
        {
            if (regs.XS.IsUnknown)
            {
                regs.SL = -1;
                regs.SH = -1;
                return -1;
            }
            else if (regs.XS == Tristate.False)
            {
                return regs.pop16();
            }
            else
            {
                return regs.pop8();
            }
        }

        public static int popMS(this IRegsEmu65816 regs)
        {
            if (regs.MS.IsUnknown)
            {
                regs.SL = -1;
                regs.SH = -1;
                return -1;
            }
            else if (regs.MS == Tristate.False)
            {
                return regs.pop16();
            }
            else
            {
                return regs.pop8();
            }
        }

        public static void pushXS(this IRegsEmu65816 regs, int value)
        {
            if (regs.XS.IsUnknown)
            {
                regs.SL = -1;
                regs.SH = -1;
            }
            else if (regs.XS == Tristate.False)
            {
                regs.push16(value);
            }
            else
            {
                regs.push8(value);
            }
        }

        public static void pushMS(this IRegsEmu65816 regs, int value)
        {
            if (regs.MS.IsUnknown)
            {
                regs.SL = -1;
                regs.SH = -1;
            }
            else if (regs.MS == Tristate.False)
            {
                regs.push16(value);
            }
            else
            {
                regs.push8(value);
            }
        }



        // regs.A set of actions to take if emulation mode enabled
        public static void emulation_mode_on(this IRegsEmu65816 regs)
        {
            regs.MS = true;
            regs.XS = true;
            if (regs.X >= 0)
            {
                regs.X &= 0x00ff;
            }
            if (regs.Y >= 0)
            {
                regs.Y &= 0x00ff;
            }
            regs.SH = 0x01;
            regs.E = true;
        }

        // regs.A set of actions to take if emulation mode enabled
        public static void emulation_mode_off(this IRegsEmu65816 regs)
        {
            regs.E = false;
        }

        // Helper to return the variable size accumulator
        public static int get_accumulator(this IRegsEmu65816 regs)
        {
            if (regs.MS == true)
            {
                // 8-bit mode
                return regs.A;
            }
            else if (regs.MS == false && regs.A >= 0 && regs.B >= 0)
            {
                // 16-bit mode
                return (regs.B << 8) + regs.A;
            }
            else
            {
                // unknown width
                return -1;
            }
        }

        public static void repsep(this IRegsEmu65816 regs, int operand, Tristate val)
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


    }
}
