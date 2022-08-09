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

            IDisassSymbols symbols = new Symbols();

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
                    foreach (var label in symbols.AddressToSymbols(dispc))
                    {
                        Console.WriteLine($"{label}:");
                        hassym = true;
                    }

                    var p = ms.Position;

                    var br = new BinaryReader(ms);
                    DisRec instr;
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

                        ms.Position = p;

                        byte[] inst_bytes = new byte[instr.Length];
                        ms.Read(inst_bytes, 0, instr.Length);
                        Console.WriteLine($"{dispc:X8}\t{instr.Mnemonic}\t{instr.Operands}\t{instr.Hints}\t{string.Join(" ",inst_bytes.Select(b => $"{b:X2}"))}");

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
