// A simple net-cat like program that given a com/host/port will 
// connect to a telnet (or other tcp) server and redirect to / from a com port

//TODO: due to the way the DossySerialPort works this is pretty much half duplex in that
//it will send blocks of MAX_BLOCK_SIZE bytes before checking for input



using DossySerialPort;
using System.Net.Sockets;
using System.Text;

namespace MyProject;
class Program
{
    const int MAX_BLOCK_SIZE = 64;

    static void Main(string[] args)
    {
        string s_serport, s_baud, s_host, s_portno;

        if (args.Length != 4)
            Usage(Console.Error, "Incorrect number of arguments", exit: 1);

        s_serport = args[0];
        s_baud = args[1];
        s_host = args[2];
        s_portno = args[3];

        int i_baud = -1;
        try
        {
            i_baud = Convert.ToInt32(s_baud);
        }
        catch { }
        if (i_baud < 75 || i_baud > 200000)
            Usage(Console.Error, $"Bad baud rate {s_baud} should be 75<=BAUD<200000", exit: 1);

        int i_portno = -1;
        try
        {
            i_portno = Convert.ToInt32(s_portno);
        } catch { };

        if (i_portno < 0 || i_portno > 65535)
            Usage(Console.Error, $"Bad port number : \"{s_portno}\"", exit: 1);

        TcpClient tcp = null;
        try
        {
            tcp = new TcpClient(s_host, i_portno);
        } catch (Exception ex)
        {
            Usage(Console.Error, $"Unable to connect to {s_host}:{s_portno}", ex, exit: 2);
        }

        byte[] buffer = new byte[MAX_BLOCK_SIZE];
        using (IDossySerial com = new DossySerial(s_serport, i_baud))
        {
            using (var s = tcp.GetStream())
            {
                while (true)
                {
                    if (s.DataAvailable)
                    {
                        int n = s.Read(buffer, 0, MAX_BLOCK_SIZE);
                        if (n > 0)
                        {
                            com.Write(buffer, 0, n);
                        }
                    }

                    if (com.Available >0)
                    {
                        int n = com.Read(buffer, MAX_BLOCK_SIZE, immediate:true);
                        s.Write(buffer, 0, n);  
                    }

                    Thread.Sleep(10);
                }
            }
        }
    }

    /// <summary>
    /// Write Usage string and optional message, exception details to stream and optionally exit
    /// </summary>
    /// <param name="wr">TextWriter to write to</param>
    /// <param name="msg">Optional error message</param>
    /// <param name="ex">Optional Exception details</param>
    /// <param name="exit">Optional. If supplied exit with given code</param>
    static void Usage(TextWriter wr, string? msg, Exception? ex = null, int? exit = null)
    {
        wr.WriteLine(@"
SimpleNetCat <COM> <SPEED> <HOST> <PORT>

A simple netcat like program to pipe serial input/output (from a micro) to the
given TCP server.

<COM>   Windows COM port to pipe to/from
<SPEED> Baud rater
<HOST>  Remote hostname / IP address
<PORT>  Port number
");

        if (ex != null)
            wr.WriteLine($"{ex}\n\n");
        if (msg != null)
            wr.WriteLine($"{msg}\n\n");
        if (exit != null)
            Environment.Exit((int)exit);
    }
}
