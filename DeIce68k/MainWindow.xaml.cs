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

        public MainWindow()
        {

            InitializeComponent();

            try
            {
                appModel = new DeIceAppModel("COM19", 19200, this);
                appModel.ReadCommandFile(@"E:\Users\dominic\electronics\bbcmicro\TUBE\68008\mos86k\mos\mos68k.noi");
                
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
            if (appModel != null)
            {
                appModel.Dispose();
                appModel = null;
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs ee)
        {



        }

       
    }
}
