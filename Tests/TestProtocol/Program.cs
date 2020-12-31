using System;
using DossySerialPort;
using System.IO;
using DeIceProtocol;

namespace TestProtocol
{
    class Program
    {
        const int E_OK = 0;
        const int E_PARAMS = 1;
        const int E_UNEX = 100;

        static void Usage(TextWriter tw, string message)
        {
            tw.WriteLine(@"
TestProtocol <COMPORT> <baudrate>

A debug target needs to be connected and in a ready state (not running), the 
current PC and following instructions will be dumped to console.
");
            if (message is not null)
                tw.WriteLine(message);

        }

        static int Main(string[] args)
        {
            try
            {
                string comPort;
                int baudRate;

                if (args.Length != 2)
                {
                    Usage(Console.Error, "Incorrect Parameters");
                    return E_PARAMS;
                }

                comPort = args[0];
                if (!int.TryParse(args[1], out baudRate))
                {
                    Usage(Console.Error, "Incorrect Parameters : bad baud rate");
                    return E_PARAMS;
                }

                using var _port = new DossySerial(comPort, baudRate);

                var _proto = new DeIceProtocolMain(_port);
                var regs = _proto.SendReqExpectReply<DeIceFnReplyReadRegs>(new DeIceFnReqReadRegs());

                Console.WriteLine($"TARGET STATUS: {regs.Registers.TargetStatus:X2}");

                Console.WriteLine("Registers:");
                Console.WriteLine(regs.Registers.PrettyString());

                return E_OK;
            } catch (Exception ex)
            {
                Console.Error.WriteLine($"Unexepcted Error: { ex.Message }");
                Console.Error.WriteLine(ex.ToString());
                return E_UNEX;
            }
        }
    }
}
