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
using Microsoft.Win32;

namespace DeIce68k
{
    /// <summary>
    /// Interaction logic for DlgTraceTo.xaml
    /// </summary>
    public partial class DlgLoadBinary : Window
    {
        public DeIceAppModel Context { get; }

        public DlgLoadBinary(DeIceAppModel context)
        {
            this.Context = context;
            InitializeComponent();
        }

        public uint Address {
            get
            {

                uint a = 0;
                try
                {
                    if (!Context.Symbols.SymbolToAddress(txtAddress.Text, out a))
                        a = Convert.ToUInt32(txtAddress.Text, 16);
                }
                catch (Exception ex) { }
                return a;
            }
            set
            {
                var sym = Context?.Symbols?.AddressToSymbols(value).FirstOrDefault();
                if (sym != null)
                    txtAddress.Text = sym;
                else
                    txtAddress.Text = $"{value:X08}";
            }
        }

        public string FileName { 
            get => txtFilename.Text;
            set => txtFilename.Text = value;
        }

        private void ButtonOK_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                uint a;
                if (!Context.Symbols.SymbolToAddress(txtAddress.Text, out a))
                    a = Convert.ToUInt32(txtAddress.Text, 16);
            } catch (Exception)
            {
                MessageBox.Show("No such symbol or bad address", "Bad Address", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!System.IO.File.Exists(txtFilename.Text))
                MessageBox.Show($"No such file \"{txtFilename.Text}\"");

            DialogResult = true;
            Close();
        }

        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void btnFilePick_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog();
            dlg.Filter = "All files|*.*";
            dlg.Title = "Open binary file";
            dlg.FileName = txtFilename.Text;
            if (dlg.ShowDialog(Context.MainWindow) == true)
                txtFilename.Text = dlg.FileName;


        }
    }
}
