
using DisassShared;
using System;
using System.Collections.Generic;
using System.Formats.Asn1;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Numerics;
using System.Reflection.Emit;
using System.Runtime.Intrinsics.Arm;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace DisassRiscV
{
    public class DisassRiscV : IDisAss
    {
        StateFactoryRiscV _stateFactory = new StateFactoryRiscV();
        public IDisassStateFactory StateFactory => _stateFactory;


        private static DisRec2<UInt32> Undefined { get => new DisRec2<UInt32> { Decoded = false, Length = 4 }; }

        public static AddressFactoryRiscV _addressFactory = new AddressFactoryRiscV();

        public IDisassAddressFactory AddressFactory => _addressFactory;

        private static readonly string[] abiregs = new[]
        {"zero","ra","sp","gp","tp","t0","t1","t2","s0","s1","a0","a1","a2","a3","a4","a5","a6","a7","s2","s3","s4","s5","s6","s7","s8","s9","s10","s11","t3","t4","t5","t6"
        };

        public DisRec2<UInt32> Decode(BinaryReader br, DisassAddressBase pc, IDisassState? state = null)
        {

            UInt32 instr = br.ReadUInt32();


            if ((instr & 0b11) == 0b11)
            {
                //32 bits instruction
                var opcode = instr & 0x7F;

                switch (opcode)
                {
                    case 0b0110011:
                        return Decode_R(
                            (instr & 0xFE000000) >> 25,
                            (instr & 0x01F00000) >> 20,
                            (instr & 0x000F8000) >> 15,
                            (instr & 0x00007000) >> 12,
                            (instr & 0x00000F80) >> 7,
                            opcode);
                    case 0b0010011:
                    case 0b1100111:
                    case 0b0000011:
                        return Decode_I(
                            (instr & 0xFFF00000) >> 20,
                            (instr & 0x000F8000) >> 15,
                            (instr & 0x00007000) >> 12,
                            (instr & 0x00000F80) >> 7,
                            opcode);
                    case 0b0100011:
                        return Decode_S(
                            ((instr & 0xFE000000) >> 20) | ((instr & 0x00000F80) >> 7),
                            (instr & 0x01F00000) >> 20,
                            (instr & 0x000F8000) >> 15,
                            (instr & 0x00007000) >> 12,
                            opcode);

                    case 0b1100011:
                        return Decode_B(
                            pc,
                            (
                                ((instr & 0x80000000) >> 19) | // 12
                                ((instr & 0x7E000000) >> 20) | // 10:5
                                ((instr & 0x00000F00) >> 7) | // 4:1
                                ((instr & 0x00000080) << 4) // 11
                            ),
                            (instr & 0x01F00000) >> 20,
                            (instr & 0x000F8000) >> 15,
                            (instr & 0x00007000) >> 12,
                            opcode);
                    case 0b1101111:
                        return Decode_J(
                            pc,
                            (
                                ((instr & 0x80000000) >> 11) | // 20
                                ((instr & 0x7FE00000) >> 20) | // 10:1
                                ((instr & 0x00100000) >> 9) | // 11
                                ((instr & 0x000FF000)) // 19:12
                            ),
                            (instr & 0x00000F80) >> 7,
                            opcode);
                    case 0b0110111:
                    case 0b0010111:
                        return Decode_U(
                            pc, 
                            (instr & 0xFFFFF000),
                            (instr & 0x00000F80) >> 7,
                            opcode);
                    default:
                        return new DisRec2<UInt32>
                        {
                            Decoded = false,
                            Length = 4
                        };

                }

            }
            else
            {
                //compressed?
                return new DisRec2<UInt32>
                {
                    Decoded = false,
                    Length = 2
                };
            }


        }

        protected DisRec2<UInt32> Decode_R(uint f7, uint rs2, uint rs1, uint f3, uint rd, uint opcode)
        {
            return new DisRec2<UInt32>
            {
                Decoded = true,
                Length = 4,
                Mnemonic = $"R.{opcode:X}[{f3:X}]{{{f7:X}}}",
                Operands = new[] { OperReg(rd), OperStr(", "), OperReg(rs1), OperStr(", "), OperReg(rs2) }
            };

        }

        protected DisRec2<UInt32> Decode_U(DisassAddressBase PC, uint imm, uint rd, uint opcode)
        {
            string? mne = null;
            IEnumerable<DisRec2OperString_Base> operands = null;

            if (opcode == 0b0110111)
            {
                mne = "lui";
            }
            else if (opcode == 0b0010111)
            {
                mne = "auipc";
                operands = new[] { OperReg(rd), OperStr(", "), OperAddr(PC + Signed(imm, 32), SymbolType.Pointer) };
            }

            return new DisRec2<UInt32>
            {
                Decoded = true,
                Length = 4,
                Mnemonic = mne ?? $"U.{opcode:X}",
                Operands = operands ?? new[] { OperReg(rd), OperStr(", "), OperImmS(imm,32) },
                Hints = $"{Signed(imm, 12)}"
            };
        }

        protected DisRec2<UInt32> Decode_B(DisassAddressBase PC, uint imm, uint rs2, uint rs1, uint f3, uint opcode)
        {

            string? mne = null;
            IEnumerable<DisRec2OperString_Base> operands = null;

            if (opcode == 0b1100011)
            {
                //TODO: special mnemonics for zeros
                switch(f3)
                {
                    case 0: mne = "beq"; break;
                    case 1: mne = "bne"; break;
                    case 4: mne = "blt"; break;
                    case 5: mne = "bge"; break;
                    case 6: mne = "bltu"; break;
                    case 7: mne = "bgeu"; break;
                }
            }

            return new DisRec2<UInt32>
            {
                Decoded = true,
                Length = 4,
                Mnemonic = mne ?? $"B.{opcode}[{f3}]",
                Operands = operands ?? new[] { OperReg(rs1), OperStr(", "), OperReg(rs2), OperStr(", "), OperAddr(PC + Signed(imm, 12), SymbolType.Pointer) },
                Hints = $"{Signed(imm, 12)}"
            };
        }


        protected DisRec2<UInt32> Decode_J(DisassAddressBase PC, uint imm, uint rd, uint opcode)
        {
            string? mne = null;
            IEnumerable<DisRec2OperString_Base> operands = null;

            if (opcode == 0b1101111)
            {
                if (rd == 0) 
                {
                    mne = "j";
                    operands = new[] { OperAddr(PC + Signed(imm, 20), SymbolType.Pointer) };
                } else
                {
                    mne = "jal";
                }
            }

            return new DisRec2<UInt32>
            {
                Decoded = true,
                Length = 4,
                Mnemonic = mne ?? $"J.{opcode:X}",
                Operands = operands ?? new[] { OperReg(rd), OperStr(", "), OperAddr( PC + Signed(imm,20), SymbolType.Pointer) }
            };
        }


        protected DisRec2<UInt32> Decode_S(uint imm, uint rs2, uint rs1, uint f3, uint opcode)
        {
            string? mne = null;
            IEnumerable<DisRec2OperString_Base> operands = null;
            switch (opcode)
            {
                case 0b0100011:
                    switch (f3)
                    {
                        case 0:mne = "sb"; break;
                        case 1: mne = "sh"; break;
                        case 2: mne = "sw"; break;
                    }
                    break;

            }

            return new DisRec2<UInt32>
            {
                Decoded = true,
                Length = 4,
                Mnemonic = mne ?? $"S.{opcode:X}[{f3:X}]?",
                Operands = new[] { OperReg(rs2), OperStr(", "), OperOffsS(imm, 12), OperStr("("), OperReg(rs1), OperStr(")") },
                Hints = $"{Signed(imm, 12)}"
            };

        }


        protected DisRec2<UInt32> Decode_I(uint imm, uint rs1, uint f3, uint rd, uint opcode)
        {

            string? mne=null;
            IEnumerable<DisRec2OperString_Base> operands = null;
            switch (opcode)
            {
                case 0b0010011:
                    switch (f3)
                    {
                        case 0: mne = "addi"; break;
                        case 4: mne = "xori"; break;
                        case 6: mne = "ori"; break;
                        case 7: mne = "andi"; break;
                        case 1: if ((imm & 0xFE0) == 0) mne = "slli"; imm = imm & 0x1F; break;
                        case 5:
                            if ((imm & 0xFE0) == 0) { mne = "srli"; imm = imm & 0x1F; }
                            else if ((imm & 0xFE0) == 0x400) { mne = "srai"; imm = imm & 0x1F; } 
                            break;
                        case 2: mne = "slti"; break;
                        case 3: mne = "sltiu"; break;
                    }


                    operands = new[] { OperReg(rd), OperStr(", "), OperReg(rs1), OperStr(", "), OperImmS(imm, 12) };
                    break;
                case 0b000011:
                    switch (f3)
                    {
                        case 0: mne = "lb"; break;
                        case 1: mne = "lh"; break;
                        case 2: mne = "lw"; break;
                        case 4: mne = "lbu"; break;
                        case 5: mne = "lhu"; break;
                    }

                    operands = new[] { OperReg(rd), OperStr(", "), OperOffsS(imm, 12), OperStr("("), OperReg(rs1), OperStr(")") };
                    break;
                case 0b1100111:
                    switch (f3) { 
                        case 0:
                            if (rd == 0 && imm == 0 && rs1 == 1)
                            {
                                mne = "ret";
                            }
                            else
                            {
                                mne = "jalr";
                                operands = new[] { OperReg(rd), OperStr(", "), OperOffsS(imm, 12), OperStr("("), OperReg(rs1), OperStr(")") };
                            }
                            break;
                    }
                    break;
            }

            return new DisRec2<UInt32>
            {
                Decoded = true,
                Length = 4,
                Mnemonic = mne ?? $"I.{opcode:X}[{f3:X}]?",
                Operands = operands ?? new[] { OperReg(rd), OperStr(", "), OperReg(rs1), OperStr(", "), OperImmS(imm, 12) }
        };

        }

        private static DisRec2OperString_Base OperNum(UInt32 num, SymbolType type)
        {
            return new DisRec2OperString_Number { Number = num, SymbolType = type } ;
        }
        private static DisRec2OperString_Base OperAddr(DisassAddressBase addr, SymbolType type)
        {
            return new DisRec2OperString_Address { Address = addr, SymbolType = type };
        }

        private static DisRec2OperString_Base OperStr(string str)
        {
            return new DisRec2OperString_String { Text = str ?? "" } ;
        }

        private static DisRec2OperString_Base OperReg(uint r)
        {
            if (r < abiregs.Length)
                return new DisRec2OperString_String { Text = abiregs[r] };
            else
                return new DisRec2OperString_String { Text = $"x{r}" };
        }

        private static DisRec2OperString_Base OperImmS(uint i, int bits)
        {

            return new DisRec2OperString_Number { Number = (ulong)(long)Signed(i, bits), SymbolType = SymbolType.Immediate, Size = DisRec2_NumSize.S32 };
        }

        private static DisRec2OperString_Base OperOffsS(uint i, int bits)
        {

            return new DisRec2OperString_Number { Number = (ulong)(long)Signed(i, bits), SymbolType = SymbolType.Offset, Size = DisRec2_NumSize.S32 };
        }

        private static long Signed(uint i, int nbits)
        {
            int sb = 1 << (nbits - 1);
            uint m = 0xFFFFFFFF << nbits;
            return (int)(((i & sb) != 0) ? (m | i) : i);

        }

    }

}
