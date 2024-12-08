
using DisassShared;
using System;
using System.Collections.Generic;
using System.Formats.Asn1;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Numerics;
using System.Reflection.Emit;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.Intrinsics.Arm;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

// compressed decode ideas from various sources
// 00   000         CIW c.addi4spn ADD Imm * 4 + SP 	        addi rd’, sp, 4*imm
// 00	010		    CL		c.lw		Load Word 		        lw rd’, (4 * imm)(rs1’)
// 00	110		    CS		c.sw		Store Word 		        sw rs1’, (4 * imm)(rs2’)
// 01	000		    CI		c.addi		ADD Immediate 		    addi rd, rd, imm
// 01	000		    CI		c.nop		No OPeration 		    addi x0, x0, 0
// 01	001		    CJ		c.jal		Jump And Link 		    jal ra, 2*offset
// 01	010		    CI		c.li		Load Immediate 		    addi rd, x0, imm
// 01	011		    CI		c.addi16sp	ADD Imm * 16 to SP 	    addi sp, sp, 16*imm
// 01	011		    CI		c.lui		Load Upper Imm 		    lui rd, imm
// 01	100011	00	CA		c.sub		SUB 			        sub rd’, rd’, rs2’
// 01	100011	01	CA		c.xor		XOR 			        xor rd’, rd’, rs2’
// 01	100011	10	CA		c.or		OR 			            or rd’, rd’, rs2’
// 01	100011	11	CA		c.and		AND 			        and rd’, rd’, rs2’
// 01	100 00		CB2		c.srli		Shift Right Logical Imm	srli rd’, rd’, imm
// 01	100 01		CB2		c.srai		Shift Right Arith Imm 	srai rd’, rd’, imm
// 01	100 10		CB2		c.andi		AND Imm 		        andi rd’, rd’, imm
// 01	101		    CJ		c.j		    Jump 			        jal x0, 2*offset
// 01	110		    CB		c.beqz		Branch == 0 		    beq rs’, x0, 2*imm
// 01	111		    CB		c.bnez		Branch != 0 		    bne rs’, x0, 2*imm
// 10	000		    CI		c.slli		Shift Left Logical Imm 	slli rd, rd, imm
// 10	010		    CI		c.lwsp		Load Word from SP 	    lw rd, (4 * imm)(sp)
// 10	1000		CR		c.jr		Jump Reg 		        jalr x0, rs1, 0
// 10	1000		CR		c.mv		MoVe 			        add rd, x0, rs2
// 10	1001		CR		c.add		ADD 			        add rd, rd, rs2
// 10	1001		CR		c.ebreak	Environment BREAK 	    ebreak
// 10	1001		CR		c.jalr		Jump And Link Reg 	    jalr ra, rs1, 0
// 10	110		    CSS		c.swsp		Store Word to SP 	    sw rs2, (4 * imm)(sp)

namespace DisassRiscV
{
    //TODO: HINTS ignored
    //TODO: partially decoded instructions should be made to return data instead of nonsense

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

        private static readonly string[] abiregsSmall = new[]
        {
            "s0", "s1", "a0", "a1", "a2", "a3", "a4", "a5"
        };

        public DisRec2<UInt32> Decode(BinaryReader br, DisassAddressBase pc, IDisassState? state = null)
        {

            byte i1 = (byte)br.ReadByte();

            if ((i1 & 0b11) == 0b11)
            {
                //32 bit instruction - TODO: longer instructions need to be decoded but for now ignore

                UInt32 instr = (UInt32)i1
                        | (UInt32)(br.ReadByte() << 8)
                        | (UInt32)(br.ReadByte() << 16)
                        | (UInt32)(br.ReadByte() << 24);

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
                UInt16 instr = (UInt16)(i1
                        | (br.ReadByte() << 8)
                        );

                if (instr == 0)
                {
                    return new DisRec2<UInt32>
                    {
                        Decoded = true,
                        Length = 2,
                        Mnemonic = "c.illegal"
                    };

                }

                switch (instr & 0b11)
                {
                    case 0b00:
                        switch ((instr & 0xE000) >> 13)
                        {
                            case 0b000: return Decode_CIW(pc, instr);
                            case 0b010: return Decode_CL(pc, instr);
                            case 0b110: return Decode_CS(pc, instr);
                        }
                        break;
                    case 0b01:
                        switch ((instr & 0xE000) >> 13)
                        {
                            case 0b000: return Decode_CI(pc, instr);
                            case 0b001: return Decode_CJ(pc, instr);
                            case 0b010: return Decode_CI(pc, instr);
                            case 0b011: return Decode_CI(pc, instr);
                            case 0b100:
                                if ((instr & 0x1C00) == 0x0C00)
                                    return Decode_CA(pc, instr);
                                else
                                    return Decode_CB2(pc, instr);
                            case 0b101: return Decode_CJ(pc, instr);
                            case 0b110: return Decode_CB(pc, instr);
                            case 0b111: return Decode_CB(pc, instr);
                        }
                        break;
                    case 0b10:
                        switch ((instr & 0xE000) >> 13)
                        {
                            case 0b000: return Decode_CI(pc, instr);
                            case 0b010: return Decode_CI(pc, instr);
                            case 0b100: return Decode_CR(pc, instr);
                            case 0b110: return Decode_CS(pc, instr);
                        }
                        break;

                    //note case 0b11 is 32 bits
                }

                return new DisRec2<UInt32>
                {
                    Decoded = false,
                    Length = 2,
                };
            }


        }

        /* SHORT instruction decoders */

        protected DisRec2<UInt32> Decode_CI(DisassAddressBase PC, UInt16 instr)
        {
            uint f3 = (uint)((instr & 0xE000) >> 13);
            uint op = (uint)(instr & 3);
            uint imm = (uint)(
                ((instr & 0x1000) >> 7) // 12->5
              | ((instr & 0x007C) >> 2) // 6:2->4:0
            );
            uint rdrs1 = (uint)((instr & 0x0F80) >> 7);

            IEnumerable<DisRec2OperString_Base>? operands = null;
            string mne = null;


            if (f3 == 0)
            {
                if (rdrs1 == 0)
                {
                    mne = "c.nop";
                    operands = new DisRec2OperString_Base[] { };
                } else
                {
                    mne = "c.addi";
                }
            }
            else if (f3 == 2 && rdrs1 != 0)
            {
                mne = "c.li";
                operands = new[] { OperReg(rdrs1), OperStr(", "), OperImmS(imm, 6) };
            }
            else if (f3 == 3)
            {
                if (rdrs1 == 2)
                {
                    mne = "c.addi16sp";
                    operands = new[] { OperReg(rdrs1), OperStr(","), OperReg(rdrs1), OperStr(", "), OperImmS(imm << 4, 10) };
                } else
                {
                    mne = "c.lui";
                    operands = new[] { OperReg(rdrs1), OperStr(", "), OperImmS(imm << 12, 18) };
                }
            }


            return new DisRec2<UInt32>
            {
                Decoded = true,
                Length = 2,
                Mnemonic = mne ?? $"c.CI{op}.[{f3}]",
                Operands = operands ?? new[] { OperReg(rdrs1), OperStr(","), OperReg(rdrs1), OperStr(", "), OperImmS(imm, 6) } 

            };
        }

        protected DisRec2<UInt32> Decode_CJ(DisassAddressBase PC, UInt16 instr)
        {

            uint f3 = (uint)((instr & 0xE000) >> 13);
            uint offs = (uint)(
                   ((instr & 0x1000) >> 1)  // 12   -> 11
                 | ((instr & 0x0800) >> 7)  // 11   -> 4
                 | ((instr & 0x0600) >> 1)  // 10:9 -> 9:8
                 | ((instr & 0x0100) << 2)  // 8    -> 10
                 | ((instr & 0x0080) >> 1)  // 7    -> 6
                 | ((instr & 0x0040) << 1)  // 6    -> 7
                 | ((instr & 0x0038) >> 2)  // 5:3  -> 3:1
                 | ((instr & 0x0004) << 3)  // 2    -> 5
                 );

            string? mne = null;

            if (f3 == 1)
                mne = "c.j";
            else if (f3 == 5)
                mne = "c.jal";

            return new DisRec2<UInt32>
            {
                Decoded = true,
                Length = 2,
                Mnemonic = mne ?? $"c.CJ{{{f3:X}}}",
                Operands = new[] { OperAddr(PC + Signed(offs, 12), SymbolType.Pointer) },
                Hints = $"{Signed(offs, 12)}"
            };
        }

        protected DisRec2<UInt32> Decode_CB(DisassAddressBase PC, UInt16 instr)
        {
            string mne = ((instr & 0x2000) != 0) ? "c.beqz" : "c.bnez";
            uint imm = (uint)(
                    ((instr & 0x1000) >> 4)
                 |  ((instr & 0x0C00) >> 7)
                 |  ((instr & 0x0060) << 1)
                 |  ((instr & 0x0018) >> 2)
                 |  ((instr & 0x0004) << 3)
                 );
            uint rs_ = (uint)((instr & 0x0380) >> 7);

            return new DisRec2<UInt32>
            {
                Decoded = true,
                Length = 2,
                Mnemonic = mne,
                Operands = new[] { OperRegSmall (rs_), OperStr(", "), OperAddr(PC + Signed(imm, 9), SymbolType.Pointer) },
                Hints = $"{Signed(imm, 9)}"
            };
        }

        protected DisRec2<UInt32> Decode_CB2(DisassAddressBase PC, UInt16 instr)
        {
            // Shift instructions / andi
            uint imm = (uint)(
                    ((instr & 0x1000) >> 7)
                |   ((instr & 0x007C) >> 2)
                );

            uint f2 = (uint)((instr & 0x0C00) >> 10);
            uint rdrs1_ = (uint)((instr & 0x0380) >> 7);

            string? mne = null;
            IEnumerable<DisRec2OperString_Base>? operands = null;

            if (f2 == 0 || f2 == 1 && (imm & 0x20) == 0 & (imm & 0x1f) != 0)
            {
                mne = (f2 == 0) ? "c.srli" : "c.srai";               
            } else if (f2 == 2)
            {
                mne = "c.andi";
            }

            return new DisRec2<UInt32>
            {
                Decoded = true,
                Length = 2,
                Mnemonic = mne ?? $"c.CB2.{{{f2:X}}}",
                Operands = operands ?? new[] { OperRegSmall(rdrs1_), OperStr(", "), OperNum(imm, SymbolType.Immediate)}
            };
        }


        protected DisRec2<UInt32> Decode_CIW(DisassAddressBase PC, UInt16 instr)
        {
            uint imm = (uint)(
                    ((instr & 0x1800) >> 7)
                 |  ((instr & 0x0780) >> 1)
                 |  ((instr & 0x0040) >> 4)
                 |  ((instr & 0x0020) >> 2)
                 );
            uint rd_ = (uint)((instr & 0x001C) >> 2);

            return new DisRec2<UInt32>
            {
                Decoded = true,
                Length = 2,
                Mnemonic = "c.addi4sp",
                Operands = new[] { OperRegSmall(rd_), OperStr(", sp, "), OperNum(imm, SymbolType.Immediate) } 
            };
        }

        protected DisRec2<UInt32> Decode_CA(DisassAddressBase PC, UInt16 instr)
        {
            uint op = (uint)(instr & 3);
            uint f6= (uint)((instr & 0xFC00) >> 10);
            uint f2 = (uint)((instr & 0x0060) >> 5);
            uint rs2_ = (uint)((instr & 0x0380) >> 7);
            uint rd_ = (uint)((instr & 0x001C) >> 2);
            string? mne = null;
            switch (f2)
            {
                case 0: mne = "c.sub"; break;
                case 1: mne = "c.xor"; break;
                case 2: mne = "c.or"; break;
                case 3: mne = "c.and"; break;
            }

            return new DisRec2<UInt32>
            {
                Decoded = true,
                Length = 2,
                Mnemonic = mne ?? $"c.CA.{op:X}[{f6:X}]{{{f2:X}}}",
                Operands = new[] { OperRegSmall(rd_), OperStr(", "), OperRegSmall(rs2_) }
            };

        }

        protected DisRec2<UInt32> Decode_CS(DisassAddressBase PC, UInt16 instr)
        {
            uint imm = (uint)((instr & 0x1C00) >> 7)
                        | (uint)((instr & 0x0040) >> 4)
                        | (uint)((instr & 0x0020) << 1)
                        ;
            uint rs1_ = (uint)((instr & 0x0380) >> 7);
            uint rd_ = (uint)((instr & 0x001C) >> 2);

            return new DisRec2<UInt32>
            {
                Decoded = true,
                Length = 2,
                Mnemonic = "c.sw",
                Operands = new[] { OperRegSmall(rd_), OperStr(", "), OperNum(imm, SymbolType.Offset), OperStr("("), OperRegSmall(rs1_), OperStr(")") }
            };
        }
        protected DisRec2<UInt32> Decode_CL(DisassAddressBase PC, UInt16 instr)
        {
            uint imm =      (uint)((instr & 0x1C00) >> 7) 
                        |   (uint)((instr & 0x0040) >> 4)
                        |   (uint)((instr & 0x0020) << 1)
                        ;
            uint rs1_ = (uint)((instr & 0x0380) >> 7);
            uint rd_ = (uint)((instr & 0x001C) >> 2);

            return new DisRec2<UInt32>
            {
                Decoded = true,
                Length = 2,
                Mnemonic = "c.lw",
                Operands = new[] { OperRegSmall(rd_), OperStr(", "), OperNum(imm, SymbolType.Offset), OperStr("("), OperRegSmall(rs1_), OperStr(")")}
            };
        }

        protected DisRec2<UInt32> Decode_CR(DisassAddressBase PC, UInt16 instr)
        {
            uint op = (uint)(instr & 3);
            uint f4 = (uint)((instr & 0xF000)>>12);
            uint rdrs1 = (uint)((instr & 0x0F80) >> 7);
            uint rs2 = (uint)((instr & 0x007C) >> 2);

            string? mne = null;
            IEnumerable<DisRec2OperString_Base>? operands = null;

            if (f4 == 0b1000)
            {
                if (rs2 == 0)
                {
                    if (rdrs1 == 1)
                    {
                        mne = "c.ret";
                        operands = new DisRec2OperString_Base[] { };
                    }
                    else
                    {
                        mne = "c.jr";
                        operands = new[] { OperReg(rdrs1) };
                    }
                } else
                {
                    mne = "c.mv";
                }
            } else if (f4 == 0b1001)
            {
                if (rdrs1 == 0 && rs2 == 0)
                {
                    mne = "c.ebreak";
                    operands = new DisRec2OperString_Base[] { };
                }
                else if (rs2 == 0)
                {
                    mne = "c.jalr";
                    operands = new[] { OperReg(rdrs1) };
                }
                else
                {
                    mne = "c.add";
                    operands = new[] { OperReg(rdrs1), OperStr(", "), OperReg(rdrs1), OperStr(", "), OperReg(rs2) };
                }

            }

            return new DisRec2<UInt32>
            {
                Decoded = true,
                Length = 2,
                Mnemonic = mne ?? $"c.CR{op:X}[{f4:X}]",
                Operands = operands ?? new[] { OperReg(rdrs1), OperStr(", "), OperReg(rs2) }
                
            };
        }


        /* LONG instruction decoders */

        protected DisRec2<UInt32> Decode_R(uint f7, uint rs2, uint rs1, uint f3, uint rd, uint opcode)
        {
            string? mne = null;
            IEnumerable<DisRec2OperString_Base>? operands = null;

            if (f3 == 0 && f7 == 0) mne = "add";
            else if (f3 == 0 && f7 == 0x20) mne = "sub";
            else if (f3 == 1 && f7 == 0) mne = "sll";
            else if (f3 == 2 && f7 == 0) mne = "slt";
            else if (f3 == 3 && f7 == 0) mne = "sltu";
            else if (f3 == 4 && f7 == 0) mne = "xor";
            else if (f3 == 5 && f7 == 0) mne = "srl";
            else if (f3 == 5 && f7 == 0x20) mne = "sra";
            else if (f3 == 6 && f7 == 0) mne = "or";
            else if (f3 == 7 && f7 == 0) mne = "and";


            return new DisRec2<UInt32>
            {
                Decoded = true,
                Length = 4,
                Mnemonic = mne ?? $"R.{opcode:X}[{f3:X}]{{{f7:X}}}",
                Operands = operands ?? new[] { OperReg(rd), OperStr(", "), OperReg(rs1), OperStr(", "), OperReg(rs2) }
            };

        }

        protected DisRec2<UInt32> Decode_U(DisassAddressBase PC, uint imm, uint rd, uint opcode)
        {
            string? mne = null;
            IEnumerable<DisRec2OperString_Base>? operands = null;

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
            IEnumerable<DisRec2OperString_Base>? operands = null;

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
            IEnumerable<DisRec2OperString_Base>? operands = null;

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
            IEnumerable<DisRec2OperString_Base>? operands = null;
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

        private static DisRec2OperString_Base OperRegSmall(uint r)
        {
            if (r < abiregsSmall.Length)
                return new DisRec2OperString_String { Text = abiregsSmall[r] };
            else
                return new DisRec2OperString_String { Text = $"x?{r}" };
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
