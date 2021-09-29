using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
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


        public MainWindow()
        {

            InitializeComponent();
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

        bool _lbMessagesAtEnd = true;
        ScrollViewer _lbMessages_scrollViewer;

        private void LbMessages_Loaded(object sender, RoutedEventArgs e)
        {
            var listBox = (ListBox)sender;

            _lbMessages_scrollViewer = FindScrollViewer(listBox);

            if (_lbMessages_scrollViewer != null)
            {
                _lbMessages_scrollViewer.ScrollChanged += (o, args) =>
                {
                    if (args.ExtentHeightChange == 0)
                    {   // Caused by user scrolls
                        if (_lbMessages_scrollViewer.VerticalOffset == _lbMessages_scrollViewer.ScrollableHeight) { 
                            //we're at then end
                            _lbMessagesAtEnd = true;
                        }
                        else
                        {   
                            //we're looking elsewhere
                            _lbMessagesAtEnd = false;
                        }
                    } else if (_lbMessagesAtEnd)  {   
                        //keep at end
                        _lbMessages_scrollViewer.ScrollToVerticalOffset(_lbMessages_scrollViewer.ExtentHeight);
                    }
                };
            }
        }


        private void Window_Closed(object sender, EventArgs e)
        {
            var serialPort = (DataContext as DeIceAppModel)?.Serial;
            if ( serialPort != null)
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
