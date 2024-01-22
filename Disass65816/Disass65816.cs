
using DisassShared;
using Disass65816;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DisassX86;
using Disass68k;

namespace Disass65816
{
    //TODO: distinguish B/K relative addresses from bank 0 locked addresses (new type in Address Base or symbol type?)
    public class Disass65816 : IDisAss
    {


        AddressFactory65816 _addressFactory = new AddressFactory65816();
        public IDisassAddressFactory AddressFactory => _addressFactory;

        /// <summary>
        /// returns as +/i hex number or "" for zero
        /// </summary>
        /// <param name="x"></param>
        /// <returns></returns>
        string offs_hex(int x)
        {
            return (x == 0) ? "" : (x > 1) ? "+" + x.ToString("X") : "-" + (-x).ToString("X");
        }

        /// <summary>
        /// Special mode for BRK instruction
        /// </summary>
        /// <param name="br"></param>
        /// <param name="len"></param>
        /// <returns></returns>
        IEnumerable<DisRec2OperString_Base> mode_BRK(BinaryReader br, DisassAddressBase pc, ref ushort len, IList<string> hints)
        {
            len++;
            return OperNum(br.ReadByte(), SymbolType.ServiceCall, DisRec2_NumSize.U8);
        }

        /// <summary>
        /// Special mode for COP instruction
        /// </summary>
        /// <param name="br"></param>
        /// <param name="len"></param>
        /// <returns></returns>
        IEnumerable<DisRec2OperString_Base> mode_COP(BinaryReader br, DisassAddressBase pc, ref ushort len, IList<string> hints)
        {
            len++;
            return OperNum(br.ReadByte(), SymbolType.ServiceCall, DisRec2_NumSize.U8);
        }

        /// <summary>
        /// Special mode for WDM instruction
        /// </summary>
        /// <param name="br"></param>
        /// <param name="len"></param>
        /// <returns></returns>
        IEnumerable<DisRec2OperString_Base> mode_WDM(BinaryReader br, DisassAddressBase pc, ref ushort len, IList<string> hints)
        {
            len++;
            return OperNum(br.ReadByte(), SymbolType.ServiceCall, DisRec2_NumSize.U8);
        }

        /// <summary>
        /// 8 bit offset instruction mode
        /// </summary>
        /// <param name="br"></param>
        /// <param name="len"></param>
        /// <returns></returns>
        IEnumerable<DisRec2OperString_Base> mode_r(BinaryReader br, DisassAddressBase pc, ref ushort len, IList<string> hints)
        {
            len++;
            sbyte offs = br.ReadSByte();
            hints.Add($"*{offs_hex(offs + 2)} ");
            return OperAddr(pc + offs + 2, SymbolType.Pointer);
        }

        /// <summary>
        /// 16 bit offset instruction mode
        /// </summary>
        /// <param name="br"></param>
        /// <param name="len"></param>
        /// <returns></returns>
        IEnumerable<DisRec2OperString_Base> mode_r16(BinaryReader br, DisassAddressBase pc, ref ushort len, IList<string> hints)
        {
            len += 2;
            short offs = br.ReadInt16();
            hints.Add($"*{offs_hex(offs + 3)} ");
            return OperAddr(pc + offs + 3, SymbolType.Pointer);
        }

        IEnumerable<DisRec2OperString_Base> mode_a(BinaryReader br, DisassAddressBase pc, ref ushort len, IList<string> hints)
        {
            len += 2;
            ushort addr = br.ReadUInt16();
            return OperAddr(new Address65816(addr), SymbolType.Pointer);
        }

        IEnumerable<DisRec2OperString_Base> mode_Acc(BinaryReader br, DisassAddressBase pc, ref ushort len, IList<string> hints)
        {
            return OperStr("A");
        }


        IEnumerable<DisRec2OperString_Base> mode_ind_a(BinaryReader br, DisassAddressBase pc, ref ushort len, IList<string> hints)
        {
            len += 2;
            ushort addr = br.ReadUInt16();
            return OperStr("(").Concat(OperAddr(new Address65816(addr), SymbolType.Pointer)).Concat(OperStr(")"));
        }

        IEnumerable<DisRec2OperString_Base> mode_ind_aX(BinaryReader br, DisassAddressBase pc, ref ushort len, IList<string> hints)
        {
            len += 2;
            ushort addr = br.ReadUInt16();
            return OperStr("(").Concat(OperAddr(new Address65816(addr), SymbolType.Pointer)).Concat(OperStr(",X)"));
        }


        IEnumerable<DisRec2OperString_Base> mode_aY(BinaryReader br, DisassAddressBase pc, ref ushort len, IList<string> hints)
        {
            len += 2;
            ushort addr = br.ReadUInt16();
            return OperAddr(new Address65816(addr), SymbolType.Pointer).Concat(OperStr(",Y"));
        }

        IEnumerable<DisRec2OperString_Base> mode_aX(BinaryReader br, DisassAddressBase pc, ref ushort len, IList<string> hints)
        {
            len += 2;
            ushort addr = br.ReadUInt16();
            return OperAddr(new Address65816(addr), SymbolType.Pointer).Concat(OperStr(",X"));
        }

        IEnumerable<DisRec2OperString_Base> mode_long_a(BinaryReader br, DisassAddressBase pc, ref ushort len, IList<string> hints)
        {
            len += 3;
            UInt32 addr = (UInt32)br.ReadUInt16() + (UInt32)(br.ReadByte() >> 16);
            return OperStr("f:").Concat(OperAddr(new Address65816(addr), SymbolType.Pointer));
        }

        IEnumerable<DisRec2OperString_Base> mode_long_aX(BinaryReader br, DisassAddressBase pc, ref ushort len, IList<string> hints)
        {
            len += 3;
            UInt32 addr = (UInt32)br.ReadUInt16() + (UInt32)(br.ReadByte() >> 16);
            return OperStr("f:").Concat(OperAddr(new Address65816(addr), SymbolType.Pointer)).Concat(OperStr(",X"));
        }


        IEnumerable<DisRec2OperString_Base> mode_imm8(BinaryReader br, DisassAddressBase pc, ref ushort len, IList<string> hints)
        {
            len++;
            byte val = br.ReadByte();
            return OperStr("#").Concat(OperNum(val, SymbolType.Immediate, DisRec2_NumSize.U8));
        }

        IEnumerable<DisRec2OperString_Base> mode_xyc(BinaryReader br, DisassAddressBase pc, ref ushort len, IList<string> hints)
        {
            len += 2;
            byte val = br.ReadByte();
            byte val2 = br.ReadByte();
            return OperStr("#").Concat(OperNum(val, SymbolType.Immediate, DisRec2_NumSize.U8))
                .Concat(OperStr(", #").Concat(OperNum(val2, SymbolType.Immediate, DisRec2_NumSize.U8)));
        }


        IEnumerable<DisRec2OperString_Base> mode_ind_dpX(BinaryReader br, DisassAddressBase pc, ref ushort len, IList<string> hints)
        {
            len++;
            byte val = br.ReadByte();
            return OperStr("(<").Concat(OperAddr(new Address65816(val), SymbolType.Pointer).Concat(OperStr(",X)")));
        }

        IEnumerable<DisRec2OperString_Base> mode_ind_dpY(BinaryReader br, DisassAddressBase pc, ref ushort len, IList<string> hints)
        {
            len++;
            byte val = br.ReadByte();
            return OperStr("(<").Concat(OperAddr(new Address65816(val), SymbolType.Pointer).Concat(OperStr("),Y")));
        }
        IEnumerable<DisRec2OperString_Base> mode_ind_dp(BinaryReader br, DisassAddressBase pc, ref ushort len, IList<string> hints)
        {
            len++;
            byte val = br.ReadByte();
            return OperStr("(<").Concat(OperAddr(new Address65816(val), SymbolType.Pointer).Concat(OperStr(")")));
        }

        IEnumerable<DisRec2OperString_Base> mode_long_ind_dp(BinaryReader br, DisassAddressBase pc, ref ushort len, IList<string> hints)
        {
            len++;
            byte val = br.ReadByte();
            return OperStr("[<").Concat(OperAddr(new Address65816(val), SymbolType.Pointer).Concat(OperStr("]")));
        }

        IEnumerable<DisRec2OperString_Base> mode_long_ind_dpY(BinaryReader br, DisassAddressBase pc, ref ushort len, IList<string> hints)
        {
            len++;
            byte val = br.ReadByte();
            return OperStr("[<").Concat(OperAddr(new Address65816(val), SymbolType.Pointer).Concat(OperStr("],Y")));
        }

        IEnumerable<DisRec2OperString_Base> mode_offs_stack(BinaryReader br, DisassAddressBase pc, ref ushort len, IList<string> hints)
        {
            len++;
            byte val = br.ReadByte();
            return OperNum(val, SymbolType.Offset, DisRec2_NumSize.S8).Concat(OperStr(",S"));
        }

        IEnumerable<DisRec2OperString_Base> mode_ind_offs_stack_Y(BinaryReader br, DisassAddressBase pc, ref ushort len, IList<string> hints)
        {
            len++;
            byte val = br.ReadByte();
            return OperStr("(").Concat(OperNum(val, SymbolType.Offset, DisRec2_NumSize.S8).Concat(OperStr(",S),Y")));
        }


        IEnumerable<DisRec2OperString_Base> mode_dp(BinaryReader br, DisassAddressBase pc, ref ushort len, IList<string> hints)
        {
            len++;
            byte val = br.ReadByte();
            return OperStr("<").Concat(OperAddr(new Address65816(val), SymbolType.Pointer));
        }

        IEnumerable<DisRec2OperString_Base> mode_dpX(BinaryReader br, DisassAddressBase pc, ref ushort len, IList<string> hints)
        {
            len++;
            byte val = br.ReadByte();
            return OperStr("<").Concat(OperAddr(new Address65816(val), SymbolType.Pointer)).Concat(OperStr(",X"));
        }
        IEnumerable<DisRec2OperString_Base> mode_dpY(BinaryReader br, DisassAddressBase pc, ref ushort len, IList<string> hints)
        {
            len++;
            byte val = br.ReadByte();
            return OperStr("<").Concat(OperAddr(new Address65816(val), SymbolType.Pointer)).Concat(OperStr(",Y"));
        }

        public DisRec2<UInt32> Decode(BinaryReader br, DisassAddressBase pc)
        {

            List<string> hints = new List<string>();
            IEnumerable<DisRec2OperString_Base> operands = Enumerable.Empty<DisRec2OperString_Base>();

            DisassAddressBase start_address = pc;
            byte opcode = br.ReadByte();
            ushort len = 1;

            string opcode_s = "?";

            byte opcol = (byte)(opcode & 0x0F);
            byte oprow = (byte)(opcode >> 4);

            if (opcode == 0x44)
            {
                opcode_s = "MVP";
                operands = mode_xyc(br, pc, ref len, hints);
            }
            else if (opcode == 0x54)
            {
                opcode_s = "MVN";
                operands = mode_xyc(br, pc, ref len, hints);
            }
            else if (opcode == 0x4C)
            {
                opcode_s = "JMP";
                operands = mode_a(br, pc, ref len, hints);
            }
            else if (opcode == 0x5C)
            {
                opcode_s = "JML";
                operands = mode_long_a(br, pc, ref len, hints);
            }
            else if (opcode == 0x6C)
            {
                opcode_s = "JMP";
                operands = mode_ind_a(br, pc, ref len, hints);
            }
            else if (opcode == 0x7C)
            {
                opcode_s = "JMP";
                operands = mode_ind_aX(br, pc, ref len, hints);
            }
            else if (opcol == 0)
            {
                //col 0
                switch (opcode & 0xF0)
                {
                    case 0x00:
                        opcode_s = "BRK";
                        operands = mode_BRK(br, pc, ref len, hints);
                        break;
                    case 0x10:
                        opcode_s = "BPL";
                        operands = mode_r(br, pc, ref len, hints);
                        break;
                    case 0x20:
                        opcode_s = "JSR";
                        operands = mode_a(br, pc, ref len, hints);
                        break;
                    case 0x30:
                        opcode_s = "BMI";
                        operands = mode_r(br, pc, ref len, hints);
                        break;
                    case 0x40:
                        opcode_s = "RTI";
                        break;
                    case 0x50:
                        opcode_s = "BVC";
                        operands = mode_r(br, pc, ref len, hints);
                        break;
                    case 0x60:
                        opcode_s = "RTS";
                        break;
                    case 0x70:
                        opcode_s = "BVS";
                        operands = mode_r(br, pc, ref len, hints);
                        break;
                    case 0x80:
                        opcode_s = "BRA";
                        operands = mode_r(br, pc, ref len, hints);
                        break;
                    case 0x90:
                        opcode_s = "BCC";
                        operands = mode_r(br, pc, ref len, hints);
                        break;
                    case 0xA0:
                        opcode_s = "LDY";
                        operands = mode_imm8(br, pc, ref len, hints);
                        break;
                    case 0xB0:
                        opcode_s = "BCS";
                        operands = mode_r(br, pc, ref len, hints);
                        break;
                    case 0xC0:
                        opcode_s = "CPY";
                        operands = mode_imm8(br, pc, ref len, hints);
                        break;
                    case 0xD0:
                        opcode_s = "BNE";
                        operands = mode_r(br, pc, ref len, hints);
                        break;
                    case 0xE0:
                        opcode_s = "CPX";
                        operands = mode_imm8(br, pc, ref len, hints);
                        break;
                    case 0xF0:
                        opcode_s = "BEQ";
                        operands = mode_r(br, pc, ref len, hints);
                        break;
                }

            }
            else if ((opcol == 1)
                    || (opcol == 2 && (oprow & 1) == 1)
                    || (opcol == 3)
                    || (opcol == 5)
                    || (opcol == 7)
                    || (opcol == 9)
                    || (opcol == 0xD)
                    || (opcol == 0xF)
                )
            {
                switch (opcode >> 5)
                {
                    case 0:
                        opcode_s = "ORA";
                        break;
                    case 1:
                        opcode_s = "AND";
                        break;
                    case 2:
                        opcode_s = "EOR";
                        break;
                    case 3:
                        opcode_s = "ADC";
                        break;
                    case 4:
                        opcode_s = "STA";
                        break;
                    case 5:
                        opcode_s = "LDA";
                        break;
                    case 6:
                        opcode_s = "CMP";
                        break;
                    case 7:
                        opcode_s = "SBC";
                        break;
                }

                switch (opcode & 0x1F)
                {
                    case 0x01:
                        operands = mode_ind_dpX(br, pc, ref len, hints);
                        break;
                    case 0x03:
                        operands = mode_offs_stack(br, pc, ref len, hints);
                        break;
                    case 0x05:
                        operands = mode_dp(br, pc, ref len, hints);
                        break;
                    case 0x07:
                        operands = mode_long_ind_dp(br, pc, ref len, hints);
                        break;
                    case 0x09:
                        operands = mode_imm8(br, pc, ref len, hints);
                        break;
                    case 0x0D:
                        operands = mode_a(br, pc, ref len, hints);
                        break;
                    case 0x0F:
                        operands = mode_long_a(br, pc, ref len, hints);
                        break;
                    case 0x11:
                        operands = mode_ind_dpY(br, pc, ref len, hints);
                        break;
                    case 0x12:
                        operands = mode_ind_dp(br, pc, ref len, hints);
                        break;
                    case 0x13:
                        operands = mode_ind_offs_stack_Y(br, pc, ref len, hints);
                        break;
                    case 0x15:
                        operands = mode_dpX(br, pc, ref len, hints);
                        break;
                    case 0x17:
                        operands = mode_long_ind_dpY(br, pc, ref len, hints);
                        break;
                    case 0x19:
                        operands = mode_aY(br, pc, ref len, hints);
                        break;
                    case 0x1D:
                        operands = mode_aX(br, pc, ref len, hints);
                        break;
                    case 0x1F:
                        operands = mode_long_aX(br, pc, ref len, hints);
                        break;

                }
            }
            else if (opcol == 2 && (oprow & 1) == 0)
            {
                switch (oprow)
                {
                    case 0:
                        opcode_s = "COP";
                        operands = mode_COP(br, pc, ref len, hints);
                        break;
                    case 2:
                        opcode_s = "JSL";
                        operands = mode_long_a(br, pc, ref len, hints);
                        break;
                    case 4:
                        opcode_s = "WDM";
                        operands = mode_WDM(br, pc, ref len, hints);
                        break;
                    case 6:
                        opcode_s = "PER";
                        operands = mode_r16(br, pc, ref len, hints);
                        break;
                    case 8:
                        opcode_s = "BRL";
                        operands = mode_r16(br, pc, ref len, hints);
                        break;
                    case 0xA:
                        opcode_s = "LDX";
                        operands = mode_imm8(br, pc, ref len, hints);
                        break;
                    case 0xC:
                        opcode_s = "REP";
                        operands = mode_imm8(br, pc, ref len, hints);
                        break;
                    case 0xE:
                        opcode_s = "SEP";
                        operands = mode_imm8(br, pc, ref len, hints);
                        break;

                }
            }
            else if (opcol == 4 || opcol == 0xC)
            {
                switch (oprow)
                {
                    case 0 or 1:
                        opcode_s = (oprow == 0) ? "TSB" : "TRB";
                        operands = (opcol == 4) ? mode_dp(br, pc, ref len, hints) : mode_a(br, pc, ref len, hints);
                        break;
                    case 2 or 3 or 0xA or 0xB:
                        opcode_s = (oprow < 0xA) ? "BIT" : "LDY";
                        operands = (opcol & 8 | oprow & 1) switch
                        {
                            0 => mode_dp(br, pc, ref len, hints),
                            8 => mode_a(br, pc, ref len, hints),
                            1 => mode_dpX(br, pc, ref len, hints),
                            9 => mode_aX(br, pc, ref len, hints)
                        };
                        break;
                    case 6 or 7:
                        switch (oprow | opcol)
                        {
                            case 4 or 5:
                                opcode_s = "STZ";
                                operands = ((oprow & 1) == 0) ? mode_dp(br, pc, ref len, hints) : mode_dpX(br, pc, ref len, hints);
                                break;
                            case 0xC:
                                opcode_s = "JMP";
                                operands = mode_aX(br, pc, ref len, hints);
                                break;
                            case 0xD:
                                opcode_s = "JML";
                                operands = mode_ind_a(br, pc, ref len, hints);
                                break;
                        }
                        break;
                    case 8 or 9:
                        switch (oprow | opcol)
                        {
                            case 4 or 5:
                                opcode_s = "STY";
                                operands = ((oprow & 1) == 0) ? mode_dp(br, pc, ref len, hints) : mode_dpX(br, pc, ref len, hints);
                                break;
                            case 0xC:
                                opcode_s = "STY";
                                operands = mode_a(br, pc, ref len, hints);
                                break;
                            case 0xD:
                                opcode_s = "STZ";
                                operands = mode_a(br, pc, ref len, hints);
                                break;
                        }
                        break;
                    case 0xC or 0xE:
                        opcode_s = (oprow == 0xC) ? "CPY" : "CPX";
                        operands = (opcol == 0x4) ? mode_dp(br, pc, ref len, hints) : mode_a(br, pc, ref len, hints);
                        break;
                }
            }
            else if ((opcol == 0x6)
                || (opcol == 0xE)
                )
            {
                opcode_s = (oprow & 0xE) switch
                {
                    0 => "ASL",
                    2 => "ROL",
                    4 => "LSR",
                    6 => "ROR",
                    8 => "STX",
                    0xA => "LDX",
                    0xC => "DEC",
                    0xE => "INC"
                };

                operands = (opcol | (oprow & 1)) switch
                {
                    0x6 => mode_dp(br, pc, ref len, hints),
                    0xE => mode_a(br, pc, ref len, hints),
                    0x7 => (oprow == 8 || oprow == 0xA) ? mode_dpY(br, pc, ref len, hints):mode_dpX(br, pc, ref len, hints),
                    0xF => (oprow == 8 || oprow == 0xA) ? mode_aY(br, pc, ref len, hints):mode_aX(br, pc, ref len, hints)
                };
            }
            else if (opcol == 0x8)
            {
                opcode_s = oprow switch
                {
                    0x0 => "PHP",
                    0x1 => "CLC",
                    0x2 => "PLP",
                    0x3 => "SEC",
                    0x4 => "PHA",
                    0x5 => "CLI",
                    0x6 => "PLA",
                    0x7 => "SEI",
                    0x8 => "DEY",
                    0x9 => "TYA",
                    0xA => "TAY",
                    0xB => "CLV",
                    0xC => "INY",
                    0xD => "CLD",
                    0xE => "INX",
                    0xF => "SED"
                };
            }
            else if (opcol == 0xA)
            {
                opcode_s = oprow switch
                {
                    0x0 => "ASL",
                    0x1 => "INC",
                    0x2 => "ROL",
                    0x3 => "DEC",
                    0x4 => "LSR",
                    0x5 => "PHY",
                    0x6 => "ROR",
                    0x7 => "PLY",
                    0x8 => "TXA",
                    0x9 => "TXS",
                    0xA => "TAX",
                    0xB => "TSX",
                    0xC => "DEX",
                    0xD => "PHX",
                    0xE => "NOP",
                    0xF => "PLX"
                };

                if (oprow <= 4 || oprow == 6)
                {
                    operands = mode_Acc(br, pc, ref len, hints);
                }
            } else if (opcol == 0xB)
            {
                opcode_s = oprow switch
                {
                    0x0 => "PHD",
                    0x1 => "TCS",
                    0x2 => "PLD",
                    0x3 => "TSC",
                    0x4 => "PHK",
                    0x5 => "TCD",
                    0x6 => "RTL",
                    0x7 => "TDC",
                    0x8 => "PHB",
                    0x9 => "TXY",
                    0xA => "PLB",
                    0xB => "TYX",
                    0xC => "WAI",
                    0xD => "STP",
                    0xE => "XBA",
                    0xF => "XCE"
                };
            }


            return new DisRec2<UInt32>()
            {
                Decoded = true,
                Mnemonic = opcode_s,
                Operands = operands,
                Hints = string.Join("; ", hints),
                Length = len
            };
        }

        private static IEnumerable<DisRec2OperString_Base> OperNum(UInt32 num, SymbolType type, DisRec2_NumSize sz)
        {
            return new[] { new DisRec2OperString_Number { Number = num, SymbolType = type, Size = sz } };
        }

        private static IEnumerable<DisRec2OperString_Base> OperAddr(DisassAddressBase addr, SymbolType type)
        {
            return new[] { new DisRec2OperString_Address { Address = addr, SymbolType = type } };
        }

        private static IEnumerable<DisRec2OperString_Base> OperStr(string str)
        {
            if (str == null)
                return null;
            else
                return new[] { new DisRec2OperString_String { Text = str } };
        }

        private static IEnumerable<DisRec2OperString_Base> RotCount(byte count)
        {
            return OperStr("#").Concat(OperNum((uint)(count == 0 ? 8 : count), SymbolType.Immediate, DisRec2_NumSize.U8));
        }

    }
}
