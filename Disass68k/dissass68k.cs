
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Disass68k
{
    public static class Disass
    {

        public static Regex reHint = new Regex(@"\s*\[([^\]]+)]\s*", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public record DisRec
        {
            public bool Decoded { get; init; }
            public string Mnemonic { get; init; }
            public string Operands { get; init; }

            public string Hints { get; init; }

            public ushort Length { get; init; }

            public override string ToString() => $"{Mnemonic,-8} {Operands,-40}; {Hints}";

        }

        struct OpcodeDetails
        {
            public ushort And { get; }
            public ushort Xor { get; }
            public OpcodeDetails(ushort a, ushort x)
            {
                And = a;
                Xor = x;
            }
        };

        readonly static OpcodeDetails[] optab = {
    new OpcodeDetails(0x0000, 0x0000), new OpcodeDetails(0xF1F0, 0xC100), new OpcodeDetails(0xF000, 0xD000), new OpcodeDetails(0xF0C0, 0xD0C0),
    new OpcodeDetails(0xFF00, 0x0600), new OpcodeDetails(0xF100, 0x5000), new OpcodeDetails(0xF130, 0xD100), new OpcodeDetails(0xF000, 0xC000),
    new OpcodeDetails(0xFF00, 0x0200), new OpcodeDetails(0xF118, 0xE100), new OpcodeDetails(0xFFC0, 0xE1C0), new OpcodeDetails(0xF118, 0xE000),
    new OpcodeDetails(0xFFC0, 0xE0C0), new OpcodeDetails(0xF000, 0x6000), new OpcodeDetails(0xF1C0, 0x0140), new OpcodeDetails(0xFFC0, 0x0840),
    new OpcodeDetails(0xF1C0, 0x0180), new OpcodeDetails(0xFFC0, 0x0880), new OpcodeDetails(0xF1C0, 0x01C0), new OpcodeDetails(0xFFC0, 0x08C0),
    new OpcodeDetails(0xF1C0, 0x0100), new OpcodeDetails(0xFFC0, 0x0800), new OpcodeDetails(0xF1C0, 0x4180), new OpcodeDetails(0xFF00, 0x4200),
    new OpcodeDetails(0xF100, 0xB000), new OpcodeDetails(0xF0C0, 0xB0C0), new OpcodeDetails(0xFF00, 0x0C00), new OpcodeDetails(0xF138, 0xB108),
    new OpcodeDetails(0xF0F8, 0x50C8), new OpcodeDetails(0xF1C0, 0x81C0), new OpcodeDetails(0xF1C0, 0x80C0), new OpcodeDetails(0xF100, 0xB100),
    new OpcodeDetails(0xFF00, 0x0A00), new OpcodeDetails(0xF100, 0xC100), new OpcodeDetails(0xFFB8, 0x4880), new OpcodeDetails(0xFFC0, 0x4EC0),
    new OpcodeDetails(0xFFC0, 0x4E80), new OpcodeDetails(0xF1C0, 0x41C0), new OpcodeDetails(0xFFF8, 0x4E50), new OpcodeDetails(0xF118, 0xE108),
    new OpcodeDetails(0xFFC0, 0xE3C0), new OpcodeDetails(0xF118, 0xE008), new OpcodeDetails(0xFFC0, 0xE2C0), new OpcodeDetails(0xC000, 0x0000),
    new OpcodeDetails(0xFFC0, 0x44C0), new OpcodeDetails(0xFFC0, 0x46C0), new OpcodeDetails(0xFFC0, 0x40C0), new OpcodeDetails(0xFFF0, 0x4E60),
    new OpcodeDetails(0xC1C0, 0x0040), new OpcodeDetails(0xFB80, 0x4880), new OpcodeDetails(0xF138, 0x0108), new OpcodeDetails(0xF100, 0x7000),
    new OpcodeDetails(0xF1C0, 0xC1C0), new OpcodeDetails(0xF1C0, 0xC0C0), new OpcodeDetails(0xFFC0, 0x4800), new OpcodeDetails(0xFF00, 0x4400),
    new OpcodeDetails(0xFF00, 0x4000), new OpcodeDetails(0xFFFF, 0x4E71), new OpcodeDetails(0xFF00, 0x4600), new OpcodeDetails(0xF000, 0x8000),
    new OpcodeDetails(0xFF00, 0x0000), new OpcodeDetails(0xFFC0, 0x4840), new OpcodeDetails(0xFFFF, 0x4E70), new OpcodeDetails(0xF118, 0xE118),
    new OpcodeDetails(0xFFC0, 0xE7C0), new OpcodeDetails(0xF118, 0xE018), new OpcodeDetails(0xFFC0, 0xE6C0), new OpcodeDetails(0xF118, 0xE110),
    new OpcodeDetails(0xFFC0, 0xE5C0), new OpcodeDetails(0xF118, 0xE010), new OpcodeDetails(0xFFC0, 0xE4C0), new OpcodeDetails(0xFFFF, 0x4E73),
    new OpcodeDetails(0xFFFF, 0x4E77), new OpcodeDetails(0xFFFF, 0x4E75), new OpcodeDetails(0xF1F0, 0x8100), new OpcodeDetails(0xF0C0, 0x50C0),
    new OpcodeDetails(0xFFFF, 0x4E72), new OpcodeDetails(0xF000, 0x9000), new OpcodeDetails(0xF0C0, 0x90C0), new OpcodeDetails(0xFF00, 0x0400),
    new OpcodeDetails(0xF100, 0x5100), new OpcodeDetails(0xF130, 0x9100), new OpcodeDetails(0xFFF8, 0x4840), new OpcodeDetails(0xFFC0, 0x4AC0),
    new OpcodeDetails(0xFFF0, 0x4E40), new OpcodeDetails(0xFFFF, 0x4E76), new OpcodeDetails(0xFF00, 0x4A00), new OpcodeDetails(0xFFF8, 0x4E58)
        };


        private readonly static string[] bra_tab = {
                "bra",  "bsr",  "bhi",  "bls",
                "bcc",  "bcs",  "bne",  "beq",
                "bvc",  "bvs",  "bpl",  "bmi",
                "bge",  "blt",  "bgt",  "ble"
        };

        private readonly static string[] scc_tab = {
                "st",   "sf",   "shi",  "sls",
                "scc",  "scs",  "sne",  "seq",
                "svc",  "svs",  "spl",  "smi",
                "sge",  "slt",  "sgt",  "sle"
        };

        private readonly static char[] size_arr = { 'b', 'w', 'l' };

        private readonly static char[] ir = { 'w', 'l' }; /* for mode 6 */


        private static string signedhex(int num, int digits)
        {
            if (num < 0)
                return $"-${(-num).ToString($"X{digits}")}";
            else
                return $"${num.ToString($"X{digits}")}";
        }

        /*!
	Prints the addressing mode @c mode, using @c reg and @c size, to @c out_s.

	@param mode 0 to 12, indicating addressing mode.
	@param size 0 = byte, 1 = word, 2 = long.
*/
        private static string sprintmode(ushort mode, byte reg, byte size, BEReader r, IReadOnlyDictionary<uint,string> dicSyms)
        {
            StringBuilder s = new StringBuilder();

            switch (mode)
            {
                case 0: s.Append($"D{reg}"); break;
                case 1: s.Append($"A{reg}"); break;
                case 2: s.Append($"(A{reg})"); break;
                case 3: s.Append($"(A{reg})+"); break;
                case 4: s.Append($"-(A{reg})"); break;
                case 5: /* reg + disp */
                case 9: /* pcr + disp */
                    {
                        short displacement = r.ReadInt16BE();
                        if (mode == 5)
                        {
                            s.Append($"{signedhex(displacement,4)}(A{reg})");
                        }
                        else
                        {
                            uint ldata = (uint)((r.PC - 2) + displacement);
                            string sym = FindSym(dicSyms, ldata);
                            if (!string.IsNullOrEmpty(sym))
                                s.Append($"{sym}(PC)[{signedhex(displacement, 4)}=${ldata:X8}]");
                            else
                                s.Append($"${ldata:X8}(PC)[{signedhex(displacement, 4)}]");
                        }
                    }
                    break;
                case 6: /* Areg with index + disp */
                case 10: /* PC with index + disp */
                    {
                        ushort data = r.ReadUInt16BE(); /* index and displacement data */

                        sbyte displacement = (sbyte)(data & 0x00FF);

                        int ireg = (data & 0x7000) >> 12;
                        int itype = (data & 0x8000); /* == 0 is Dreg */
                        int isize = (data & 0x0800) >> 11; /* == 0 is .W else .L */

                        if (mode == 6)
                        {
                            if (itype == 0)
                            {
                                s.Append($"{signedhex(displacement, 2)}(A{reg},D{ireg}.{ir[isize]})");
                            }
                            else
                            {
                                s.Append($"{signedhex(displacement, 2)}(A{reg},A{ireg}.{ir[isize]})");
                            }
                        }
                        else
                        { /* PC */
                            uint ldata = (uint)((r.PC - 4) + displacement);
                            string sym = FindSym(dicSyms, ldata);
                            string rt = (itype == 0) ? "D" : "A";
                            if (!string.IsNullOrEmpty(sym))
                                s.Append($"{sym}(PC,{rt}{ireg}.{ir[isize]})[{signedhex(displacement, 2)}=${ldata:X8}]");
                            else
                                s.Append($"{ldata:X8}(PC,{rt}{ireg}.{ir[isize]})[{signedhex(displacement, 2)}=${ldata:X8}]");
                        }
                    }
                    break;
                case 7: /* abs short */
                    {                        
                        short ldata = r.ReadInt16BE();
                        string sym = FindSym(dicSyms, ldata);
                        if (!string.IsNullOrEmpty(sym))
                            s.Append($"{sym}[=${ldata:X4}]");
                        else
                            s.Append($"{ldata:X4}");
                    }
                    break;
                case 8:
                    {
                        uint ldata = r.ReadUInt32BE();
                        string sym = FindSym(dicSyms, ldata);
                        if (!string.IsNullOrEmpty(sym))
                            s.Append($"{sym}[=${ldata:X8}]");
                        else
                            s.Append($"${ldata:X8}");
                    }
                    break;
                case 11:
                    {
                        switch (size)
                        {
                            case 0:
                                {
                                    var d = (sbyte)(r.ReadInt16BE() & 0xFF);
                                    s.Append($"#{signedhex(d, 2)}[${((byte)d):X2}]");
                                }
                                break;
                            case 1:
                                {
                                    var d = r.ReadInt16BE();
                                    s.Append($"#{signedhex(d, 4)}[${((ushort)d):X4}]");
                                }
                                break;
                            case 2:
                                {
                                    var d = r.ReadInt32BE();
                                    s.Append($"#{signedhex(d, 8)}[${((uint)d):X8}]");
                                }
                                break;
                        }
                    }
                    break;
                default:
                    throw new System.Exception($"Mode out of range in sprintmode = {mode}");
            }
            return s.ToString();
        }

        /*!
	Decodes the addressing mode from @c instruction.

	@returns A mode in the range 0 to 11, if a valid addressing mode could be
		determined; 12 otherwise.
*/
        private static byte getmode(ushort instruction)
        {
            byte mode = (byte)((instruction & 0x0038) >> 3);
            byte reg = (byte)(instruction & 0x0007);

            if (mode == 7)
            {
                if (reg >= 5)
                {
                    return 12; /* i.e. invalid */
                }
                else
                {
                    return (byte)(7 + reg);
                }
            }
            return mode;
        }

        private static byte getBits_0007(ushort word)
        {
            return (byte)(word & 0x0007);
        }
        private static byte getBits_0E00(ushort word)
        {
            return (byte)((word & 0x0E00) >> 9);
        }
        private static byte getBits_00C0(ushort word)
        {
            return (byte)((word & 0x00C0) >> 6);
        }
        private static byte getBits_0100(ushort word)
        {
            return (byte)((word & 0x0100) >> 8);
        }
        private static byte getBits_0F00(ushort word)
        {
            return (byte)((word & 0x0F00) >> 8);
        }
        private static byte getBits_3000(ushort word)
        {
            return (byte)((word & 0x3000) >> 12);
        }

        public static string FindSym(IReadOnlyDictionary<uint, string> dicSyms, short addr)
        {
            return FindSym(dicSyms, (uint)(int)addr);
        }
        
        public static string FindSym(IReadOnlyDictionary<uint, string> dicSyms, uint addr)
        {
            string s;
            if (dicSyms != null && dicSyms.TryGetValue(addr, out s))

                return s;
            else
                return null;
        }

        public static DisRec Decode(BinaryReader br, uint pc, IReadOnlyDictionary<uint,string> dicSyms = null, bool specialAbi = false)
        {
            var r = new BEReader(br, pc);


            uint start_address = r.PC;
            ushort word = r.ReadUInt16BE();
            bool decoded = false;

            string opcode_s = null, operand_s = null;
            for (int opnum = 1; opnum <= 87; ++opnum)
            {
                if ((word & optab[opnum].And) == optab[opnum].Xor)
                {
                    /* Diagnostic code */
                    //                    diagnostic_printf("(%i) ", opnum);

                    switch (opnum)
                    { /* opnum = 1..85 */
                        case 1:
                        case 74:
                            { /* ABCD + SBCD */
                                byte sreg = getBits_0007(word);
                                byte dreg = getBits_0E00(word);
                                if (opnum == 1)
                                {
                                    opcode_s = "abcd";
                                }
                                else
                                {
                                    opcode_s = "sbcd";
                                }
                                if ((word & 0x0008) == 0)
                                {
                                    /* reg-reg */
                                    operand_s = $"D{sreg},D{dreg}";
                                }
                                else
                                {
                                    /* mem-mem */
                                    operand_s = $"-(A{sreg}),-A({dreg})";
                                }
                                decoded = true;
                            }
                            break;
                        case 2:
                        case 7:
                        case 31:
                        case 59: /* ADD, AND, EOR, OR */
                        case 77:
                            { /* SUB */
                                ushort dmode = getmode(word);
                                byte dreg = getBits_0007(word);
                                byte size = getBits_00C0(word);

                                /* Diagnostic code */
                                //diagnostic_printf("dmode = %i, dreg = %i, size = %i", dmode, dreg, size);

                                if (size == 3) break;
                                /*
                                if (dmode == 1) break;
                                */
                                if ((opnum == 2) && (dmode == 1) && (size == 0)) break;
                                if ((opnum == 77) && (dmode == 1) && (size == 0)) break;

                                int dir = getBits_0100(word); /* 0 = dreg dest */
                                if ((opnum == 31) && (dir == 0)) break;
                                /* dir == 1 : Dreg is source */
                                if ((dir == 1) && (dmode >= 9)) break;

                                switch (opnum)
                                {
                                    case 2:
                                        opcode_s = $"add.{size_arr[size]}";
                                        break;
                                    case 7:
                                        opcode_s = $"and.{size_arr[size]}";
                                        break;
                                    case 31:
                                        opcode_s = $"eor.{size_arr[size]}";
                                        break;
                                    case 59:
                                        opcode_s = $"or.{size_arr[size]}";
                                        break;
                                    case 77:
                                        opcode_s = $"sub.{size_arr[size]}";
                                        break;
                                }

                                string dest_s = sprintmode(dmode, dreg, size, r, dicSyms);

                                byte sreg = getBits_0E00(word);
                                string source_s;
                                source_s = $"D{sreg}";
                                /* reverse source & dest if dir == 0 */
                                if (dir != 0)
                                {
                                    operand_s = $"{source_s},{dest_s}";
                                }
                                else
                                {
                                    operand_s = $"{dest_s},{source_s}";
                                }
                                decoded = true;
                            }
                            break;
                        case 3:
                        case 78:
                            { /* ADDA + SUBA */
                                ushort smode = getmode(word);
                                byte sreg = getBits_0007(word);
                                byte dreg = getBits_0E00(word);
                                byte size = (byte)((getBits_0100(word)) + 1);
                                switch (opnum)
                                {
                                    case 3:
                                        opcode_s = $"adda.{ size_arr[size] }";
                                        break;
                                    case 78:
                                        opcode_s = $"suba.{ size_arr[size] }";
                                        break;
                                }
                                string source_s;
                                source_s = sprintmode(smode, sreg, size, r, dicSyms);
                                operand_s = $"{ source_s },A{ dreg }";
                                decoded = true;
                            }
                            break;
                        case 4:
                        case 8:
                        case 26:
                        case 32:
                        case 60:
                        case 79:
                            { /* ADDI, ANDI, CMPI, EORI, ORI, SUBI */
                                ushort dmode = getmode(word);
                                byte dreg = getBits_0007(word);
                                byte size = getBits_00C0(word);

                                if (size == 3) break;
                                if (dmode == 1) break;
                                if ((dmode == 9) || (dmode == 10)) break; /* Invalid */
                                if (dmode == 12) break;
                                if ((dmode == 11) && /* ADDI, CMPI, SUBI */
                                    ((opnum == 4) || (opnum == 26) || (opnum == 79))) break;

                                switch (opnum)
                                {
                                    case 4:
                                        opcode_s = $"addi.{ size_arr[size] }";
                                        break;
                                    case 8:
                                        opcode_s = $"andi.{ size_arr[size] }";
                                        break;
                                    case 26:
                                        opcode_s = $"cmpi.{ size_arr[size] }";
                                        break;
                                    case 32:
                                        opcode_s = $"eori.{ size_arr[size] }";
                                        break;
                                    case 60:
                                        opcode_s = $"ori.{ size_arr[size] }";
                                        break;
                                    case 79:
                                        opcode_s = $"subi.{ size_arr[size] }";
                                        break;
                                }

                                string source_s = "?";
                                switch (size)
                                {
                                    case 0:
                                        source_s = $"#${r.ReadUInt16BE():X2}";
                                        break;
                                    case 1:
                                        source_s = $"#${r.ReadUInt16BE():X4}";
                                        break;
                                    case 2:
                                        source_s = $"#${r.ReadUInt32BE():X8}";
                                        break;
                                }

                                string dest_s;
                                if (dmode == 11)
                                {
                                    dest_s = "SR";
                                }
                                else
                                {
                                    dest_s = sprintmode(dmode, dreg, size, r, dicSyms);
                                }
                                operand_s = $"{source_s},{dest_s}";
                                decoded = true;
                            }
                            break;
                        case 5:
                        case 80:
                            {/* ADDQ + SUBQ */
                                byte dmode = getmode(word);
                                byte dreg = getBits_0007(word);
                                byte size = getBits_00C0(word);

                                if (size == 3) break;
                                if (dmode >= 9) break;
                                if ((size == 0) && (dmode == 1)) break;

                                if (opnum == 5)
                                {
                                    opcode_s = $"addq.{size_arr[size]}";
                                }
                                else
                                {
                                    opcode_s = $"subq.{size_arr[size]}";
                                }
                                string dest_s = sprintmode(dmode, dreg, size, r, dicSyms);
                                byte count = getBits_0E00(word);
                                operand_s = $"#{(count == 0 ? 8 : count)},{dest_s}";
                                decoded = true;
                            }
                            break;
                        case 6:
                        case 81: /* ADDX + SUBX */
                        case 27:
                            { /* CMPM */
                                byte size = getBits_00C0(word);
                                if (size == 3) break;

                                byte sreg = getBits_0007(word);
                                byte dreg = getBits_0E00(word);
                                switch (opnum)
                                {
                                    case 6:
                                        opcode_s = $"addx.{size_arr[size]}";
                                        break;
                                    case 81:
                                        opcode_s = $"subx.{size_arr[size]}";
                                        break;
                                    case 27:
                                        opcode_s = $"cmpm.{size_arr[size]}";
                                        break;
                                }
                                if ((opnum != 27) && ((word & 0x0008) == 0))
                                {
                                    /* reg-reg */
                                    operand_s = $"D{sreg},D{dreg}";
                                }
                                else
                                {
                                    /* mem-mem */
                                    operand_s = $"-(A{sreg}),-(A{dreg})";
                                }
                                if (opnum == 27)
                                {
                                    operand_s = $"(A{sreg})+,(A{dreg})+";
                                }
                                decoded = true;
                            }
                            break;
                        case 9:
                        case 11:
                        case 39:
                        case 41:
                        case 63:
                        case 65:
                        case 67:
                        case 69:
                            { /* ASL, ASR, LSL, LSR, ROL, ROR, ROXL, ROXR */
                                byte dreg = getBits_0007(word);
                                byte size = getBits_00C0(word);
                                if (size == 3) break;

                                switch (opnum)
                                {
                                    case 9:
                                        opcode_s = $"asl.{size_arr[size]}";
                                        break;
                                    case 11:
                                        opcode_s = $"asr.{size_arr[size]}";
                                        break;
                                    case 39:
                                        opcode_s = $"lsl.{size_arr[size]}";
                                        break;
                                    case 41:
                                        opcode_s = $"lsr.{size_arr[size]}";
                                        break;
                                    case 63:
                                        opcode_s = $"ror.{size_arr[size]}";
                                        break;
                                    case 65:
                                        opcode_s = $"rol.{size_arr[size]}";
                                        break;
                                    case 67:
                                        opcode_s = $"roxl.{size_arr[size]}";
                                        break;
                                    case 69:
                                        opcode_s = $"roxr.{size_arr[size]}";
                                        break;
                                }
                                byte count = getBits_0E00(word);
                                if (((word & 0x0020) >> 5) == 0)
                                { /* imm */
                                    if (count == 0) count = 8;
                                    operand_s = $"#{count},D{dreg}";
                                }
                                else
                                { /* count in dreg */
                                    operand_s = $"D{count},D{dreg}";
                                }
                                decoded = true;
                            }
                            break;
                        case 10:
                        case 12:
                        case 40:
                        case 42:
                        case 64:
                        case 66:
                        case 68: /* Memory-to-memory */
                        case 70:
                            { /* ASL, ASR, LSL, LSR, ROL, ROR, ROXL, ROXR */
                                byte dmode = getmode(word);
                                byte dreg = getBits_0007(word);
                                if ((dmode <= 1) || (dmode >= 9)) break; /* Invalid */

                                switch (opnum)
                                {
                                    case 10:
                                        opcode_s = "asl";
                                        break;
                                    case 12:
                                        opcode_s = "asr";
                                        break;
                                    case 40:
                                        opcode_s = "lsl";
                                        break;
                                    case 42:
                                        opcode_s = "lsr";
                                        break;
                                    case 64:
                                        opcode_s = "ror";
                                        break;
                                    case 66:
                                        opcode_s = "rol";
                                        break;
                                    case 68:
                                        opcode_s = "roxl";
                                        break;
                                    case 70:
                                        opcode_s = "roxr";
                                        break;
                                }
                                operand_s = sprintmode(dmode, dreg, 0, r, dicSyms);
                                decoded = true;
                            }
                            break;
                        case 13:
                            {/* Bcc */
                                int cc = getBits_0F00(word);
                                opcode_s = bra_tab[cc];

                                sbyte offset = (sbyte)(word & 0x00FF);
                                if (offset != 0)
                                {
                                    string sym = FindSym(dicSyms, (uint)(r.PC + offset));
                                    if (!string.IsNullOrEmpty(sym))
                                        operand_s = $"{sym}[{signedhex(offset, 2)}=${(r.PC + offset):X8}]";
                                    else
                                        operand_s = $"${(r.PC + offset):X8}[{signedhex(offset, 2)}]";

                                }
                                else
                                {
                                    short offsetw = r.ReadInt16BE();
                                    string sym = FindSym(dicSyms, (uint)(r.PC + offsetw - 2));
                                    if (!string.IsNullOrEmpty(sym)) 
                                        operand_s = $"{sym}[{signedhex(offsetw, 4)}=${(r.PC + offsetw - 2):X8}]";
                                    else
                                        operand_s = $"${(r.PC + offsetw - 2):X8}[=${(r.PC + offsetw - 2):X8}[{signedhex(offsetw, 4)}]";
                                }
                                decoded = true;
                            }
                            break;
                        case 14:
                        case 15:
                        case 16:
                        case 17: /* BCHG + BCLR */
                        case 18:
                        case 19: /* BSET */
                        case 20:
                        case 21:
                            {/* BTST */
                                byte dmode = getmode(word);
                                byte dreg = getBits_0007(word);

                                if (dmode == 1) break;
                                if (dmode >= 11) break;
                                if ((opnum < 20) && (dmode >= 9)) break;

                                byte sreg = getBits_0E00(word);
                                string source_s;
                                switch (opnum)
                                {
                                    case 14: /* BCHG_DREG */
                                        opcode_s = "bchg";
                                        source_s = $"D{sreg}";
                                        break;
                                    case 15:
                                        {/* BCHG_IMM */
                                            opcode_s = "bchg";
                                            byte data = (byte)(r.ReadUInt16BE() & 0x001F);
                                            source_s = $"#${data:X2}";
                                        }
                                        break;
                                    case 16: /* BCLR_DREG */
                                        opcode_s = "bclr";
                                        source_s = $"D{sreg}";
                                        break;
                                    case 17:
                                        {/* BCLR_IMM */
                                            opcode_s = "bclr";
                                            byte data = (byte)(r.ReadUInt16BE() & 0x001F);
                                            source_s = $"#${data:X2}";
                                        }
                                        break;
                                    case 18: /* BSET_DREG */
                                        opcode_s = "bset";
                                        source_s = $"D{sreg}";
                                        break;
                                    case 19:
                                        { /* BSET_IMM */
                                            opcode_s = "bset";
                                            byte data = (byte)(r.ReadUInt16BE() & 0x001F);
                                            source_s = $"#${data:X2}";
                                        }
                                        break;
                                    case 20: /* BTST_DREG */
                                        opcode_s = "btst";
                                        source_s = $"D{sreg}";
                                        break;
                                    case 21:
                                        {/* BTST_IMM */
                                            opcode_s = "btst";
                                            byte data = (byte)(r.ReadUInt16BE() & 0x001F);
                                            source_s = $"#${data:X2}";
                                        }
                                        break;
                                    default:
                                        throw new System.Exception("Unrecognized op in BSET et al");
                                }
                                string dest_s = sprintmode(dmode, dreg, 0, r, dicSyms);
                                operand_s = $"{source_s},{dest_s}";
                                decoded = true;
                            }
                            break;
                        case 22: /* CHK */
                        case 29:
                        case 30:
                        case 52:
                        case 53: /* DIVS, DIVU, MULS, MULU */
                        case 24: /* CMP */
                            {
                                byte smode = getmode(word);
                                if ((smode == 1) && (opnum != 24)) break;
                                if (smode >= 12) break;

                                byte sreg = getBits_0007(word);
                                byte dreg = getBits_0E00(word);

                                byte size;
                                if (opnum == 24)
                                {
                                    size = getBits_00C0(word);
                                }
                                else
                                {
                                    size = 1; /* WORD */
                                }
                                if (size == 3) break;

                                switch (opnum)
                                {
                                    case 22: /* CHK */
                                        opcode_s = "chk";
                                        break;
                                    case 24: /* CMP */
                                        opcode_s = $"cmp.{size_arr[size]}";
                                        break;
                                    case 29: /* DIVS */
                                        opcode_s = "divs";
                                        break;
                                    case 30: /* DIVU */
                                        opcode_s = "divu";
                                        break;
                                    case 52: /* MULS */
                                        opcode_s = "muls";
                                        break;
                                    case 53: /* MULU */
                                        opcode_s = "mulu";
                                        break;
                                }
                                string source_s;
                                source_s = sprintmode(smode, sreg, size, r, dicSyms);
                                operand_s = $"{source_s},D{dreg}";
                                decoded = true;
                            }
                            break;
                        case 23:
                            {/* CLR */
                                byte dmode = getmode(word);
                                byte dreg = getBits_0007(word);
                                if ((dmode == 1) || (dmode >= 9)) break; /* Invalid */

                                byte size = getBits_00C0(word);
                                if (size == 3) break;

                                opcode_s = $"clr.{size_arr[size]}";
                                operand_s = sprintmode(dmode, dreg, size, r, dicSyms);
                                decoded = true;
                            }
                            break;
                        case 25:
                            {/* CMPA */
                                byte smode = getmode(word);
                                byte sreg = getBits_0007(word);
                                byte areg = getBits_0E00(word);
                                byte size = (byte)(getBits_0100(word) + 1);

                                opcode_s = $"cmpa.{size_arr[size]}";
                                string source_s = sprintmode(smode, sreg, size, r, dicSyms);
                                operand_s = $"{source_s},A{areg}";
                                decoded = true;
                            }
                            break;
                        case 28:
                            { /* DBcc */
                                byte cc = getBits_0F00(word);
                                opcode_s = $"D{bra_tab[cc]}";

                                if (cc == 0) opcode_s = "dbt";
                                if (cc == 1) opcode_s = "dbf";
                                short offset = r.ReadInt16BE();
                                byte dreg = getBits_0007(word);
                                operand_s = $"D{dreg},${(r.PC - 2 + offset):X8}";
                                decoded = true;
                            }
                            break;
                        case 33:
                            { /* EXG */
                                byte dmode = (byte)((word & 0x00F8) >> 3);
                                /*	8 - Both Dreg
                                    9 - Both Areg
                                    17 - Dreg + Areg */
                                if ((dmode != 8) && (dmode != 9) && (dmode != 17)) break;

                                byte dreg = getBits_0007(word);
                                byte areg = getBits_0E00(word);
                                opcode_s = "exg";

                                switch (dmode)
                                {
                                    case 8:
                                        operand_s = $"D{dreg},D{areg}";
                                        break;
                                    case 9:
                                        operand_s = $"A{dreg},A{areg}";
                                        break;
                                    case 17:
                                        operand_s = $"D{dreg},A{areg}";
                                        break;
                                }
                                decoded = true;
                            }
                            break;
                        case 34:
                            {/* EXT */
                                byte dreg = getBits_0007(word);
                                int size = ((word & 0x0040) >> 6) + 1;
                                opcode_s = $"ext.{size_arr[size]}";
                                operand_s = $"D{dreg}";
                                decoded = true;
                            }
                            break;
                        case 35:
                        case 36:
                            {/* JMP + JSR */
                                byte dmode = getmode(word);
                                byte dreg = getBits_0007(word);

                                if (dmode <= 1) break;
                                if ((dmode == 3) || (dmode == 4)) break;
                                if (dmode >= 11) break; /* Invalid */

                                switch (opnum)
                                {
                                    case 35:
                                        opcode_s = "jmp";
                                        break;
                                    case 36:
                                        opcode_s = "jsr";
                                        break;
                                }

                                operand_s = sprintmode(dmode, dreg, 0, r, dicSyms);
                                decoded = true;
                            }
                            break;
                        case 37:
                            {/* LEA */
                                byte smode = getmode(word);
                                if ((smode == 0) || (smode == 1)) break;
                                if ((smode == 3) || (smode == 4)) break;
                                if (smode >= 11) break;

                                byte sreg = getBits_0007(word);
                                opcode_s = "lea";
                                string source_s = sprintmode(smode, sreg, 0, r, dicSyms);

                                byte dreg = getBits_0E00(word);
                                operand_s = $"{source_s},A{dreg}";
                                decoded = true;
                            }
                            break;
                        case 38:
                            {/* LINK */
                                byte areg = getBits_0007(word);
                                short offset = r.ReadInt16BE();
                                opcode_s = "link";
                                operand_s = $"A{areg},#{signedhex(offset, 4)}";
                                decoded = true;
                            }
                            break;
                        case 43:
                            {/* MOVE */
                                ushort smode = getmode(word);
                                byte sreg = getBits_0007(word);
                                ushort data = (ushort)(((word & 0x0E00) >> 9) | ((word & 0x01C0) >> 3));
                                byte dmode = getmode(data);
                                byte dreg = getBits_0007(data);

                                byte size = getBits_3000(word); /* 1=B, 2=L, 3=W */
                                if (size == 0) break;
                                switch (size)
                                {
                                    case 1:
                                        size = 0;
                                        break;
                                    case 2:
                                        size = 2;
                                        break;
                                    case 3:
                                        size = 1;
                                        break;
                                }
                                /* 0=B, 1=W, 2=L */

                                /*
                                printf("smode = %i dmode = %i ",smode,dmode);
                                printf("sreg = %i dreg = %i \n",sreg,dreg);
                                */

                                /* check for illegal modes */
//DB:                                if ((smode == 1) && (size == 1)) break;
//DB:                                if ((smode == 9) || (smode == 10)) break;
                                if (smode > 11) break;
                                if (dmode == 1) break;
                                if (dmode >= 9) break;

                                opcode_s = $"move.{size_arr[size]}";


                                string source_s = sprintmode(smode, sreg, size, r, dicSyms);
                                string dest_s = sprintmode(dmode, dreg, size, r, dicSyms);
                                operand_s = $"{source_s},{dest_s}";
                                decoded = true;
                            }
                            break;
                        case 44: /* MOVE to CCR */
                        case 45:
                            {/* MOVE to SR */
                                byte smode = getmode(word);
                                byte sreg = getBits_0007(word);
                                byte size = 1; /* WORD */

                                if (smode == 1) break;
                                if (smode >= 12) break;

                                opcode_s = "move.w";
                                string source_s = sprintmode(smode, sreg, size, r, dicSyms);
                                if (opnum == 44)
                                {
                                    operand_s = $"{source_s},CCR";
                                }
                                else
                                {
                                    operand_s = $"{source_s},SR";
                                }
                                decoded = true;
                            }
                            break;
                        case 46:
                            {/* MOVE from SR */
                                byte dmode = getmode(word);
                                byte dreg = getBits_0007(word);
                                byte size = 1; /* WORD */

                                if (dmode == 1) break;
                                if (dmode >= 9) break;

                                opcode_s = "move.w";
                                string dest_s = sprintmode(dmode, dreg, size, r, dicSyms);
                                operand_s = $"SR,{dest_s}";
                                decoded = true;
                            }
                            break;
                        case 47:
                            { /* MOVE USP */
                                byte sreg = getBits_0007(word);
                                opcode_s = "move";
                                if ((word & 0x0008) == 0)
                                {
                                    /* to USP */
                                    operand_s = $"A{getBits_0007(word)},USP";
                                }
                                else
                                {
                                    /* from USP */
                                    operand_s = $"USP,A{getBits_0007(word)}";
                                }
                                decoded = true;
                            }
                            break;
                        case 48:
                            {/* MOVEA */
                                byte smode = getmode(word);
                                byte sreg = getBits_0007(word);
                                byte size = getBits_3000(word);

                                /* 2 = L, 3 = W */
                                if (size <= 1) break;
                                if (size == 3) size = 1;
                                /* 1 = W, 2 = L */

                                byte dreg = getBits_0E00(word);

                                opcode_s = $"movea.{size_arr[size]}";

                                string source_s = sprintmode(smode, sreg, size, r, dicSyms);
                                operand_s = $"{source_s},A{dreg}";
                                decoded = true;
                            }
                            break;
                        case 49:
                            {/* MOVEM */
                                byte dmode = getmode(word);
                                byte dreg = getBits_0007(word);
                                byte size = (byte)(((word & 0x0040) >> 6) + 1);

                                if ((dmode == 0) || (dmode == 1)) break;
                                if (dmode >= 9) break;

                                byte dir = (byte)((word & 0x0400) >> 10); /* 1 == from mem */
                                if ((dir == 0) && (dmode == 3)) break;
                                if ((dir == 1) && (dmode == 4)) break;

                                ushort data = r.ReadUInt16BE();
                                if (dmode == 4)
                                { /* dir == 0 if dmode == 4 !! */
                                  /* reverse bits in data */
                                    ushort temp = data;
                                    data = 0;
                                    for (int i = 0; i <= 15; ++i)
                                    {
                                        data = (ushort)((data >> 1) | (temp & 0x8000));
                                        temp = (ushort)(temp << 1);
                                    }
                                }

                                StringBuilder source_sb = new StringBuilder();
                                string dest_s;

                                /**** DATA LIST ***/

                                bool sommat = false;
                                byte[] rlist = new byte[11];
                                for (int i = 0; i <= 7; ++i)
                                {
                                    rlist[i + 1] = (byte)((data >> i) & 0x0001);
                                }
                                rlist[0] = 0;
                                rlist[9] = 0;
                                rlist[10] = 0;

                                for (int i = 1; i <= 8; ++i)
                                {
                                    if ((rlist[i - 1] == 0) && (rlist[i] == 1) &&
                                        (rlist[i + 1] == 1))
                                    {
                                        /* first reg in list */
                                        if (sommat)
                                            source_sb.Append("/");
                                        source_sb.Append($"D{i - 1}-");
                                        sommat = true;
                                    }
                                    else if (
                                        (rlist[i - 1] == 0) 
                                        && (rlist[i] == 1) 
                                        && (rlist[i + 1] == 0) 
                                        )
                                    {
                                        /* singleton */
                                        if (sommat)
                                            source_sb.Append("/");
                                        source_sb.Append($"D{i - 1}");
                                        sommat = true;
                                    }
                                    else if ((rlist[i] == 1) && (rlist[i + 1] == 0))
                                    {
                                        /* last in list */
                                        source_sb.Append($"D{i - 1}");
                                        sommat = true;
                                    }
                                }

                                /**** ADDRESS LIST ***/

                                for (int i = 8; i <= 15; ++i)
                                {
                                    rlist[i - 7] = (byte)((data >> i) & 0x0001);
                                }
                                rlist[0] = 0;
                                rlist[9] = 0;
                                rlist[10] = 0;

                                for (int i = 1; i <= 8; ++i)
                                {
                                    if ((rlist[i - 1] == 0) && (rlist[i] == 1) &&
                                        (rlist[i + 1] == 1))
                                    {
                                        /* first reg in list */
                                        if (sommat)
                                            source_sb.Append("/");
                                        source_sb.Append($"A{i - 1}-");
                                        sommat = true;
                                    }
                                    else if (
                                        (rlist[i - 1] == 0) 
                                        && (rlist[i] == 1) 
                                        && (rlist[i + 1] == 0)
                                        )
                                    {
                                        /* singleton */
                                        if (sommat)
                                            source_sb.Append("/");
                                        source_sb.Append($"A{i - 1}");
                                        sommat = true;
                                    }
                                    else if ((rlist[i] == 1) && (rlist[i + 1] == 0))
                                    {
                                        /* last in list */
                                        source_sb.Append($"A{i - 1}");
                                        sommat = true;
                                    }
                                }

                                opcode_s = $"movem.{size_arr[size]}";
                                dest_s = sprintmode(dmode, dreg, size, r, dicSyms);
                                if (dir == 0)
                                {
                                    /* the comma comes from the reglist */
                                    operand_s = $"{source_sb},{dest_s}";
                                }
                                else
                                {
                                    /* add the comma */
                                    operand_s = $"{dest_s},{source_sb}";
                                }
                                decoded = true;
                            }
                            break;
                        case 50:
                            {/* MOVEP */
                                byte dreg = getBits_0E00(word);
                                byte areg = getBits_0007(word);
                                byte size = (byte)(((word & 0x0040) >> 6) + 1);

                                ushort data = r.ReadUInt16BE();
                                opcode_s = $"movep.{size_arr[size]}";
                                if ((word & 0x0080) == 0)
                                {
                                    /* mem -> data reg */
                                    operand_s = $"${data:X4}(A{areg}),D{dreg}";
                                }
                                else
                                {
                                    /* data reg -> mem */
                                    operand_s = $"D{dreg},${data:X4}(A{areg})";
                                }
                                decoded = true;
                            }
                            break;
                        case 51:
                            { /* MOVEQ */
                                byte dreg = getBits_0E00(word);
                                opcode_s = "moveq";
                                operand_s = $"#{signedhex(word & 0x00FF, 2)},D{dreg}";
                                decoded = true;
                            }
                            break;
                        case 54: /* NBCD */
                        case 55:
                        case 56:
                        case 58:
                            { /* NEG, NEGX + NOT */
                                byte dmode = getmode(word);
                                byte dreg = getBits_0007(word);
                                byte size = getBits_00C0(word);

                                if (dmode == 1) break;
                                if (dmode >= 9) break;
                                if (size == 3) break;

                                switch (opnum)
                                {
                                    case 54:
                                        opcode_s = $"nbcd.{size_arr[size]}";
                                        break;
                                    case 55:
                                        opcode_s = $"neg.{size_arr[size]}";
                                        break;
                                    case 56:
                                        opcode_s = $"negx.{size_arr[size]}";
                                        break;
                                    case 58:
                                        opcode_s = $"not.{size_arr[size]}";
                                        break;
                                }
                                operand_s = sprintmode(dmode, dreg, size, r, dicSyms);
                                decoded = true;
                            }
                            break;
                        case 57:
                        case 62:
                        case 71:
                        case 72:
                        case 73:
                        case 76:
                        case 85:
                            { /* NOP, RESET, RTE, RTR, RTS, STOP, TRAPV */
                                switch (opnum)
                                {
                                    case 57:
                                        opcode_s = "nop";
                                        break;
                                    case 62:
                                        opcode_s = "reset";
                                        break;
                                    case 71:
                                        opcode_s = "rte";
                                        break;
                                    case 72:
                                        opcode_s = "rtr";
                                        break;
                                    case 73:
                                        opcode_s = "rts";
                                        break;
                                    case 76:
                                        opcode_s = "stop";
                                        operand_s = $"#${r.ReadUInt16BE():X2}";                                        
                                        break;
                                    case 85:
                                        opcode_s = "trapv";
                                        break;
                                }
                                decoded = true;
                            }
                            break;
                        case 61:
                            { /* PEA */
                                byte smode = getmode(word);
                                if (smode <= 1) break;
                                if ((smode == 3) || (smode == 4)) break;
                                if (smode >= 11) break;

                                opcode_s = "pea";
                                byte sreg = getBits_0007(word);
                                operand_s = sprintmode(smode, sreg, 0, r, dicSyms);
                                decoded = true;
                            }
                            break;
                        case 75:
                            {/* Scc */
                                byte dmode = getmode(word);
                                if (dmode == 1) break;
                                if (dmode >= 9) break;

                                byte dreg = getBits_0007(word);
                                int cc = getBits_0F00(word);

                                opcode_s = scc_tab[cc];
                                operand_s = sprintmode(dmode, dreg, 0, r, dicSyms);
                                decoded = true;
                            }
                            break;
                        case 82:
                            {/* SWAP */
                                byte dreg = getBits_0007(word);
                                opcode_s = "swap";
                                operand_s = $"D{dreg}";
                                decoded = true;
                            }
                            break;
                        case 83:
                            { /* TAS */
                                byte dmode = getmode(word);
                                byte dreg = getBits_0007(word);
                                if (dmode == 1) break;
                                if (dmode >= 9) break;

                                opcode_s = "tas ";
                                operand_s = sprintmode(dmode, dreg, 0, r, dicSyms);
                                decoded = true;
                            }
                            break;
                        case 84:
                            { /* TRAP */
                                byte dreg = (byte)(word & 0x000F);

                                if (specialAbi && dreg == 0)
                                {
                                    opcode_s = "break";
                                    decoded = true;
                                }
                                else if (specialAbi && dreg == 1)
                                {
                                    ushort x = r.ReadUInt16BE();
                                    uint xx = (uint)((x & 0x8000) << 2) | (uint)(x & 0x7FFF);
                                    opcode_s = "swi";
                                    operand_s = $"${xx:X6}";
                                    decoded = true;
                                }
                                else if (specialAbi && dreg == 2)
                                {
                                    uint xx = r.ReadUInt32BE() & 0x00FFFFFF;
                                    opcode_s = "swi";
                                    operand_s = $"${xx:X6}";
                                    decoded = true;
                                }
                                else {
                                    opcode_s = "trap";
                                    operand_s = $"#${dreg:X1}";
                                    decoded = true;
                                }
                            }
                            break;
                        case 86:
                            { /* TST */
                                byte dmode = getmode(word);
                                byte dreg = getBits_0007(word);
                                byte size = getBits_00C0(word);

                                if (dmode == 1) break;
                                if (dmode >= 9) break;
                                if (size == 3) break;

                                opcode_s = $"tst.{size_arr[size]}";
                                operand_s = sprintmode(dmode, dreg, size, r, dicSyms);
                                decoded = true;
                            }
                            break;
                        case 87:
                            {/* UNLK */
                                byte areg = getBits_0007(word);
                                opcode_s = "unlk";
                                operand_s = $"A{areg}";
                                decoded = true;
                            }
                            break;

                        default:
                            throw new System.Exception($"opnum out of range in switch (={opnum})");
                    }
                }
            }


            ushort len;
            if (!decoded)
            {
                opcode_s = "dc.w";
                operand_s = $"${word:X4}";
                len = 2;
            }
            else
            {
                len = (ushort)(r.PC - start_address);
            }

            StringBuilder sb = new StringBuilder();
            if (!string.IsNullOrEmpty(operand_s))
            {
                foreach (Match m in reHint.Matches(operand_s))
                {
                    if (sb.Length > 0)
                        sb.Append(" ");
                    sb.Append($"[{m.Groups[1].ToString()}]");
                }
                operand_s = reHint.Replace(operand_s, "");
            }
            


            return new DisRec() {
                Decoded = decoded,
                Mnemonic = opcode_s,
                Operands = operand_s,
                Hints = (sb.Length == 0)?null:sb.ToString(),
                Length = len
            };
        }


    }
}
