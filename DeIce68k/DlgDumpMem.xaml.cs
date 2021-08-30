using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using DeIce68k.ViewModel;


namespace DeIce68k
{
    /// <summary>
    /// Interaction logic for DlgTraceTo.xaml
    /// </summary>
    public partial class DlgDumpMem : Window
    {
        public DeIceAppModel Context { get; }

        public DlgDumpMem(DeIceAppModel context)
        {
            this.Context = context;
            InitializeComponent();
        }

        public uint Address { get; private set; }

        private void ButtonOK_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                uint a;
                if (!Context.Symbols.SymbolToAddress(txtAddress.Text, out a))
                    a = Convert.ToUInt32(txtAddress.Text, 16);
                Address = a;
            } catch (Exception)
            {
                MessageBox.Show("No such symbol or bad address", "Bad Address", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            DialogResult = true;
            Close();
        }

        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
