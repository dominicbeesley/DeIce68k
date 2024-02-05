using Disass65816;
using System.Text;
using System.Text.RegularExpressions;

namespace TestEmuDormann
{
    class Program
    {
        static void Usage(TextWriter wr, string message, int ExitCode, Exception ex = null)
        {
            if (ex != null)
            {
                wr.WriteLine(ex.ToString());
            }

            wr.WriteLine("""
TestEmuDormann [options] <binary>

Assumes to load binary at offset 0 and run at 400 with registers
set as:
E,MS,XS = 1
PB,PC = 0,0400
DB = 0
SH = 1
A,X,Y,B,SL = -1
All flags = Unknown

"""
);
            if (!string.IsNullOrEmpty(message))
                wr.WriteLine(message);

            Environment.Exit(ExitCode);
        }

        static int [] memory = new int[65536];

        private static bool getmem4(int index, byte[] ret)
        {
            for (int i = 0; i < 4; i++)
            {
                var x = memory[index++ & 0xFFFF];
                if (x < 0)
                    return false;
                ret[i] = (byte)x;   
            }
            return true;
        }

        static int Main(string[] args)
        {
            if (args.Length < 1)
                Usage(Console.Error, "Wrong number of arguments", 100);

            Array.Fill(memory, -1);

            int offset = 0;

            FileStream fsbin = null;
            try
            {
                fsbin = new FileStream(args[0], FileMode.Open, FileAccess.Read);
            } catch (Exception ex)
            {
                Usage(Console.Error, $"Cannot open {args[0]} for input", -1, ex);
            }
            using (fsbin)
            {
                byte[] buf = new byte[65536];
                int len = fsbin.Read(buf, 0, 65536);
                if (len <= 0)
                {
                    Usage(Console.Error, "Empty binary file", -2);
                }
                int i = 0;
                while (i < len)
                {
                    memory[offset++] = buf[i++];
                }
            }

            em65816 em = new em65816()
            {
                memory_write = (ea, val) => { memory[ea & 0xFFFF] = val; },
                memory_read = (ea) => { return memory[ea & 0xFFFF]; }
            };
            var regs = new em65816.Registers(em);
            regs.E = em65816.Tristate.True;
            regs.XS = em65816.Tristate.True;
            regs.MS = em65816.Tristate.True;
            regs.I = em65816.Tristate.True;


            regs.PB = 0;
            regs.PC = 0x400;
            regs.DB = 0;
            regs.DP = 0;
            regs.SH = 1;

            Disass65816.Disass65816 disass = new Disass65816.Disass65816();

            bool done = false;
            int instructions = 0;
            byte[] pdata = new byte[4];
            do
            {
                if (regs.PC < 0)
                {
                    Console.Error.WriteLine($"END: Bad program counter");
                    return -5;
                }
                if (getmem4(regs.PC, pdata)) {

                    using (var ms = new MemoryStream(pdata))
                    using (var br = new BinaryReader(ms))
                    {
                        Console.Write($"{regs.PC:X4} ");

                        var dis = disass.Decode(br, new Address65816_abs((uint)regs.PC));

                        for (int i = 0; i < dis.Length; i++) 
                            Console.Write($"{pdata[i]:X02} ");
                        for (int i = dis.Length; i < 4; i++)
                            Console.Write("   ");

                        Console.Write(dis.ToString());
                    }

                    Console.WriteLine();

                    em65816.instruction_t instr;
                    var regsNext = em.em_65816_emulate(pdata, regs, out instr);
                    var regsN0 = regsNext.FirstOrDefault();
                    if (regsN0 == null)
                    {
                        Console.Error.WriteLine($"Instruction at {regs.PC:X4} failed to emulate");
                        return -4;
                    }
                    if (regsN0.PC == regs.PC)
                    {
                        Console.Error.WriteLine($"Stuck at PC={regs.PC:X4} - end of Dormann tests");
                        return -6;
                    }
                    Console.Write(regsN0.ToString());

                    regs = regsN0;

                    instructions++;
                    if (instructions > 10000000)
                        done = true;
                } 
                else
                {
                    Console.Error.WriteLine($"END: Bad program data at {regs.PC:X4}");
                    return -3;
                }
                Console.WriteLine();
            } while (!done);


            return 0;
        }

    }
}

