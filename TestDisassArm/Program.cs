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

            ISymbols2<UInt32> symbols = new Symbols();

            byte[] Data = File.ReadAllBytes(@"E:\Users\dominic\GitHub\b-em\roms\tube\ARMeval_100.rom");

            UInt32 BaseAddress = 0x0;
            UInt32 PC = BaseAddress;

            uint dispc = BaseAddress;



            bool ok = true;
            bool first = true;
            using (var ms = new MemoryStream(Data))
            {
                while (ok)
                {
                    bool hassym = false;
                    foreach (var label in symbols.GetByAddress(dispc))
                    {
                        Console.WriteLine($"{label.Name}:");
                        hassym = true;
                    }

                    var p = ms.Position;

                    var br = new BinaryReader(ms);
                    DisRec2<UInt32> instr;
                    try
                    {

                        instr = DisassArm.DisassArm.Decode(br, dispc, symbols);
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

                            Console.WriteLine($"{dispc:X8}\t{instr.Mnemonic}\t{string.Join("", instr.Operands)}\t{instr.Hints}\t{string.Join(" ", inst_bytes.Select(b => $"{b:X2}"))} {string.Join("", inst_bytes.Select(b => (b > 32 && b < 128) ? (char)b : ' '))}");
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
    }
}
