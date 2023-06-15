using DeIce68k.ViewModel;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO.Ports;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace DeIce68k
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
		private void Application_Startup(object sender, StartupEventArgs e)
		{


            DeIceAppModel appModel;
            DossySerialPort.DossySerial serialPort;

            // Create the startup window

            try
            {
                List<string> commandFiles = new List<string>();

                string[] args = Environment.GetCommandLineArgs();
                int i = 1;
                while (i < args.Length && args[i].StartsWith("/"))
                {
                    if (args[i] == "/exec")
                    {
                        i++;
                        if (i >= args.Length)
                            throw new ArgumentException("Missing exec file argument");
                        commandFiles.Add(args[i]);
                    }
                    else
                    {
                        throw new ArgumentException($"Unrecognised switch {args[i]}");
                    }

                    i++;
                }

                if (args.Length < 1)
                {
                    throw new ArgumentException("Missing COM port argument");
                }
                string comport = args[i];

                serialPort = new DossySerialPort.DossySerial(comport, 19200);

                MainWindow wnd = new MainWindow();

                appModel = new DeIceAppModel(serialPort, wnd);
                appModel.Init();

                wnd.DataContext = appModel;
                // Show the window
                wnd.Show();


                foreach (var l in commandFiles)
                    try
                    {
                        appModel.ReadCommandFile(l);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"Error reading command file {l} ; {ex.ToString()}");
                    }

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "An error occurred", MessageBoxButton.OK, MessageBoxImage.Error);
            }


        }

    }
}
