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
using DisassShared;

namespace DeIce68k
{
    /// <summary>
    /// Interaction logic for DlgTraceTo.xaml
    /// </summary>
    public partial class DlgTraceTo : Window
    {
        public DeIceAppModel Context { get; }

        public DlgTraceTo(DeIceAppModel context)
        {
            this.Context = context;
            InitializeComponent();
        }

        public DisassAddressBase Address { get; private set; }

        private void ButtonOK_Click(object sender, RoutedEventArgs e)
        {
            if (!ucAddr.Valid)
            {
                MessageBox.Show("No such symbol or bad address", "Bad Address", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            else
            {
                Address = ucAddr.Address;
                DialogResult = true;
                Close();
            }
        }

        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
