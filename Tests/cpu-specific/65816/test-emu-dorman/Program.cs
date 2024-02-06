using Disass65816;
using Disass65816.Emulate;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Text.RegularExpressions;
using System.Transactions;

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

Options:
--log <log> log to output
-lw         log writes
-lr         log reads

Description:

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
            bool logreads = false;
            bool logwrites = false;
            TextWriter log = null;

            int i = 0;
            var nextarg = () =>
            {
                if (i >= args.Length - 1)
                    Usage(Console.Error, "Too few arguments", -1);

                return args[i++];
            };

            while (args[i].StartsWith("-"))
            {
                var sw = args[i++];

                if (sw == "--log")
                {
                    var fn = nextarg();
                    try
                    {
                        log = new StreamWriter(fn);
                    } catch (Exception ex)
                    {
                        Usage(Console.Error, $"Cannot open log file \"{fn}\" for output", -1, ex);
                    }
                } else if (sw == "-lw")
                {
                    logwrites = true;
                }
                else if (sw == "-lr")
                {
                    logreads = true;
                }
            }

            if (args.Length < i + 1)
                Usage(Console.Error, "Wrong number of arguments", 100);

            Array.Fill(memory, -1);

            int offset = 0;

            FileStream fsbin = null;
            try
            {
                fsbin = new FileStream(args[i], FileMode.Open, FileAccess.Read);
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
                i = 0;
                while (i < len)
                {
                    memory[offset++] = buf[i++];
                }
            }
            try
            {
                Emulate65816 em = new Emulate65816()
                {
                    memory_write = (ea, val) =>
                    {
                        memory[ea & 0xFFFF] = val;
                        if (logwrites)
                            log?.WriteLine($"WR:{ea:X4}<={val:X0}");
                    },
                    memory_read = (ea) =>
                    {
                        var val = memory[ea & 0xFFFF];
                        if (logreads)
                            log?.WriteLine($"RD:{ea:X4}=>{val:X0}");
                        return val;
                    }
                };
                IRegsEmu65816 regs = new Emulate65816.Registers(em);
                regs.E = true;
                regs.XS = true;
                regs.MS = true;
                regs.I = true;


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
                    if (getmem4(regs.PC, pdata))
                    {

                        using (var ms = new MemoryStream(pdata))
                        using (var br = new BinaryReader(ms))
                        {
                            log?.Write($"{regs.PC:X4} ");

                            var dis = disass.Decode(br, new Address65816_abs((uint)regs.PC));

                            for (i = 0; i < dis.Length; i++)
                                log?.Write($"{pdata[i]:X02} ");
                            for (i = dis.Length; i < 4; i++)
                                log?.Write("   ");

                            log?.Write(dis.ToString());
                        }

                        log?.WriteLine();

                        Emulate65816.instruction_t instr;
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
                        log?.Write(regsN0.ToString());

                        regs = regsN0;

                        instructions++;
                        //                    if (instructions > 100000000)
                        //                        done = true;
                        if ((instructions & 0xFFFFF) == 0)
                            Console.WriteLine(".");
                    }
                    else
                    {
                        Console.Error.WriteLine($"END: Bad program data at {regs.PC:X4}");
                        return -3;
                    }
                    log?.WriteLine();
                } while (!done);


                return 0;
            } finally
            {
                if (log != null)
                {
                    log.Flush();
                    log.Close();
                    log.Dispose();
                    log = null;
                }
            }
        }

    }
}

