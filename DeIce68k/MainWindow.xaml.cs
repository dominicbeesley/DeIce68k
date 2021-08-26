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
                serialPort = new DossySerialPort.DossySerial("COM3", 19200);
                appModel = new DeIceAppModel(serialPort, this);
                appModel.ReadCommandFile(@"E:\Users\dominic\programming\68k\mos\mos68k.noi");
                
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
