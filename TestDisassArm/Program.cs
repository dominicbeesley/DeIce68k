using DisassShared;
using DisassX86;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace TestDisass
{
    class Program
    {
        static void Usage(TextWriter wr, string message, int ExitCode)
        {
            wr.WriteLine("TestDisAss <type> <file> <base>");
            if (!string.IsNullOrEmpty(message))
                wr.WriteLine(message);

            Environment.Exit(ExitCode);
        }

        static int Main(string[] args)
        {
            if (args.Length != 3)
                Usage(Console.Error, "Wrong number of arguments", 100);

            IDisAss disass = null;

            switch (args[0])
            {
                case "X86": 
                    disass = new DisassX86.DisassX86();
                    break;
                case "ARM":
                    disass = new DisassArm.DisassArm();
                    break;
                case "M68K":
                    disass = new Disass68k.Disass();
                    break;
                default:
                    Usage(Console.Error, $"Unknown assembler \"{args[0]}\"", 102);
                    break;

            }

            Symbols symbols = new Symbols();

            byte[] Data = null;

            try
            {
                Data = File.ReadAllBytes(args[1]);
            } catch (Exception ex)
            {
                Usage(Console.Error, $"Error reading file \"{args[1]}\" : {ex.ToString()}", 101);
            }

            UInt32 BaseAddress = 0;

            try
            {
                BaseAddress = Convert.ToUInt32(args[2], 16);
            } catch (Exception)
            {
                Usage(Console.Error, $"Bad base address {args[2]}", 103);
            }

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

                        instr = disass.Decode(br, dispc);

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
                UInt32 lastGood = BaseAddress;
                long lastGood_p = 0;
                while (ok)
                {
                    foreach (var label in symbols.GetByAddress(dispc, SymbolType.Pointer))
                    {
                        Console.WriteLine($"{label.Name}:");
                    }

                    var p = ms.Position;

                    DisRec2<UInt32> instr;
                    try
                    {

                        instr = disass.Decode(br, dispc);
                    }
                    catch (EndOfStreamException)
                    {
                        ok = false;
                        instr = null;
                    }

                    if (!ok || instr.Decoded)
                    {
                        var dispc2 = lastGood;
                        while (lastGood_p < p)
                        {
                            ms.Position = lastGood_p;

                            var ll = p - lastGood_p;
                            if (ll > 8)
                                ll = 8;
                            byte[] skip_bytes = new byte[ll];
                            ms.Read(skip_bytes, 0, (int)ll);

                            Console.WriteLine($"{dispc2:X8}\t\t\t\t{string.Join(" ", skip_bytes.Select(b => $"{b:X2}"))} {string.Join("", skip_bytes.Select(b => (b > 32 && b < 128) ? (char)b : ' '))}");

                            dispc2 += (UInt32)ll;
                            lastGood_p += ll;
                        }
                    }


                    if (instr != null && ok)
                    {

                        if (instr.Decoded)
                        {

                            ms.Position = p;

                            byte[] inst_bytes = new byte[instr.Length];
                            ms.Read(inst_bytes, 0, instr.Length);

                            Console.WriteLine($"{dispc:X8}\t{instr.Mnemonic}\t{ExpandSymbols(symbols, instr.Operands)}\t{instr.Hints}\t{string.Join(" ", inst_bytes.Select(b => $"{b:X2}"))} {string.Join("", inst_bytes.Select(b => (b > 32 && b < 128) ? (char)b : ' '))}");

                            lastGood = dispc + instr.Length;
                            lastGood_p = ms.Position;

                        } else
                        {
                            ms.Position = p + instr.Length;
                        }

                        dispc += instr.Length;
                    }
                    else
                    {
                        ok = false;
                    }
                }
            }

            return 0;

        }

        static string ExpandSymbols(Symbols symbols, IEnumerable<DisRec2OperString_Base> oper)
        {
            StringBuilder sb = new StringBuilder();
            if (oper != null)
            {
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
                    }
                    else
                    {
                        sb.Append(o.ToString());
                    }
                }
            }
            return sb.ToString();
        }
    }
}
