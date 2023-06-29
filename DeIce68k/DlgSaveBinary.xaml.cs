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
using Microsoft.Win32;

namespace DeIce68k
{
    /// <summary>
    /// Interaction logic for DlgTraceTo.xaml
    /// </summary>
    public partial class DlgSaveBinary : Window
    {
        public DeIceAppModel Context { get; }

        public DlgSaveBinary(DeIceAppModel context)
        {
            this.Context = context;
            this.DataContext = context;
            InitializeComponent();
        }

        public DisassAddressBase Address
        {
            get
            {

                return ucAddr.Address;
            }
            set
            {
                ucAddr.Address = value;
            }
        }

        public string FileName
        {
            get => txtFilename.Text;
            set => txtFilename.Text = value;
        }

        public long Length
        {
            get { try { return Convert.ToInt64(txtLength.Text, 16); } catch { return 0; } }
            set { txtLength.Text = value.ToString("X"); }
        }

        private void ButtonOK_Click(object sender, RoutedEventArgs e)
        {
            if (Address == null)
            {
                MessageBox.Show("No such symbol or bad address", "Bad Address", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (Length <= 0)
            {
                MessageBox.Show("Please set a valid hexadecimal length", "Bad Length", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var fn = txtFilename.Text;
            if (string.IsNullOrWhiteSpace(fn))
            {
                MessageBox.Show("Please enter a filename", "Bad filename", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var dr = System.IO.Path.GetDirectoryName(fn);
            if (!System.IO.Directory.Exists(dr))
            {
                MessageBox.Show($"Directory \"{dr}\" does not exist.", "Bad filename", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (System.IO.File.Exists(txtFilename.Text) &&
                MessageBox.Show($"File \"{fn}\" already exist - do you want to overwrite it?", "Overwrite?", MessageBoxButton.YesNo, MessageBoxImage.Hand) != MessageBoxResult.Yes)
                return;

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
            var dlg = new SaveFileDialog();
            dlg.Filter = "All files|*.*";
            dlg.Title = "Open binary file";
            dlg.FileName = txtFilename.Text;
            if (dlg.ShowDialog(Context.MainWindow) == true)
                txtFilename.Text = dlg.FileName;


        }
    }
}
