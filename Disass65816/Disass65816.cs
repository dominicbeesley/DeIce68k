
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
using System.Net.NetworkInformation;

namespace Disass65816
{
    //TODO: distinguish B/K relative addresses from bank 0 locked addresses (new type in Address Base or symbol type?)
    public class Disass65816 : IDisAss
    {

        StateFactory65816 _stateFactory = new StateFactory65816();
        public IDisassStateFactory StateFactory => _stateFactory;

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
        IEnumerable<DisRec2OperString_Base> mode_BRK(BinaryReader br, DisassAddressBase pc, ref ushort len, IList<string> hints, DisassState65816 state)
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
        IEnumerable<DisRec2OperString_Base> mode_COP(BinaryReader br, DisassAddressBase pc, ref ushort len, IList<string> hints, DisassState65816 state)
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
        IEnumerable<DisRec2OperString_Base> mode_WDM(BinaryReader br, DisassAddressBase pc, ref ushort len, IList<string> hints, DisassState65816 state)
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
        IEnumerable<DisRec2OperString_Base> mode_r(BinaryReader br, DisassAddressBase pc, ref ushort len, IList<string> hints, DisassState65816 state)
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
        IEnumerable<DisRec2OperString_Base> mode_r16(BinaryReader br, DisassAddressBase pc, ref ushort len, IList<string> hints, DisassState65816 state)
        {
            len += 2;
            short offs = br.ReadInt16();
            hints.Add($"*{offs_hex(offs + 3)} ");
            return OperAddr(pc + offs + 3, SymbolType.Pointer);
        }

        IEnumerable<DisRec2OperString_Base> mode_a(BinaryReader br, DisassAddressBase pc, ref ushort len, IList<string> hints, DisassState65816 state)
        {
            len += 2;
            ushort addr = br.ReadUInt16();
            return OperAddr(new Address65816_abs(addr), SymbolType.Pointer);
        }

        IEnumerable<DisRec2OperString_Base> mode_Acc(BinaryReader br, DisassAddressBase pc, ref ushort len, IList<string> hints, DisassState65816 state)
        {
            return OperStr("A");
        }


        IEnumerable<DisRec2OperString_Base> mode_ind_a(BinaryReader br, DisassAddressBase pc, ref ushort len, IList<string> hints, DisassState65816 state)
        {
            len += 2;
            ushort addr = br.ReadUInt16();
            return OperStr("(").Concat(OperAddr(new Address65816_abs(addr), SymbolType.Pointer)).Concat(OperStr(")"));
        }

        IEnumerable<DisRec2OperString_Base> mode_ind_aX(BinaryReader br, DisassAddressBase pc, ref ushort len, IList<string> hints, DisassState65816 state)
        {
            len += 2;
            ushort addr = br.ReadUInt16();
            return OperStr("(").Concat(OperAddr(new Address65816_abs(addr), SymbolType.Pointer)).Concat(OperStr(",X)"));
        }


        IEnumerable<DisRec2OperString_Base> mode_aY(BinaryReader br, DisassAddressBase pc, ref ushort len, IList<string> hints, DisassState65816 state)
        {
            len += 2;
            ushort addr = br.ReadUInt16();
            return OperAddr(new Address65816_abs(addr), SymbolType.Pointer).Concat(OperStr(",Y"));
        }

        IEnumerable<DisRec2OperString_Base> mode_aX(BinaryReader br, DisassAddressBase pc, ref ushort len, IList<string> hints, DisassState65816 state)
        {
            len += 2;
            ushort addr = br.ReadUInt16();
            return OperAddr(new Address65816_abs(addr), SymbolType.Pointer).Concat(OperStr(",X"));
        }

        IEnumerable<DisRec2OperString_Base> mode_long_a(BinaryReader br, DisassAddressBase pc, ref ushort len, IList<string> hints, DisassState65816 state)
        {
            len += 3;
            UInt32 addr = (UInt32)br.ReadUInt16() + (UInt32)(br.ReadByte() >> 16);
            return OperStr("f:").Concat(OperAddr(new Address65816_far(addr), SymbolType.Pointer));
        }

        IEnumerable<DisRec2OperString_Base> mode_long_aX(BinaryReader br, DisassAddressBase pc, ref ushort len, IList<string> hints, DisassState65816 state)
        {
            len += 3;
            UInt32 addr = (UInt32)br.ReadUInt16() + (UInt32)(br.ReadByte() >> 16);
            return OperStr("f:").Concat(OperAddr(new Address65816_far(addr), SymbolType.Pointer)).Concat(OperStr(",X"));
        }

        IEnumerable<DisRec2OperString_Base> mode_imm8(BinaryReader br, DisassAddressBase pc, ref ushort len, IList<string> hints, DisassState65816 state)
        {
            len++;
            byte val = br.ReadByte();
            return OperStr("#").Concat(OperNum(val, SymbolType.Immediate, DisRec2_NumSize.U8));
        }

        IEnumerable<DisRec2OperString_Base> mode_immRep(BinaryReader br, DisassAddressBase pc, ref ushort len, IList<string> hints, DisassState65816 state)
        {
            len++;
            byte val = br.ReadByte();
            /******** SIDE EFFECTS *************/
            if ((val & 0x10) != 0) state.RegSizeX8 = false;
            if ((val & 0x20) != 0) state.RegSizeM8 = false;

            return OperStr("#").Concat(OperNum(val, SymbolType.Immediate, DisRec2_NumSize.U8));
        }

        IEnumerable<DisRec2OperString_Base> mode_immSep(BinaryReader br, DisassAddressBase pc, ref ushort len, IList<string> hints, DisassState65816 state)
        {
            len++;
            byte val = br.ReadByte();
            /******** SIDE EFFECTS *************/
            if ((val & 0x10) != 0) state.RegSizeX8 = true;
            if ((val & 0x20) != 0) state.RegSizeM8 = true;
            return OperStr("#").Concat(OperNum(val, SymbolType.Immediate, DisRec2_NumSize.U8));
        }


        IEnumerable<DisRec2OperString_Base> mode_imm16(BinaryReader br, DisassAddressBase pc, ref ushort len, IList<string> hints, DisassState65816 state)
        {
            len+=2;
            ushort val = br.ReadUInt16();
            return OperStr("#").Concat(OperNum(val, SymbolType.Immediate, DisRec2_NumSize.U16));
        }

        IEnumerable<DisRec2OperString_Base> mode_immMem(BinaryReader br, DisassAddressBase pc, ref ushort len, IList<string> hints, DisassState65816 state)
        {
            return state.RegSizeM8 ? mode_imm8(br, pc, ref len, hints, state) : mode_imm16(br, pc, ref len, hints, state);
        }
        IEnumerable<DisRec2OperString_Base> mode_immIX(BinaryReader br, DisassAddressBase pc, ref ushort len, IList<string> hints, DisassState65816 state)
        {
            return state.RegSizeX8 ? mode_imm8(br, pc, ref len, hints, state) : mode_imm16(br, pc, ref len, hints, state);
        }


        IEnumerable<DisRec2OperString_Base> mode_xyc(BinaryReader br, DisassAddressBase pc, ref ushort len, IList<string> hints, DisassState65816 state)
        {
            len += 2;
            byte val = br.ReadByte();
            byte val2 = br.ReadByte();
            return OperStr("#").Concat(OperNum(val, SymbolType.Immediate, DisRec2_NumSize.U8))
                .Concat(OperStr(", #").Concat(OperNum(val2, SymbolType.Immediate, DisRec2_NumSize.U8)));
        }


        IEnumerable<DisRec2OperString_Base> mode_ind_dpX(BinaryReader br, DisassAddressBase pc, ref ushort len, IList<string> hints, DisassState65816 state)
        {
            len++;
            byte val = br.ReadByte();
            return OperStr("(z:").Concat(OperAddr(new Address65816_dp(val), SymbolType.Pointer | SymbolType.Offset).Concat(OperStr(",X)")));
        }

        IEnumerable<DisRec2OperString_Base> mode_ind_dpY(BinaryReader br, DisassAddressBase pc, ref ushort len, IList<string> hints, DisassState65816 state)
        {
            len++;
            byte val = br.ReadByte();
            return OperStr("(z:").Concat(OperAddr(new Address65816_dp(val), SymbolType.Pointer | SymbolType.Offset).Concat(OperStr("),Y")));
        }
        IEnumerable<DisRec2OperString_Base> mode_ind_dp(BinaryReader br, DisassAddressBase pc, ref ushort len, IList<string> hints, DisassState65816 state)
        {
            len++;
            byte val = br.ReadByte();
            return OperStr("(z:").Concat(OperAddr(new Address65816_dp(val), SymbolType.Pointer | SymbolType.Offset).Concat(OperStr(")")));
        }

        IEnumerable<DisRec2OperString_Base> mode_long_ind_dp(BinaryReader br, DisassAddressBase pc, ref ushort len, IList<string> hints, DisassState65816 state)
        {
            len++;
            byte val = br.ReadByte();
            return OperStr("[z:").Concat(OperAddr(new Address65816_dp(val), SymbolType.Pointer | SymbolType.Offset).Concat(OperStr("]")));
        }

        IEnumerable<DisRec2OperString_Base> mode_long_ind_dpY(BinaryReader br, DisassAddressBase pc, ref ushort len, IList<string> hints, DisassState65816 state)
        {
            len++;
            byte val = br.ReadByte();
            return OperStr("[z:").Concat(OperAddr(new Address65816_dp(val), SymbolType.Pointer | SymbolType.Offset).Concat(OperStr("],Y")));
        }

        IEnumerable<DisRec2OperString_Base> mode_offs_stack(BinaryReader br, DisassAddressBase pc, ref ushort len, IList<string> hints, DisassState65816 state)
        {
            len++;
            byte val = br.ReadByte();
            return OperNum(val, SymbolType.Offset, DisRec2_NumSize.S8).Concat(OperStr(",S"));
        }

        IEnumerable<DisRec2OperString_Base> mode_ind_offs_stack_Y(BinaryReader br, DisassAddressBase pc, ref ushort len, IList<string> hints, DisassState65816 state)
        {
            len++;
            byte val = br.ReadByte();
            return OperStr("(").Concat(OperNum(val, SymbolType.Offset, DisRec2_NumSize.S8).Concat(OperStr(",S),Y")));
        }


        IEnumerable<DisRec2OperString_Base> mode_dp(BinaryReader br, DisassAddressBase pc, ref ushort len, IList<string> hints, DisassState65816 state)
        {
            len++;
            byte val = br.ReadByte();
            return OperStr("z:").Concat(OperAddr(new Address65816_dp(val), SymbolType.Pointer | SymbolType.Offset));
        }

        IEnumerable<DisRec2OperString_Base> mode_dpX(BinaryReader br, DisassAddressBase pc, ref ushort len, IList<string> hints, DisassState65816 state)
        {
            len++;
            byte val = br.ReadByte();
            return OperStr("z:").Concat(OperAddr(new Address65816_dp(val), SymbolType.Pointer | SymbolType.Offset)).Concat(OperStr(",X"));
        }
        IEnumerable<DisRec2OperString_Base> mode_dpY(BinaryReader br, DisassAddressBase pc, ref ushort len, IList<string> hints, DisassState65816 state)
        {
            len++;
            byte val = br.ReadByte();
            return OperStr("z:").Concat(OperAddr(new Address65816_dp(val), SymbolType.Pointer | SymbolType.Offset)).Concat(OperStr(",Y"));
        }

        private delegate IEnumerable<DisRec2OperString_Base> mode_decode(BinaryReader br, DisassAddressBase pc, ref ushort len, IList<string> hints, DisassState65816 state);

        public DisRec2<UInt32> Decode(BinaryReader br, DisassAddressBase pc, IDisassState state = null)
        {

            DisassState65816 state816 = state as DisassState65816 ?? new DisassState65816();

            List<string> hints = new List<string>();

            byte opcode = br.ReadByte();
            ushort len = 1;

            string opcode_s = "?";

            mode_decode address_mode = null;


            byte opcol = (byte)(opcode & 0x0F);
            byte oprow = (byte)(opcode >> 4);

            (opcode_s, address_mode) = opcode switch
            {
                /* random col 2 even rows */
                0x02 => ("COP", mode_COP),
                0x22 => ("JSL", mode_long_a),
                0x42 => ("WDM", mode_WDM),
                0x62 => ("PER", mode_r16),
                0x82 => ("BRL", mode_r16),
                0xA2 => ("LDX", mode_immIX),
                0xC2 => ("REP", mode_immRep),
                0xE2 => ("SEP", mode_immSep),
                /* random col 4 */
                0x44 => ("MVP", mode_xyc),
                0x54 => ("MVN", mode_xyc),
                0xD4 => ("PEI", mode_ind_a),
                0xF4 => ("PEA", mode_a),
                /* random col C */
                0x4C => ("JMP", mode_a),
                0x5C => ("JML", mode_long_a),
                0x6C => ("JMP", mode_ind_a),
                0x7C => ("JMP", mode_ind_aX),
                0xDC => ("JML", mode_ind_a),
                0xFC => ("JSR", mode_ind_aX),
                _ => (opcol) switch
                {
                    0 => oprow switch
                    {
                        //column 0 - randomers
                        0x0 => ("BRK", mode_BRK),
                        0x1 => ("BPL", mode_r),
                        0x2 => ("JSR", mode_a),
                        0x3 => ("BMI", mode_r),
                        0x4 => ("RTI", null),
                        0x5 => ("BVC", mode_r),
                        0x6 => ("RTS", null),
                        0x7 => ("BVS", mode_r),
                        0x8 => ("BRA", mode_r),
                        0x9 => ("BCC", mode_r),
                        0xA => ("LDY", mode_immIX),
                        0xB => ("BCS", mode_r),
                        0xC => ("CPY", mode_immIX),
                        0xD => ("BNE", mode_r),
                        0xE => ("CPX", mode_immIX),
                        0xF => ("BEQ", mode_r),
                        _ => throw new Exception("Unexpected value")
                    },
                    1 or 2 or 3 or 5 or 7 or 9 or 0xD or 0xF =>
                        /* arithmetic and main load stores even rows of col 2 already covered above */
                        (
                            (opcode >> 5) switch
                            {
                                0 => "ORA",
                                1 => "AND",
                                2 => "EOR",
                                3 => "ADC",
                                4 => "STA",
                                5 => "LDA",
                                6 => "CMP",
                                7 => "SBC",
                                _ => throw new Exception("Unexpected value")
                            },
                            (opcode & 0x1F) switch
                            {
                                0x01 => mode_ind_dpX,
                                0x03 => mode_offs_stack,
                                0x05 => mode_dp,
                                0x07 => mode_long_ind_dp,
                                0x09 => mode_immMem,
                                0x0D => mode_a,
                                0x0F => mode_long_a,
                                0x11 => mode_ind_dpY,
                                0x12 => mode_ind_dp,
                                0x13 => mode_ind_offs_stack_Y,
                                0x15 => mode_dpX,
                                0x17 => mode_long_ind_dpY,
                                0x19 => mode_aY,
                                0x1D => mode_aX,
                                0x1F => mode_long_aX,
                                _ => throw new Exception("Unexpected value")
                            }

                        ),
                    4 or 0xC => oprow switch
                    {
                        /* column 4 / C */
                        0 or 1 => (
                            (oprow == 0) ? "TSB" : "TRB",
                            (opcol == 4) ? mode_dp : mode_a),
                        2 or 3 or 0xA or 0xB => (
                            (oprow < 0xA) ? "BIT" : "LDY",
                            (opcol & 8 | oprow & 1) switch
                            {
                                0 => mode_dp,
                                8 => mode_a,
                                1 => mode_dpX,
                                9 => mode_aX,
                                _ => throw new Exception($"Unexpected value {opcode:X}")
                            }),
                        6 or 7 => (oprow | opcol) switch
                        {
                            6 or 7 => ("STZ", ((oprow & 1) == 0) ? mode_dp : mode_dpX),
                            0xC => ("JMP", mode_aX),
                            0xD => ("JML", mode_ind_a),
                            _ => throw new Exception($"Unexpected value {opcode:X}")
                        },
                        8 or 9 => (oprow | opcol) switch
                        {
                            4 or 5 => ("STY", ((oprow & 1) == 0) ? mode_dp : mode_dpX),
                            0xC => ("STY", address_mode = mode_a),
                            0xD => ("STZ", mode_a),
                            _ => throw new Exception($"Unexpected value {opcode:X}")
                        },
                        0xC or 0xE =>
                            ((oprow == 0xC) ? "CPY" : "CPX",
                                (opcol == 0x4) ? mode_dp : mode_a
                            ),
                        _ => throw new Exception($"Unexpected value {opcode:X}")
                    },
                    6 or 0xE => (
                        /* columns 6 and E */
                        (oprow & 0xE) switch
                        {
                            0 => "ASL",
                            2 => "ROL",
                            4 => "LSR",
                            6 => "ROR",
                            8 => "STX",
                            0xA => "LDX",
                            0xC => "DEC",
                            0xE => "INC",
                            _ => throw new Exception($"Unexpected value {opcode:X}")
                        },
                        (opcol | (oprow & 1)) switch
                        {
                            0x6 => mode_dp,
                            0xE => mode_a,
                            0x7 => (oprow == 8 || oprow == 0xA) ? mode_dpY : mode_dpX,
                            0xF => (oprow == 8 || oprow == 0xA) ? mode_aY : mode_aX,
                            _ => throw new Exception($"Unexpected value {opcode:X}")
                        }
                        ),
                    8 => (oprow switch
                        {   /* column 8 - all no operands */
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
                            0xF => "SED",
                            _ => throw new Exception($"Unexpected value {opcode:X}")
                        }, null),
                    0xA => (oprow switch
                        {   /* column A - all no operands */
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
                            0xF => "PLX",
                            _ => throw new Exception($"Unexpected value {opcode:X}")
                        }, oprow switch
                        {
                            <=4 or 6 => mode_Acc,
                            _ => null
                        }),
                    0xB => (oprow switch
                        {   /* column B - all no operands */
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
                            0xF => "XCE",
                            _ => throw new Exception($"Unexpected value {opcode:X}")
                        }, null),
                    _ => throw new Exception($"Unexpected value {opcode:X}")
                }
            };

            IEnumerable<DisRec2OperString_Base> operands = (address_mode == null) ? Enumerable.Empty<DisRec2OperString_Base>() : address_mode(br, pc, ref len, hints, state816);

            hints.AddRange(operands.OfType<DisRec2OperString_Address>().Where(i => i.Address is ILongFormAddress).Select(i => (i.Address as ILongFormAddress)?.ToStringLong()));

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

    }
}
