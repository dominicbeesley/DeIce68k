using DisassShared;
using DisassArm;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestDisassArm
{
    class Program
    {
        static void Main(string[] args)
        {

            Symbols symbols = new Symbols();

            byte[] Data = File.ReadAllBytes(@"E:\Users\dominic\GitHub\b-em\roms\tube\ARMeval_100.rom");

            UInt32 BaseAddress = 0x0;
            UInt32 PC = BaseAddress;

            UInt32 dispc = BaseAddress;
            UInt32 EndAddress = BaseAddress;



            using (var ms = new MemoryStream(Data))
            {
                var br = new BinaryReader(ms);

                var miss = new HashSet<DisRec2OperString_Number>();

                bool ok = true;
                //first pass to autogen symbols
                while (ok)
                {

                    DisRec2<UInt32> instr;
                    try
                    {

                        instr = DisassArm.DisassArm.Decode(br, dispc, symbols, true);

                        if (instr?.Operands != null) {
                            //look for missing symbols and add to set to create later
                            miss.UnionWith(
                                instr.Operands.Where(i => i is DisRec2OperString_Number).Cast<DisRec2OperString_Number>()
                                .Where(i => i.SymbolType == SymbolType.Pointer || i.SymbolType == SymbolType.ServiceCall)
                                );
                        }

                    }
                    catch (EndOfStreamException)
                    {
                        ok = false;
                        continue;
                    }

                    if (instr != null)
                    {
                        dispc += instr.Length;
                    }
                    else
                    {
                        ok = false;
                    }
                }
                EndAddress = dispc;

                foreach (var n in miss)
                {
                    if (n.SymbolType == SymbolType.ServiceCall)
                    {
                        symbols.Add($"SWI_{n.Number:X}", n.Number, n.SymbolType);
                    } else if (n.SymbolType == SymbolType.Pointer)
                    {
                        if (n.Number >= BaseAddress && n.Number < EndAddress)
                            symbols.Add($"L_{n.Number:X}", n.Number, n.SymbolType);
                        else
                            symbols.Add($"P_{n.Number:X}", n.Number, n.SymbolType);
                    }
                }

                dispc = BaseAddress;
                ok = true;
                ms.Position = 0;
                bool first = true;
                while (ok)
                {
                    bool hassym = false;
                    foreach (var label in symbols.GetByAddress(dispc, SymbolType.Pointer))
                    {
                        Console.WriteLine($"{label.Name}:");
                        hassym = true;
                    }

                    var p = ms.Position;

                    DisRec2<UInt32> instr;
                    try
                    {

                        instr = DisassArm.DisassArm.Decode(br, dispc, symbols, false);
                    }
                    catch (EndOfStreamException)
                    {
                        ok = false;
                        continue;
                    }

                    if (instr != null)
                    {

                        if (instr.Decoded)
                        {
                            ms.Position = p;

                            byte[] inst_bytes = new byte[instr.Length];
                            ms.Read(inst_bytes, 0, instr.Length);

                            Console.WriteLine($"{dispc:X8}\t{instr.Mnemonic}\t{ExpandSymbols(symbols, instr.Operands)}\t{instr.Hints}\t{string.Join(" ", inst_bytes.Select(b => $"{b:X2}"))} {string.Join("", inst_bytes.Select(b => (b > 32 && b < 128) ? (char)b : ' '))}");
                        } else
                        {
                            ms.Position = p;
                            int l = instr.Length;
                            
                            while (l >= 4)
                            {
                                UInt32 v = br.ReadUInt32();
                                byte[] inst_bytes = BitConverter.GetBytes(v);
                                Console.WriteLine($"{dispc:X8}\t.ualong\t{v:X8}\t{instr.Hints}\t{string.Join(" ", inst_bytes.Select(b => $"{b:X2}"))} {string.Join("", inst_bytes.Select(b => (b > 32 && b < 128) ? (char)b : ' '))}");
                                l -= 4;
                            }

                            while (l >= 1)
                            {
                                byte v = br.ReadByte();
                                byte[] inst_bytes = new[] { v };
                                Console.WriteLine($"{dispc:X8}\t.ualong\t0x{v:X8}\t{instr.Hints}\t{string.Join(" ", inst_bytes.Select(b => $"{b:X2}"))} {string.Join("", inst_bytes.Select(b => (b > 32 && b < 128) ? (char)b : ' '))}");
                                l -= 4;
                            }

                        }

                        dispc += instr.Length;
                    }
                    else
                    {
                        ok = false;
                    }
                }
            }

        }

        static string ExpandSymbols(Symbols symbols, IEnumerable<DisRec2OperString_Base> oper)
        {
            StringBuilder sb = new StringBuilder();
            foreach (var o in oper)
            {
                if (o is DisRec2OperString_Number)
                {
                    var n = (DisRec2OperString_Number)o;
                    var s = symbols.GetByAddress(n.Number, n.SymbolType).FirstOrDefault();
                    if (s != null)
                        sb.Append(s.Name);
                    else
                        sb.Append(n.ToString());
                } else
                {
                    sb.Append(o.ToString());
                }
            }
            return sb.ToString();
        }
    }
}
