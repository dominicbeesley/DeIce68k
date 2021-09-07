using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using DeIce68k.ViewModel;
using DeIceProtocol;

namespace DeIce68k
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        DeIceAppModel appModel;
        DossySerialPort.DossySerial serialPort;

        public MainWindow()
        {

            InitializeComponent();

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
                    } else
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
                appModel = new DeIceAppModel(serialPort, this);
                appModel.Init();


                foreach (var l in commandFiles)
                    try
                    {
                        appModel.ReadCommandFile(l);
                    } catch (Exception ex)
                    {
                        throw new Exception($"Error reading command file {l}");
                    }
                
            } catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "An error occurred", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            this.DataContext = appModel;
        }


        // Search for ScrollViewer, breadth-first
        private static ScrollViewer FindScrollViewer(DependencyObject root)
        {
            var queue = new Queue<DependencyObject>(new[] { root });

            do
            {
                var item = queue.Dequeue();

                if (item is ScrollViewer)
                    return (ScrollViewer)item;

                for (var i = 0; i < VisualTreeHelper.GetChildrenCount(item); i++)
                    queue.Enqueue(VisualTreeHelper.GetChild(item, i));
            } while (queue.Count > 0);

            return null;
        }

        private void LbMessages_Loaded(object sender, RoutedEventArgs e)
        {
            var listBox = (ListBox)sender;

            var scrollViewer = FindScrollViewer(listBox);

            if (scrollViewer != null)
            {
                scrollViewer.ScrollChanged += (o, args) =>
                {
                    if (args.ExtentHeightChange > 0)
                        scrollViewer.ScrollToBottom();
                };
            }
        }


        private void Window_Closed(object sender, EventArgs e)
        {
            if (serialPort != null)
            {
                serialPort.Dispose();
                serialPort = null;
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs ee)
        {



        }

    }
}
