using DisassShared;
using DisassX86;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;

namespace TestDisass
{
    class Program
    {
        static void Usage(TextWriter wr, string message, int ExitCode)
        {
            wr.WriteLine("TestDisAss <type> <file> <base> [<symbols>]");
            if (!string.IsNullOrEmpty(message))
                wr.WriteLine(message);

            Environment.Exit(ExitCode);
        }

        static int Main(string[] args)
        {
            if (args.Length < 3)
                Usage(Console.Error, "Wrong number of arguments", 100);

            IDisAss disass = null;

            switch (args[0].ToUpper())
            {
                case "X86":
                    disass = new DisassX86.DisassX86(DisassX86.DisassX86.API.cpu_x86);
                    break;
                case "186":
                    disass = new DisassX86.DisassX86(DisassX86.DisassX86.API.cpu_186);
                    break;
                case "386":
                    disass = new DisassX86.DisassX86(DisassX86.DisassX86.API.cpu_386);
                    break;
                case "386_32":
                    disass = new DisassX86.DisassX86(DisassX86.DisassX86.API.cpu_386_32);
                    break;
                case "ARM":
                    disass = new DisassArm.DisassArm();
                    break;
                case "M68K":
                    disass = new Disass68k.Disass68k();
                    break;
                case "65816":
                    disass = new Disass65816.Disass65816();
                    break;
                default:
                    Usage(Console.Error, $"Unknown assembler \"{args[0]}\"", 102);
                    break;

            }

            Symbols symbols = new Symbols();

            if (args.Length >= 4)
            {
                string symfn = args[3];

                Regex reDEF = new Regex(@$"^\s*DEF\s+(\w+)\s+({disass.AddressFactory.AddressRegEx})", RegexOptions.IgnoreCase | RegexOptions.Compiled);

                TextReader f_sym = null;
                try
                {
                    f_sym = new StreamReader(symfn);
                }
                catch (Exception ex)
                {
                    Usage(Console.Error, $"Error reading symbol file \"{symfn}\" : {ex.ToString()}", 104);
                }
                using(f_sym)
                {
                    int lno = 1;
                    string l;
                    try {
                        while ((l = f_sym.ReadLine()) != null) {
                            var m = reDEF.Match(l);
                            if (m.Success)
                            {
                                symbols.Add(m.Groups[1].Value, disass.AddressFactory.Parse(m.Groups[2].Value), SymbolType.Pointer);
                            }

                            lno++;
                        }
                    } catch (Exception ex) { 
                        Usage(Console.Error, $"Error reading symbold file \"{symfn}\" at line {lno} : {ex.ToString()}", 105);
                    }
                }
            }

            byte[] Data = null;

            try
            {
                Data = File.ReadAllBytes(args[1]);
            }
            catch (Exception ex)
            {
                Usage(Console.Error, $"Error reading file \"{args[1]}\" : {ex.ToString()}", 101);
            }

            DisassAddressBase BaseAddress;

            try
            {
                BaseAddress = disass.AddressFactory.Parse(args[2]);
            }
            catch (Exception ex)
            {
                Usage(Console.Error, $"Bad base address {args[2]} : {ex}", 103);
                return -1;
            }

            DisassAddressBase PC = BaseAddress;

            DisassAddressBase dispc = BaseAddress;
            DisassAddressBase EndAddress = BaseAddress;



            using (var ms = new MemoryStream(Data))
            {
                var br = new BinaryReader(ms);

                var miss = new HashSet<DisRec2OperString_Number>();
                var missA = new HashSet<DisRec2OperString_Address>();

                bool ok = true;
                //first pass to autogen symbols
                while (ok)
                {

                    DisRec2<UInt32> instr;
                    try
                    {

                        instr = disass.Decode(br, dispc);

                        if (instr?.Operands != null)
                        {
                            //look for missing symbols and add to set to create later
                            miss.UnionWith(
                                instr.Operands.Where(i => i is DisRec2OperString_Number).Cast<DisRec2OperString_Number>()
                                .Where(i => i.SymbolType == SymbolType.Pointer || i.SymbolType == SymbolType.ServiceCall)
                                );
                            missA.UnionWith(
                                instr.Operands.Where(i => i is DisRec2OperString_Address).Cast<DisRec2OperString_Address>()
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
                        //TODO: DB: Sort out address forms for service calls for Arm, X86, 68K?
                        symbols.Add($"SWIX_{n.Number:X}", disass.AddressFactory.FromCanonical(n.Number), n.SymbolType);
                    }
                    else if (n.SymbolType == SymbolType.Pointer)
                    {
                        var ad = disass.AddressFactory.FromCanonical(n.Number);

                        if (ad >= BaseAddress && ad < EndAddress)
                            symbols.Add($"LX_{n.Number:X}", ad, n.SymbolType);
                        else
                            symbols.Add($"PX_{n.Number:X}", ad, n.SymbolType);
                    }
                }

                foreach (var n in missA)
                {
                    if (n.SymbolType == SymbolType.ServiceCall)
                    {
                        //TODO: DB: Sort out address forms for service calls for Arm, X86, 68K?
                        symbols.Add($"SWI_{n.Address}", n.Address, n.SymbolType);
                    }
                    else if (n.SymbolType == SymbolType.Pointer)
                    {
                        var ad = n.Address;

                        if (ad >= BaseAddress && ad < EndAddress)
                            symbols.Add($"L_{ad}", ad, n.SymbolType);
                        else
                            symbols.Add($"P_{ad}", ad, n.SymbolType);
                    }
                }

                foreach (var sn in symbols.All.OrderBy( x => x.Name))
                {
                    Console.WriteLine($"{sn.Name}\t\tequ\t{sn.Address}\t; {sn.SymbolType}");
                }


                dispc = BaseAddress;
                ok = true;
                ms.Position = 0;
                DisassAddressBase lastGood = BaseAddress;
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

                            Console.WriteLine($"{dispc:X8}\t{instr.Mnemonic}\t{ExpandSymbols(disass, symbols, instr.Operands)}\t{instr.Hints}\t{string.Join(" ", inst_bytes.Select(b => $"{b:X2}"))} {string.Join("", inst_bytes.Select(b => (b > 32 && b < 128) ? (char)b : ' '))}");

                            lastGood = dispc + instr.Length;
                            lastGood_p = ms.Position;

                        }
                        else
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

        static string ExpandSymbols(IDisAss disass, Symbols symbols, IEnumerable<DisRec2OperString_Base> oper)
        {
            StringBuilder sb = new StringBuilder();
            if (oper != null)
            {
                foreach (var o in oper)
                {
                    var n = o as DisRec2OperString_Address;
                    if (n != null)
                    {
                        var s = symbols.GetByAddress(n.Address, n.SymbolType).FirstOrDefault();
                        if (s != null)
                            sb.Append(s.Name);
                        else
                            sb.Append(n.ToString());
                    }
                    else
                    {
                        var n2 = o as DisRec2OperString_Number;
                        if (n2 != null)
                        {
                            var s = symbols.GetByAddress(disass.AddressFactory.FromCanonical(n2.Number), n2.SymbolType).FirstOrDefault();
                            if (s != null)
                                sb.Append(s.Name);
                            else
                                sb.Append(n2.ToString());

                        }
                        else
                            sb.Append(o.ToString());
                    }
                }
            }
            return sb.ToString();
        }
    }
}
