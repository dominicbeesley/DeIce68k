
using DisassShared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DisassArm
{
    public static class DisassArm
    {
        private static DisRec2<UInt32> Undefined { get => new DisRec2<UInt32> { Decoded = false, Length = 4 }; }

        public static DisRec2<UInt32> Decode(BinaryReader br, UInt32 pc, ISymbols2<UInt32> symbols, bool createSymbols)
        {

            UInt32 opcode = br.ReadUInt32();
            string cond = DecodeCond(opcode);

            if ((opcode & 0x0E000000) == 0x0A000000)
                return DecodeBranch(cond, opcode, pc, symbols, createSymbols);
            else if ((opcode & 0x0FC00090) == 0x00000090)
                return DecodeMul(cond, opcode, pc, symbols, createSymbols);
            else if ((opcode & 0x0C000000) == 0x00000000)
                return DecodeAlu(cond, opcode, pc, symbols, createSymbols);
            else if ((opcode & 0x0C000000) == 0x04000000)
                return DecodeLdrStr(cond, opcode, pc, symbols, createSymbols);
            else if ((opcode & 0x0E000000) == 0x08000000)
                return DecodeLdmStm(cond, opcode, pc, symbols, createSymbols);
            else if ((opcode & 0x0F000000) == 0x0F000000)
                return DecodeSwi(cond, opcode, pc, symbols, createSymbols);
            else
                return new DisRec2<UInt32>
                {
                    Decoded = false,
                    Length = 4
                };            
        }

        private static DisRec2<UInt32> DecodeSwi(string cond, UInt32 opcode, UInt32 pc, ISymbols2<UInt32> symbols, bool createSymbols)
        {
            return new DisRec2<UInt32>
            {
                Decoded = true,
                Mnemonic = $"swi{cond}",
                Operands = new [] { SymFind(symbols, opcode & 0xFFFFFF, createSymbols) },
                Hints = "",
                Length = 4,
            };

        }

        private static readonly string[] m_modes = { "da", "ia", "db", "ib"};


        public static IEnumerable<Tuple<int, int>> GroupConsecutive(this IEnumerable<int> source)
        {
            using (var e = source.GetEnumerator())
            {
                for (bool more = e.MoveNext(); more;)
                {
                    int first = e.Current, last = first, next;
                    while ((more = e.MoveNext()) && (next = e.Current) > last && next - last == 1)
                        last = next;
                    yield return new Tuple<int,int>( first, last );
                }
            }
        }

        private static DisRec2<UInt32> DecodeLdmStm(string cond, UInt32 opcode, UInt32 pc, ISymbols2<UInt32> symbols, bool createSymbols)
        {
            int puflag = (int)(opcode & 0x01800000) >> 23;
            bool sflag = (opcode & 0x00400000) != 0;
            bool wflag = (opcode & 0x00200000) != 0;
            bool lflag = (opcode & 0x00100000) != 0;
            int rnix = (int)(opcode & 0xF0000) >> 16;
            string Rn = Reg(rnix);

            string op = $"{(lflag ? "ldm" : "stm")}{cond}{m_modes[puflag]}";

            string regs = string.Join(",", 
                Enumerable.Range(0, 15)
                .Where(i => (opcode & 1 << i) != 0)
                .GroupConsecutive()
                .Select(i => 
                    (i.Item1 == i.Item2)
                        ?Reg(i.Item1)
                        :i.Item2 == i.Item1+1
                            ?$"{Reg(i.Item1)},{Reg(i.Item2)}"
                            :$"{Reg(i.Item1)}-{Reg(i.Item2)}"
                ));

            return new DisRec2<UInt32>
            {
                Decoded = true,
                Mnemonic = op,
                Operands = new[] { new DisRec2OperString<UInt32> { Text = $"{Rn}{(wflag ? "!" : "")},{{{regs}}}{(sflag ? "^" : "")}" } },
                Hints = "",
                Length = 4,
            };

        }
        private static DisRec2<UInt32> DecodeLdrStr(string cond, UInt32 opcode, UInt32 pc, ISymbols2<UInt32> symbols, bool createSymbols)
        {
            bool iflag = (opcode & 0x02000000) != 0;
            bool pflag = (opcode & 0x01000000) != 0;
            bool uflag = (opcode & 0x00800000) != 0;
            bool bflag = (opcode & 0x00400000) != 0;
            bool wflag = (opcode & 0x00200000) != 0;
            bool tflag = !pflag && wflag;
            wflag = wflag & !tflag;
            bool lflag = (opcode & 0x00100000) != 0;
            int rnix = (int)(opcode & 0xF0000) >> 16;
            int rdix = (int)(opcode & 0xF000) >> 12;

            string op = $"{(lflag?"ldr":"str")}";
            string Rd = Reg(rdix);
            string Rn = Reg(rnix);

            IEnumerable<DisRec2OperString<UInt32>> mem;

            if (rnix == 15 & !iflag & !wflag & pflag)
            {
                UInt32 addr = uflag ? pc + 8 + (opcode & 0xFFF) : pc + 8 - (opcode & 0xFFF);
                    mem = new[] { SymFind(symbols, addr, createSymbols) };
            } else
            {
                if (!iflag)
                {
                    UInt32 offs = opcode & 0xFFF;

                    if (offs == 0)
                    {
                        mem = OperStr( $"[{Rn}]" );
                    } else
                    {
                        if (pflag)
                            mem = OperStr($"[{Rn},#{(uflag ? "" : "-")}{Hex(offs)}]{(wflag?"!":"")}");
                        else
                            mem = OperStr($"[{Rn}],#{(uflag ? "" : "-")}{Hex(offs)}");
                    }
                } 
                else
                {
                    //illegal check
                    if ((opcode & 0x10) != 0)
                        return Undefined;

                    int stix = (int)(opcode & 0x60) >> 5;
                    int sha = (int)(opcode & 0xF80) >> 7;
                    string Shi = ShiftDecode(sha, stix);
                    string Rm = $"{Reg((int)opcode & 0xF)}{(Shi!=null?",":"")}{Shi}";
                    
                    if (pflag)
                        mem = OperStr($"[{Rn},{(uflag ? "" : "-")}{Rm}]{(wflag ? "!" : "")}");
                    else
                        mem = OperStr($"[{Rn}],{(uflag ? "" : "-")}{Rm}");

                }

            }

            return new DisRec2<UInt32>
            {
                Decoded = true,
                Mnemonic = $"{op}{cond}{(bflag ? "b" : "")}{(tflag ? "t" : "")}",
                Operands = OperStr($"{Rd},").Concat(mem),
                Hints = "",
                Length = 4,
            };

        }


        private static (string, bool, bool) DecodeAluOp(int opix)
        {
            switch (opix)
            {
                case 0x0:
                    return ("and", true, true);
                case 0x1:
                    return ("eor", true, true);
                case 0x2:
                    return ("sub", true, true);
                case 0x3:
                    return ("rsb", true, true);
                case 0x4:
                    return ("add", true, true);
                case 0x5:
                    return ("adc", true, true);
                case 0x6:
                    return ("sbc", true, true);
                case 0x7:
                    return ("rsc", true, true);
                case 0x8:
                    return ("tst", false, true);
                case 0x9:
                    return ("teq", false, true);
                case 0xA:
                    return ("cmp", false, true);
                case 0xB:
                    return ("cmn", false, true);
                case 0xC:
                    return ("orr", true, true);
                case 0xD:
                    return ("mov", true, false);
                case 0xE:
                    return ("bic", true, true);
                default:
                    return ("mvn", true, false);
            }
        }

        private static DisRec2<UInt32> DecodeAlu(string cond, UInt32 opcode, UInt32 pc, ISymbols2<UInt32> symbols, bool createSymbols)
        {

            int opix = (int)(opcode & 0x01E00000) >> 21;
            (string op, bool hasRd, bool hasOp1) = DecodeAluOp(opix);

            bool sflag = (opcode & 0x00100000) != 0;
            string sorp = (sflag ? "s" : "");
            int rdix = (int)(opcode & 0xF000) >> 12;
            string Rd = Reg(rdix);
            int rnix = (int)(opcode & 0xF0000) >> 16;
            string Rn = Reg(rnix);
            string Op2=null;
            string Shi=null;

            if (!hasRd)
            {
                if (rdix == 15 & sflag)
                {
                    sorp = "p";
                } else
                {
                    sorp = null;
                }
                Rd = null;
            }
            

            if ((opcode & 0x02000000) != 0)
            {
                //op2 is immed
                UInt32 imm = Ror32(opcode & 0xFF, (opcode & 0xF00) >> 7);
                Op2 = $"#{Hex(imm)}";

                //special case for ADR
                if (rnix == 15 && !sflag && opix == 2)
                    return new DisRec2<UInt32>
                    {
                        Decoded = true,
                        Mnemonic = $"adr",
                        Operands = new[] { SymFind(symbols, pc + 8 - imm, createSymbols) },
                        Hints = "",
                        Length = 4
                    };
                else if (rnix == 15 && !sflag && opix == 4)
                    return new DisRec2<UInt32>
                    {
                        Decoded = true,
                        Mnemonic = $"adr",
                        Operands = new[] { SymFind(symbols, pc + 8 + imm, createSymbols) },
                        Hints = "",
                        Length = 4
                    };
            }
            else
            {
                string Rm = Reg((int)opcode & 0xF);
                int stix = (int)(opcode & 0x60) >> 5;

                if ((opcode & 0x00000010) != 0)
                {
                    if ((opcode & 0x80) != 0)
                        return Undefined;

                    //shift by reg
                    string Rs = $"R{(opcode & 0xF00) >> 8}";
                    Op2 = $"{Rm}";
                    string St = ShiftType(stix);
                    Shi = $"{St} {Rs}";
                } 
                else
                {

                    if ((opcode & 0x10) != 0)
                        return Undefined;

                    Op2 = $"{Rm}";

                    int sha = (int)((opcode & 0xF80) >> 7);

                    Shi = ShiftDecode(sha, stix);

                }
            }

            if (!hasOp1)
                Rn = null;


            return new DisRec2<UInt32>
            {
                Decoded = true,
                Mnemonic = $"{op}{cond}{sorp}",
                Operands = OperStr(string.Join(',', new [] {Rd,Rn,Op2,Shi}.Where(x => x != null))),
                Hints = "",
                Length = 4
            };


        }

        private static DisRec2<UInt32> DecodeMul(string cond, UInt32 opcode, UInt32 pc, ISymbols2<UInt32> symbols, bool createSymbols)
        {

            bool sflag = (opcode & 0x00100000) != 0;
            string sorp = (sflag ? "s" : "");
            int rdix = (int)(opcode & 0xF0000) >> 16;
            string Rd = Reg(rdix);
            string Rn = Reg((int)(opcode & 0xF000) >> 12);
            string Rs = Reg((int)(opcode & 0xF00) >> 8);
            string Rm = Reg((int)(opcode & 0xF));
            bool mla = (opcode & 0x00200000) != 0;

            string op;
            if (mla)
            {
                op = "mla";

            } else
            {
                op = "mul";
                Rn = null;
            }

            return new DisRec2<UInt32>
            {
                Decoded = true,
                Mnemonic = $"{op}{cond}{sorp}",
                Operands = OperStr(string.Join(',', new[] { Rd, Rm, Rs, Rn}.Where(x => x != null))),
                Hints = "",
                Length = 4,
            };

        }

        private static string ShiftType(int s)
        {
            switch(s)
            {
                case 0:
                    return "lsl";
                case 1:
                    return "lsr";
                case 2:
                    return "asr";
                case 3:
                    return "ror";
                default:
                    return "??";
            }
        }

        private static string ShiftDecode(int sha, int stix)
        {
            if (sha == 0)
            {
                switch (stix)
                {
                    case 1:
                        return "lsr #32";
                    case 2:
                        return "asr #32";
                    case 3:
                        return "rrx";
                    default:
                        return null;
                }

            }
            else
                return $"{ShiftType(stix)} #{sha}";

        }

        private static UInt32 Ror32(UInt32 val, uint n)
        {
            int nn = (int)n;
            int v2 = (int)val;
            v2 = (v2 >> nn) | (v2 << 32 - nn);
            return (UInt32)v2;
        }

        private static DisRec2<UInt32> DecodeBranch(string cond, UInt32 opcode, UInt32 pc, ISymbols2<UInt32> symbols, bool createSymbols)
        {
            UInt32 dest = (pc + 8 + ((opcode & 0xFFFFFF) << 2)) & 0x3FFFFFF;

            bool lflag = (opcode & 0x01000000) != 0;

            return new DisRec2<UInt32>
            {
                Decoded = true,
                Mnemonic = $"b{(lflag ? "l" : "")}{cond}",
                Operands = new[] { SymFind(symbols, dest, createSymbols) },
                Hints = "",
                Length = 4,
            };
        }

        private static string Reg(int ix)
        {
            if (ix == 15)
                return "pc";
            else if (ix == 14)
                return "lr";
            else
                return $"r{ix}";
        }

        private static string Hex(UInt32 x, int digits=0)
        {
            if (digits > 0)
            {
                return "0x" + x.ToString("X" + digits);
            } else
            {
                if (x < 10)
                    return x.ToString();
                else
                    return "0x" + x.ToString("X");
            }
        }

        private static string DecodeCond(UInt32 opcode)
        {
            switch ((opcode & 0xF0000000) >> 28)
            {
                case 0x0:
                    return "eq";
                case 0x1:
                    return "ne";
                case 0x2:
                    return "cs";
                case 0x3:
                    return "cc";
                case 0x04:
                    return "mi";
                case 0x05:
                    return "pl";
                case 0x6:
                    return "vs";
                case 0x7:
                    return "vc";
                case 0x8:
                    return "hi";
                case 0x9:
                    return "ls";
                case 0xa:
                    return "ge";
                case 0xb:
                    return "lt";
                case 0xc:
                    return "gt";
                case 0xd:
                    return "le";
                case 0xe:
                    return "";
                case 0xf:
                    return "nv";
                default:
                    return "??";
            
            }
        }

        private static DisRec2OperString<UInt32>SymFind(ISymbols2<UInt32> symbols, UInt32 addr, bool createSymbols)
        {
            var sym = symbols?.GetByAddress(addr).FirstOrDefault();
            if (sym == null)
            {
                sym = symbols.Add($"L_{addr:X8}", addr);
            }
            return new DisRec2OperString<UInt32>{ Text = Hex(addr), Symbol = sym };
        }

        private static IEnumerable<DisRec2OperString<UInt32>> OperStr(string str)
        {
            return new[] { new DisRec2OperString<UInt32> { Text = str } };
        }
    }

}
