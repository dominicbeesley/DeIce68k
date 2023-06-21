using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
    public partial class DlgAddWatch : Window
    {
        public DeIceAppModel Context { get; }

        public DisassAddressBase Address {
            get => txtAddress.Address;
            set => txtAddress.Address = value;
        }

        public string Symbol
        {
            get => txtAddress.Symbol;
            set => txtAddress.Symbol = value;
        }

        public WatchType WatchType
        {
            get => (WatchType)Enum.Parse(typeof(WatchType), cbWatchType.SelectedItem.ToString() ?? "Empty");
            set => cbWatchType.SelectedItem = Enum.GetName(typeof(WatchType), value);        
        }

        public uint[] Indices { 
            get {
                uint[] ret;
                GetIndices(out ret);
                return ret;
            }
            set => txtIndices.Text = value.Length == 0 ? "[]" : string.Join("", value.Select(x => $"[{x}]"));
        }

        public DlgAddWatch(DeIceAppModel context)
        {
            this.Context = context;
            this.DataContext = context;
            InitializeComponent();
        }

        private void ButtonOK_Click(object sender, RoutedEventArgs e)
        {
            if (!txtAddress.Valid)
            {
                MessageBox.Show("Bad address or symbol", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (cbWatchType.SelectedIndex < 0)
            {
                MessageBox.Show("Please select a type", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            uint[] tmp;
            if (!GetIndices(out tmp))
            {
                MessageBox.Show("Bad or missing indices", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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


        Regex reIndices = new Regex(@"^(\[([0-9]+)\])+", RegexOptions.Compiled);

        protected bool GetIndices(out uint[] ret)
        {
            ret = new uint[] { };

            var s = txtIndices.Text.Replace(" ", "");
            if (string.IsNullOrEmpty(s) || s == "[]")
                return true;

            var mWA = reIndices.Match(s);
            if (mWA.Success)
            {
                ret = mWA.Groups[1].Captures.Select(x => Convert.ToUInt32(x)).ToArray();

                return true;
            }
            return false;
        }
    }
}
